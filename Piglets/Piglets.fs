// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2013 IntelliFactory
//
// For open source use, WebSharper is licensed under GNU Affero General Public
// License v3.0 (AGPLv3) with open-source exceptions for most OSS license types
// (see http://websharper.com/licensing). This enables you to develop open
// source WebSharper applications royalty-free, without requiring a license.
// However, for closed source use, you must acquire a developer license.
//
// Please contact IntelliFactory for licensing and support options at
// {licensing|sales @ intellifactory.com}.
//
// $end{copyright}

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

type Stream<'a> with

    [<JavaScript>]
    member this.Write x =
        ConcreteWriter<unit>(function
            | Failure m -> this.Trigger (Failure m)
            | Success () -> this.Trigger (Success x))
        :> Writer<unit>

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

module Stream =

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

    [<JavaScript>]
    let Map (a2b: 'a -> 'b) (b2a: 'b -> 'a) (s: Stream<'a>) : Stream<'b> =
        let s' = Stream<'b> (Result.Map a2b s.Latest, id = s.Id)
        let pa = ref s.Latest
        let pb = ref s'.Latest
        s.Subscribe (fun a ->
            if !pa !==. a then
                pb := Result.Map a2b a
                s'.Trigger !pb) |> ignore
        s'.Subscribe (fun b ->
            if !pb !==. b then
                pa := Result.Map b2a b
                s.Trigger !pa) |> ignore
        s'

type Piglet<'a, 'v> =
    {
        stream: Stream<'a>
        view: 'v
    }

    [<JavaScript>]
    member this.Stream = this.stream

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

    [<JavaScript>]
    let WithSubmit fin =
        let submitter = Submitter(fin.stream)
        {
            stream = submitter.Output
            view = fin.view <<^ submitter
        }

    type Stream<'a, 'v, 'w, 'y,'z>(p : 'a -> Piglet<'a, 'v -> 'w>, out: Stream<'a[]>,adder : Piglet<'a,'y -> 'z>) =

        inherit Reader<'a[]>(out.Id)

        let addStream = Stream<_>(Failure [])

        let streams = ResizeArray<Stream<'a>>()

        let update() =
            Seq.fold (fun acc (cur: Stream<'a>) ->
                match acc, cur.Latest with
                | Success l, Success x -> Success (x :: l)
                | Failure m , Success _
                | Success _, Failure m -> Failure m
                | Failure m1, Failure m2 -> Failure (m2 @ m1))
                (Success [])
                streams
            |> Result.Map (List.rev >> Array.ofList)
            |> out.Trigger

        do
            adder.stream.Subscribe(
                function Success v -> Success v |> addStream.Trigger
                        | Failure _ -> ()
            ) |> ignore


        override this.Subscribe f = out.Subscribe f

        override this.Latest = out.Latest

        member this.Render (c: Container<'w, 'u>) (f : Operations -> 'v) : 'u =
            let add x =
                let piglet = p x
                streams.Add piglet.stream
                piglet.stream.SubscribeImmediate (fun _ -> update()) |> ignore
                let getThisIndex() =
                    streams |> Seq.findIndex (fun x -> x.Id = piglet.stream.Id)
                let moveUp i =
                    if i > 0 && i < streams.Count then
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
                    if getThisIndex() < streams.Count - 1 then Success() else Failure []
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
                    streams.RemoveAt i
                    c.Remove i
                    outSubscription.Dispose()
                    subUpSubscription.Dispose()
                    subDownSubscription.Dispose()
                    update()
                c.Add(piglet.view (f (Operations(delete, subMoveUp, subMoveDown))))
            match out.Latest with
            | Failure _ -> ()
            | Success xs -> Array.iter add xs
            addStream.Subscribe(function
                | Failure _ -> ()
                | Success init -> add init)
            |> ignore
            c.Container

        member this.Add = addStream :> Writer<'a>

        member this.AddRender f = adder.view f

    type UnitStream<'a, 'v, 'w>(p : 'a -> Piglet<'a, 'v -> 'w>, out: Stream<'a[]>, init: Piglet<'a,'v-> 'w>,``default`` : 'a) =

        inherit Stream<'a,'v,'w,'v,'w>(p,out,init)

        let submitStream =
            let submitter = Stream<_>(Failure [])
            let trigger = init.Stream.Trigger

            submitter.Subscribe(
                        function Failure msgs -> Failure msgs |> trigger
                                | Success () -> Success ``default``  |> trigger
                    ) |> ignore

            submitter

        member this.Add = submitStream :> Writer<unit>

[<JavaScript>]
module Choose =

    open System.Collections.Generic

    type Stream<'o, 'i, 'u, 'v, 'w, 'x when 'i : equality>(chooser: Piglet<'i, 'u -> 'v>, choice: 'i -> Piglet<'o, 'w -> 'x>, out: Stream<'o>) =
        inherit Reader<'o>(out.Id)

        let plStream = Stream<_>(Failure [])

        let choiceSubscriptions = Dictionary()

        let subscriptions =
            ref [
                chooser.stream.SubscribeImmediate (fun res ->
                    res
                    |> Result.Map (fun i ->
                        i,
                        if choiceSubscriptions.ContainsKey i then
                            fst choiceSubscriptions.[i]
                        else
                            let pl = choice i
                            choiceSubscriptions.[i] <-
                                (pl, pl.stream.Subscribe out.Trigger)
                            pl)
                    |> plStream.Trigger)
            ]

        override this.Latest = out.Latest
        override this.Subscribe f = out.Subscribe f

        member this.Chooser (f: 'u) : 'v =
            chooser.view f

        member this.Choice (c: Container<'x, 'y>) (f: 'w) : 'y =
            let renders = Dictionary()
            let hasChild = ref false
            subscriptions :=
                plStream.SubscribeImmediate (fun res ->
                    match res with
                    | Failure _ -> ()
                    | Success (i, pl) ->
                        let render =
                            if renders.ContainsKey i then
                                renders.[i]
                            else
                                pl.view f
                        out.Trigger pl.stream.Latest
                        if !hasChild then c.Remove 0
                        hasChild := true
                        c.Add render)
                :: !subscriptions
            c.Container

        interface IDisposable with
            member this.Dispose() =
                for s in !subscriptions do
                    s.Dispose()
                choiceSubscriptions |> Seq.iter (fun (KeyValue (_, (_, s))) ->
                    s.Dispose())


module Piglet =

    [<JavaScript>]
    let Yield (x: 'a) =
        let s = Stream(Success x)
        {
            stream = s
            view = fun f -> f s
        }

    [<JavaScript>]
    let YieldFailure () =
        let s = Stream<'a>(Failure [])
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
    let ReturnFailure () =
        {
            stream = Stream(Failure [])
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
    let Choose (chooser: Piglet<'i, 'u -> 'v>) (choices: 'i -> Piglet<'o, 'w -> 'x>) =
        let s = Stream(Failure [])
        let c = new Choose.Stream<'o, 'i, 'u, 'v, 'w, 'x>(chooser, choices, s)
        {
            stream = s
            view = fun f -> f c
        }

    [<JavaScript>]
    let ManyPiglet (inits : 'a[]) (create : Piglet<'a,'y->'z>) (p: 'a -> Piglet<'a, 'v -> 'w>) : Piglet<'a[], (Many.Stream<'a, 'v, 'w,'y,'z> -> 'x) -> 'x> =
        let s = Stream (Success inits)
        let m = Many.Stream<'a,'v,'w,'y,'z>(p,s,create)
        {
            stream = s
            view = fun f -> f m
        }

    [<JavaScript>]
    let ManyInit (inits: 'a[]) (init: 'a) (p: 'a -> Piglet<'a, 'v -> 'w>) : Piglet<'a[], (Many.UnitStream<'a, 'v, 'w> -> 'x) -> 'x> =
        let s = Stream(Success inits)
        let _init = p init

        let m = Many.UnitStream<'a, 'v, 'w>(p, s,_init,init)
        {
            stream = s
            view = fun f -> f m
        }

    [<JavaScript>]
    let Many init p =
        ManyInit [|init|] init p

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
    let FlushErrors p =
        MapResult (function Failure _ -> Failure [] | x -> x) p

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

    [<JavaScript>]
    let YieldOption (x: 'a option) (none: 'a) =
        Yield x
        |> MapViewArgs
            (Stream.Map
                (function None -> none | Some s -> s)
                (fun x -> if x = none then None else Some x))

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
        let Match re = (RegExp re).Test

    [<JavaScript>]
    let Confirm init validate nomatch =
        let second = Yield init
        Return (fun a b -> a, b)
        <*> validate (Yield init)
        <*> second
        |> Validation.Is' (fun (a, b) -> a = b)
            (ErrorMessage.Create nomatch second.Stream)
        |> Map fst
        |> MapViewArgs (fun a b -> (a, b))

    [<JavaScript>]
    type Builder =
        | Do

        member this.Bind(pl, f) = Choose pl f

        member this.Return x = Return x

        member this.ReturnFrom (pl: Piglet<_, _>) = pl

        member this.Yield x = Yield x

        member this.YieldFrom (pl: Piglet<_, _>) = pl

        member this.Zero() = ReturnFailure()
