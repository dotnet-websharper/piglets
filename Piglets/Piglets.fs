namespace IntelliFactory.WebSharper.Piglets

open System.Collections.Generic
open IntelliFactory.WebSharper

type ValidatorId = int

type Result<'a> =
    {
        Value : 'a option
        Errors : Map<ValidatorId, string>
    }

module Map =

    [<JavaScript>]
    let merge m1 m2 = Map.foldBack Map.add m1 m2

module M = Map

module Result =

    [<JavaScript>]
    [<Inline>]
    let Success (x: 'a) =
        { Value = Some x; Errors = Map.empty }

    [<JavaScript>]
    [<Inline>]
    let Empty : Result<'a> =
        { Value = None; Errors = Map.empty }

    [<JavaScript>]
    [<Inline>]
    let Failure msg : Result<'a> =
        { Value = None; Errors = Map.ofList [0, msg] }

    [<JavaScript>]
    [<Inline>]
    let IsSuccess x =
        x.Errors.IsEmpty && x.Value.IsSome

    [<JavaScript>]
    [<Inline>]
    let Value x = x.Value

    [<JavaScript>]
    let SuccessValue x =
        if Map.isEmpty x.Errors then
            x.Value
        else None

    [<JavaScript>]
    let Errors x =
        x.Errors
        |> Seq.map (fun (KeyValue(_, v)) -> v)
        |> List.ofSeq

    [<JavaScript>]
    [<Inline>]
    let ErrorsMap x =
        x.Errors

    [<JavaScript>]
    let (|Success|Failure|) x =
        if x.Value.IsSome && M.isEmpty x.Errors then
            Success x.Value.Value
        else Failure (Errors x)

type Result<'a> with
    [<JavaScript>]
    static member Ap(r1: Result<'a -> 'b>, r2: Result<'a>) =
        let errors = Map.merge r1.Errors r2.Errors
        {
            Value =
                match r1.Value, r2.Value with
                | Some f, Some x when Map.isEmpty errors -> Some (f x)
                | _ -> None
            Errors = errors
        }

    [<JavaScript>]
    static member Join (r: Result<Result<'a>>) =
        {
            Value = Option.bind Result.Value r.Value
            Errors = Map.merge r.Errors (defaultArg (Option.map Result.ErrorsMap r.Value) Map.empty)
        }

[<AbstractClass>]
type Reader<'a> [<JavaScript>] () =
    abstract member Latest : Result<'a>
    abstract member Subscribe : (Result<'a> -> unit) -> unit
    abstract member SubscribeImmediate : (Result<'a> -> unit) -> unit
    [<JavaScript>]
    default this.SubscribeImmediate f =
        f this.Latest
        this.Subscribe f

[<Interface>]
type Writer<'a> =
    abstract member Trigger : Result<'a> -> unit

[<Sealed>]
type Stream<'a> [<JavaScript>] (init: Result<'a>) =
    inherit Reader<'a>()

    let s = IntelliFactory.Reactive.HotStream.New init
    let mutable triggering = false
    let toTrigger = new Queue<Result<'a>>()

    [<JavaScript>]
    override this.Latest =
        (!s.Latest).Value

    [<JavaScript>]
    override this.Subscribe f =
        s.Add f

    [<JavaScript>]
    member this.Trigger x =
        toTrigger.Enqueue x
        if not triggering then
            triggering <- true
            while toTrigger.Count > 0 do
                s.Trigger (toTrigger.Dequeue())
            triggering <- false

    interface Writer<'a> with
        [<JavaScript>] member this.Trigger x = this.Trigger x

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
    inherit Reader<'a>()

    [<JavaScript>]
    override this.Latest = latest()
    [<JavaScript>]
    override this.Subscribe f = subscribe f

[<Sealed>]
type Submitter<'a> [<JavaScript>] (input: Reader<'a>) =
    inherit Reader<'a>()

    let output = Stream({ Value = None; Errors = input.Latest.Errors })

    let writer =
        ConcreteWriter(fun unitIn ->
            output.Trigger {
                Value = input.Latest.Value
                Errors = Map.merge input.Latest.Errors unitIn.Errors
            })
        :> Writer<unit>

    [<JavaScript>]
    member this.Input = input

    [<JavaScript>]
    member this.Output = output

    interface Writer<unit> with
        [<JavaScript>] member this.Trigger(x) = writer.Trigger(x)

    [<JavaScript>] override this.Latest = output.Latest
    [<JavaScript>] override this.Subscribe f = output.Subscribe f

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
                { res with Errors = Map.merge r.Errors res.Errors })

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
                    return { Value = v'.Value; Errors = M.merge v.Errors v'.Errors }
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
            | { Errors = e; Value = Some v } when M.isEmpty e -> action v
            | _ -> ())
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
        let nextId =
            let current = ref 0
            fun () ->
                incr current
                !current

        [<JavaScript>]
        let Is pred msg f =
            let id = nextId()
            f.stream.SubscribeImmediate(fun v ->
                match v.Value with
                | Some x when pred x ->
                    // Remove the error message if it was there
                    match M.tryFind id v.Errors with
                    | Some _ ->
                        f.stream.Trigger({ v with Errors = M.remove id v.Errors })
                    | None -> ()
                | Some x ->
                    // Add the error message if it wasn't there
                    match M.tryFind id v.Errors with
                    | Some msg -> ()
                    | None ->
                        f.stream.Trigger({ v with Errors = M.add id msg v.Errors })
                | None -> ())
            f

        [<JavaScript>]
        let IsNotEmpty msg f =
            Is ((<>) "") msg f

        [<JavaScript>]
        let IsMatch re msg f =
            Is (RegExp re).Test msg f
