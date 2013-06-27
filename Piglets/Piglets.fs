namespace IntelliFactory.WebSharper.Piglets

open IntelliFactory.WebSharper

type Result<'a> =
    | Success of 'a
    | Failure of string list

    [<JavaScript>]
    member this.isSuccess =
        match this with
        | Success _ -> true
        | Failure _ -> false

    [<JavaScript>]
    static member Ap(r1: Result<'a -> 'b>, r2: Result<'a>) =
        match r1, r2 with
        | Success f, Success x -> Success(f x)
        | Failure m, Success _
        | Success _, Failure m -> Failure m
        | Failure m1, Failure m2 -> Failure(m1 @ m2)

    [<JavaScript>]
    static member Join (r: Result<Result<'a>>) =
        match r with
        | Failure m
        | Success (Failure m) -> Failure m
        | Success (Success x) -> Success x

[<Interface>]
type Reader<'a> =
    abstract member Latest : Result<'a>
    abstract member Subscribe : (Result<'a> -> unit) -> unit

[<Interface>]
type Writer<'a> =
    abstract member Trigger : Result<'a> -> unit

[<Sealed>]
type Stream<'a> [<JavaScript>] (init: Result<'a>) =
    let s = IntelliFactory.Reactive.HotStream.New init

    [<JavaScript>]
    member this.Latest =
        (!s.Latest).Value

    [<JavaScript>]
    member this.Subscribe f =
        s.Add f

    [<JavaScript>]
    member this.Trigger x =
        s.Trigger x

    interface Writer<'a> with
        [<JavaScript>] member this.Trigger x = this.Trigger x

    interface Reader<'a> with
        [<JavaScript>] member this.Latest = this.Latest
        [<JavaScript>] member this.Subscribe f = this.Subscribe f

/// I'd rather use an object expression,
/// but they're forbidden inside [<JS>].
[<Sealed>]
type ConcreteWriter<'a> [<JavaScript>] (trigger: Result<'a> -> unit) =
    interface Writer<'a> with
        [<JavaScript>]
        member this.Trigger x = trigger x

/// I'd rather use an object expression,
/// but they're forbidden inside [<JS>].
[<Sealed>]
type ConcreteReader<'a> [<JavaScript>] (latest, subscribe) =
    interface Reader<'a> with
        [<JavaScript>]
        member this.Latest = latest()
        [<JavaScript>]
        member this.Subscribe f = subscribe f

[<Sealed>]
type Submitter<'a> [<JavaScript>] (input: Reader<'a>) =

    let output = Stream(Failure [])

    let writer =
        ConcreteWriter(fun unitIn ->
            match unitIn, input.Latest with
            | Failure m1, Failure m2 -> output.Trigger(Failure(m1 @ m2))
            | Failure m, Success _
            | Success _, Failure m -> output.Trigger(Failure m)
            | Success(), Success x -> output.Trigger(Success x))
        :> Writer<unit>

    [<JavaScript>]
    member this.Input = input

    [<JavaScript>]
    member this.Output = output

    interface Writer<unit> with
        [<JavaScript>] member this.Trigger(x) = writer.Trigger(x)

    interface Reader<'a> with
        [<JavaScript>] member this.Latest = output.Latest
        [<JavaScript>] member this.Subscribe f = output.Subscribe f

