namespace IntelliFactory.WebSharper.Piglets

open System
open IntelliFactory.WebSharper

module Id =

    [<JavaScript>]
    let next =
        let current = ref 0
        fun () ->
            // must increment first: id 0 is reserved.
            incr current
            !current

type ErrorSourceId = int

[<Sealed>]
type ErrorMessage [<JavaScript>] (message, source) =
    [<JavaScript>]
    member this.Message : string = message
    [<JavaScript>]
    member this.Source : ErrorSourceId = source

type Result<'a> =
    | Success of 'a
    | Failure of ErrorMessage list

    [<JavaScript>]
    member this.isSuccess =
        match this with
        | Success _ -> true
        | Failure _ -> false

    [<JavaScript>]
    static member Failwith msg : Result<'a> = Failure [ErrorMessage(msg, 0)]

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

    [<JavaScript>]
    static member Map (f: 'a -> 'b) = function
        | Success x -> Success (f x)
        | Failure m -> Failure m

    [<JavaScript>]
    static member Iter (f: 'a -> unit) = function
        | Success x -> f x
        | Failure _ -> ()

[<AbstractClass>]
type Reader<'a> [<JavaScript>] (id) =
    abstract member Latest : Result<'a>
    abstract member Subscribe : (Result<'a> -> unit) -> IDisposable

    [<JavaScript>]
    member this.SubscribeImmediate f =
        f this.Latest
        this.Subscribe f

    [<JavaScript>]
    member this.Id = id

    [<JavaScript>]
    member this.Through (r: Reader<'b>) =
        let out = Stream(this.Latest)
        r.Subscribe(function
            | Success _ -> out.Trigger(this.Latest)
            | Failure msgs ->
                match this.Latest, msgs |> List.filter (fun m -> m.Source = this.Id) with
                | _, [] -> out.Trigger this.Latest
                | Success x, l -> out.Trigger (Failure l)
                | Failure l, l' -> out.Trigger (Failure (l @ l')))
        |> ignore
        out :> Reader<'a>

and [<Interface>] Writer<'a> =
    abstract member Trigger : Result<'a> -> unit

and [<Sealed>] Stream<'a> [<JavaScript>] (init: Result<'a>, ?id) =
    inherit Reader<'a>(match id with Some id -> id | None -> Id.next())
    let s = IntelliFactory.Reactive.HotStream.New init

    [<JavaScript>]
    override this.Latest =
        (!s.Latest).Value

    [<JavaScript>]
    override this.Subscribe f =
        s.Subscribe f

    [<JavaScript>]
    member this.Trigger x =
        s.Trigger x

    interface Writer<'a> with
        [<JavaScript>] member this.Trigger x = this.Trigger x

type ErrorMessage with
    [<JavaScript>]
    static member Create msg (reader: Reader<'a>) =
        new ErrorMessage(msg, reader.Id)
            

/// I'd rather use an object expression,
/// but they're forbidden inside [<JS>].
[<Sealed>]
[<JavaScript>]
type ConcreteWriter<'a> (trigger: Result<'a> -> unit) =

    static member New (trigger: 'a -> unit) =
        ConcreteWriter<'a>(function
            | Success x -> trigger x
            | Failure _ -> ())

    interface Writer<'a> with
        [<JavaScript>]
        member this.Trigger x = trigger x

/// I'd rather use an object expression,
/// but they're forbidden inside [<JS>].
[<Sealed>]
type ConcreteReader<'a> [<JavaScript>] (latest, subscribe) =
    inherit Reader<'a>(Id.next())
    [<JavaScript>]
    override this.Latest = latest()
    [<JavaScript>]
    override this.Subscribe f = subscribe f

[<Sealed>]
type Submitter<'a> [<JavaScript>] (input: Reader<'a>) =
    inherit Reader<'a>(Id.next())

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

    [<JavaScript>]
    member this.Trigger() = writer.Trigger(Success())

    interface Writer<unit> with
        [<JavaScript>] member this.Trigger(x) = writer.Trigger(x)

    [<JavaScript>] override this.Latest = output.Latest
    [<JavaScript>] override this.Subscribe f = output.Subscribe f

