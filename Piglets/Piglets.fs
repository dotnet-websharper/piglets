namespace IntelliFactory.WebSharper.Piglets

open IntelliFactory.WebSharper

type Result<'a> =
    {
        Value : 'a option
        Errors : string list
    }

    [<JavaScript>]
    static member Success (x: 'a) =
        { Value = Some x; Errors = [] }

    [<JavaScript>]
    static member Empty =
        { Value = None; Errors = [] }

    [<JavaScript>]
    member this.IsSuccess =
        not this.Errors.IsEmpty

module Result =
    [<JavaScript>]
    [<Inline>]
    let Value x = x.Value

    [<JavaScript>]
    [<Inline>]
    let Errors x = x.Errors

type Result<'a> with
    [<JavaScript>]
    static member Ap(r1: Result<'a -> 'b>, r2: Result<'a>) =
        {
            Value =
                match r1.Value, r2.Value with
                | Some f, Some x
                    when r1.Errors.IsEmpty && r2.Errors.IsEmpty ->
                    Some (f x)
                | _ -> None
            Errors = r1.Errors @ r2.Errors
        }

    [<JavaScript>]
    static member Join (r: Result<Result<'a>>) =
        {
            Value = Option.bind Result.Value r.Value
            Errors = r.Errors @ defaultArg (Option.map Result.Errors r.Value) []
        }

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

    let output = Stream({ Value = None; Errors = input.Latest.Errors })

    let writer =
        ConcreteWriter(fun unitIn ->
            output.Trigger {
                Value = input.Latest.Value
                Errors = input.Latest.Errors @ unitIn.Errors
            })
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
        let s = Stream(Result.Success x)
        {
            stream = s
            view = fun f -> f s
        }

    [<JavaScript>]
    let Return (x: 'a) =
        {
            stream = Stream(Result.Success x)
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
        f |> MapResult (fun r ->
            match r.Value with
            | None -> { Value = None; Errors = r.Errors }
            | Some v ->
                let res = m v
                { res with Errors = r.Errors @ res.Errors })

    [<JavaScript>]
    let Map m f =
        f |> MapResult (fun r ->
            { Value = Option.map m r.Value; Errors = r.Errors })

    [<JavaScript>]
    let MapAsyncResult m f =
        let out = Stream ({ Value = None; Errors = f.stream.Latest.Errors })
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
        f |> MapAsyncResult (fun v ->
            match v.Value with
            | None -> async.Return { Value = None; Errors = v.Errors }
            | Some x ->
                async {
                    let! v' = m x
                    return { Value = v'.Value; Errors = v.Errors @ v'.Errors }
                })

    [<JavaScript>]
    let MapAsync m f =
        f |> MapAsyncResult (fun v ->
            match v.Value with
            | None -> async.Return { Value = None; Errors = v.Errors }
            | Some x ->
                async {
                    let! res = m x
                    return { Value = Some res; Errors = v.Errors }
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