module private Stream =

    [<JavaScript>]
    let Ap (sf: Stream<'a -> 'b>) (sx: Stream<'a>) : Stream<'b> =
        let out = Stream(Result.Ap(sf.Latest, sx.Latest))
        sf.Subscribe(fun f -> out.Trigger(Result.Ap(f, sx.Latest)))
        sx.Subscribe(fun x -> out.Trigger(Result.Ap(sf.Latest, x)))
        out

    [<JavaScript>]
    let ApJoin (sf: Stream<'a -> 'b>) (sx: Stream<Result<'a>>) : Stream<'b> =
        let out = Stream(Result.Ap(sf.Latest, Result.Join sx.Latest))
        sf.Subscribe(fun f -> out.Trigger(Result.Ap(f, Result.Join sx.Latest)))
        sx.Subscribe(fun x -> out.Trigger(Result.Ap(sf.Latest, Result.Join x)))
        out

type Piglet<'a, 'v> =
    {
        stream: Stream<'a>
        view: 'v
    }

[<AutoOpen>]
module Pervasives =

    /// Push an argument to the view function.
    [<JavaScript>]
    [<Inline>]
    let (<<^) v a = fun x -> v x a

    /// Map argument(s) to the view function.
    [<JavaScript>]
    [<Inline>]
    let (>>^) v f = fun g -> g (v f)

    [<JavaScript>]
    let (<*>) f x =
        {
            stream = Stream.Ap f.stream x.stream
            view = f.view >> x.view
        }

    [<JavaScript>]
    let (<*?>) f x =
        {
            stream = Stream.ApJoin f.stream x.stream
            view = f.view >> x.view
        }

module Piglet =

    [<JavaScript>]
    let Yield (x: 'a) =
        let s = Stream(Success x)
        {
            stream = s
            view = fun f -> f s
        }

    [<JavaScript>]
    let Return (x: 'a) =
        {
            stream = Stream(Success x)
            view = id
        }

    [<JavaScript>]
    let WithSubmit fin =
        let submitter = Submitter(fin.stream)
        {
            stream = submitter.Output
            view = fin.view <<^ submitter
        }

    [<JavaScript>]
    let TransmitStream f =
        {
            stream = f.stream
            view = f.view <<^ f.stream
        }

    [<JavaScript>]
    let TransmitReader f =
        {
            stream = f.stream
            view = f.view <<^ (f.stream :> Reader<_>)
        }

    [<JavaScript>]
    let TransmitWriter f =
        {
            stream = f.stream
            view = f.view <<^ (f.stream :> Writer<_>)
        }

    [<JavaScript>]
    let MapResult m f =
        let out = Stream(m f.stream.Latest)
        f.stream.Subscribe(out.Trigger << m)
        {
            stream = out
            view = f.view
        }

    [<JavaScript>]
    let MapToResult m f =
        f |> MapResult (function
            | Failure msg -> Failure msg
            | Success x -> m x)

    [<JavaScript>]
    let Map m f =
        f |> MapResult (function
            | Failure msg -> Failure msg
            | Success x -> Success (m x))

    [<JavaScript>]
    let MapAsyncResult m f =
        let out = Stream (Failure [])
        f.stream.Subscribe (fun v ->
            async {
                let! res = m v
                return out.Trigger res
            } |> Async.Start)
        async {
            let! res = m f.stream.Latest
            return out.Trigger res
        }
        |> Async.Start
        {
            stream = out
            view = f.view
        }

    [<JavaScript>]
    let MapToAsyncResult m f =
        f |> MapAsyncResult (function
            | Failure msg -> async.Return (Failure msg)
            | Success x -> m x)

    [<JavaScript>]
    let MapAsync m f =
        f |> MapAsyncResult (function
            | Failure msg -> async.Return (Failure msg)
            | Success x ->
                async {
                    let! res = m x
                    return Success res
                })

    [<JavaScript>]
    let Run action f =
        f.stream.Subscribe(function
            | Success x -> action x
            | Failure _ -> ())
        f

    [<JavaScript>]
    let Render view f =
        f.view view

    [<JavaScript>]
    let MapViewArgs view f =
        {
            stream = f.stream
            view = f.view >>^ view
        }

    module Validation =

        open IntelliFactory.WebSharper.EcmaScript

        [<JavaScript>]
        let Is pred msg f =
            let s' = Stream(f.stream.Latest)
            f.stream.Subscribe(function
                | Failure m -> s'.Trigger (Failure m)
                | Success x when pred x -> s'.Trigger (Success x)
                | _ -> s'.Trigger (Failure [msg]))
            { f with
                stream = s'
            }

        [<JavaScript>]
        let IsNotEmpty msg f =
            Is ((<>) "") msg f

        [<JavaScript>]
        let IsMatch re msg f =
            Is (RegExp re).Test msg f