module private Stream =

    [<JavaScript>]
    let Ap (sf: Stream<'a -> 'b>) (sx: Stream<'a>) : Stream<'b> =
        let out = Stream(Result.Ap(sf.Latest, sx.Latest))
        sf.Subscribe(fun f -> out.Trigger(Result.Ap(f, sx.Latest))) |> ignore
        sx.Subscribe(fun x -> out.Trigger(Result.Ap(sf.Latest, x))) |> ignore
        out

    [<JavaScript>]
    let ApJoin (sf: Stream<'a -> 'b>) (sx: Stream<Result<'a>>) : Stream<'b> =
        let out = Stream(Result.Ap(sf.Latest, Result.Join sx.Latest))
        sf.Subscribe(fun f -> out.Trigger(Result.Ap(f, Result.Join sx.Latest))) |> ignore
        sx.Subscribe(fun x -> out.Trigger(Result.Ap(sf.Latest, Result.Join x))) |> ignore
        out

type Piglet<'a, 'v> =
    {
        stream: Stream<'a>
        view: 'v
    }

    [<JavaScript>]
    member this.Stream = this.stream

[<AutoOpen>]
module Pervasives =

    [<Inline "$arr.push($x)">]
    let push (arr: 'T []) (x: 'T) = ()

    [<Direct "Array.prototype.splice.apply($arr, [$index, $howMany].concat($items))">]
    let splice (arr: 'T []) (index: int) (howMany: int) (items: 'T[]) : 'T [] = items

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

type Container<'t, 'u> =
    abstract member Add : 't -> unit
    abstract member Remove : int -> unit
    abstract member MoveUp : int -> unit
    abstract member Container : 'u

[<JavaScript>]
module Many =

    type Operations(delete: unit -> unit, moveUp: Submitter<unit>, moveDown: Submitter<unit>) =
        member this.Delete = ConcreteWriter.New delete :> Writer<_>
        member this.MoveUp = moveUp
        member this.MoveDown = moveDown

    type Stream<'a, 'v, 'w>(p : 'a -> Piglet<'a, 'v -> 'w>, out: Stream<'a[]>, init: 'a) =

        inherit Reader<'a[]>(out.Id)

        let addTrigger = Stream<unit>(Failure [])

        let streams : Stream<'a>[] = [||]

        let update() =
            Array.foldBack (fun (cur: Stream<'a>) acc ->
                match acc, cur.Latest with
                | Success l, Success x -> Success (x :: l)
                | Failure m , Success _
                | Success _, Failure m -> Failure m
                | Failure m1, Failure m2 -> Failure (m2 @ m1))
                streams
                (Success [])
            |> Result.Map Array.ofList
            |> out.Trigger

        override this.Subscribe f = out.Subscribe f

        override this.Latest = out.Latest

        member this.Render (c: Container<'w, 'u>) (f : Operations -> 'v) : 'u =
            let add x =
                let piglet = p x
                push streams piglet.stream
                piglet.stream.SubscribeImmediate (fun _ -> update()) |> ignore
                let getThisIndex() =
                    streams |> Seq.findIndex (fun x -> x.Id = piglet.stream.Id)
                let moveUp i =
                    if i > 0 && i < streams.Length then
                        let s = streams.[i]
                        streams.[i] <- streams.[i-1]
                        streams.[i-1] <- s
                        c.MoveUp i
                        update()
                let moveDown () =
                    moveUp (getThisIndex() + 1)
                let moveUp () = moveUp (getThisIndex())
                let canMoveUp () =
                    if getThisIndex() > 0 then Success() else Failure []
                let canMoveDown () =
                    if getThisIndex() < streams.Length - 1 then Success() else Failure []
                let inMoveUp = Stream<_>(canMoveUp())
                let inMoveDown = Stream<_>(canMoveDown())
                let outSubscription =
                    out.Subscribe(fun _ ->
                        inMoveUp.Trigger(canMoveUp())
                        inMoveDown.Trigger(canMoveDown()))
                let subMoveUp = Submitter(inMoveUp)
                let subMoveDown = Submitter(inMoveDown)
                let subUpSubscription =
                    subMoveUp.Subscribe(Result.Iter moveUp)
                let subDownSubscription =
                    subMoveDown.Subscribe(Result.Iter moveDown)
                let delete () =
                    let i = getThisIndex()
                    splice streams i 1 [||] |> ignore
                    c.Remove i
                    outSubscription.Dispose()
                    subUpSubscription.Dispose()
                    subDownSubscription.Dispose()
                    update()
                c.Add(piglet.view (f (Operations(delete, subMoveUp, subMoveDown))))
            match out.Latest with
            | Failure _ -> ()
            | Success xs -> Array.iter add xs
            addTrigger.Subscribe(function
                | Failure _ -> ()
                | Success () -> add init)
            |> ignore
            c.Container

        member this.Add = addTrigger :> Writer<unit>

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
    let ManyInit (inits: 'a[]) (init: 'a) (p: 'a -> Piglet<'a, 'v -> 'w>) : Piglet<'a[], (Many.Stream<'a, 'v, 'w> -> 'x) -> 'x> =
        let s = Stream(Success inits)
        let m = Many.Stream<'a, 'v, 'w>(p, s, init)
        {
            stream = s
            view = fun f -> f m
        }

    [<JavaScript>]
    let Many init p =
        ManyInit [|init|] init p

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
        f.stream.Subscribe(out.Trigger << m) |> ignore
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
        |> ignore
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
    let RunResult action p =
        p.stream.Subscribe action
        |> ignore
        p

    [<JavaScript>]
    let Run action p =
        RunResult (Result.Iter action) p

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
        let Is' pred msg f =
            let s' = Stream(f.stream.Latest, f.stream.Id)
            f.stream.Subscribe(function
                | Failure m -> s'.Trigger (Failure m)
                | Success x when pred x -> s'.Trigger (Success x)
                | _ -> s'.Trigger (Failure [msg]))
            |> ignore
            { f with
                stream = s'
            }

        [<JavaScript>]
        let Is pred msg f =
            let s' = Stream(f.stream.Latest, f.stream.Id)
            f.stream.Subscribe(function
                | Failure m -> s'.Trigger (Failure m)
                | Success x when pred x -> s'.Trigger (Success x)
                | _ -> s'.Trigger (Failure [ErrorMessage(msg, s'.Id)]))
            |> ignore
            { f with
                stream = s'
            }

        [<JavaScript>]
        let NotEmpty x = x <> ""

        [<JavaScript>]
        let Match re x = (RegExp re).Test x
