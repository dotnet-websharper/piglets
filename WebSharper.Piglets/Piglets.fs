// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2018 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace WebSharper.Piglets

open System
open WebSharper
open WebSharper.JavaScript

module Id =

    [<JavaScript>]
    let next =
        let current = ref 0
        fun () ->
            // must increment first: id 0 is reserved.
            incr current
            !current

type ErrorSourceId = int

[<Sealed; JavaScript>]
type ErrorMessage (message, source) =
    member this.Message : string = message
    member this.Source : ErrorSourceId = source

[<JavaScript>]
type Result<'a> =
    | Success of 'a
    | Failure of ErrorMessage list

    member this.isSuccess =
        match this with
        | Success _ -> true
        | Failure _ -> false

    static member Failwith msg : Result<'a> = Failure [ErrorMessage(msg, 0)]

    static member Ap(r1: Result<'a -> 'b>, r2: Result<'a>) =
        match r1, r2 with
        | Success f, Success x -> Success(f x)
        | Failure m, Success _
        | Success _, Failure m -> Failure m
        | Failure m1, Failure m2 -> Failure(m1 @ m2)

    static member Join (r: Result<Result<'a>>) =
        match r with
        | Failure m
        | Success (Failure m) -> Failure m
        | Success (Success x) -> Success x

    static member Map (f: 'a -> 'b) ra =
        match ra with
        | Success x -> Success (f x)
        | Failure m -> Failure m

    static member Map2 (f: 'a -> 'b -> 'c) ra rb =
        match ra, rb with
        | Success a, Success b -> Success (f a b)
        | Failure ma, Failure mb -> Failure (ma @ mb)
        | Failure m, _ | _, Failure m -> Failure m

    static member Iter (f: 'a -> unit) = function
        | Success x -> f x
        | Failure _ -> ()

    static member Bind (f: 'a -> Result<'b>) = function
        | Success x -> f x
        | Failure m -> Failure m

[<AbstractClass; JavaScript>]
type Reader<'a> (id) =
    abstract member Latest : Result<'a>
    abstract member Subscribe : (Result<'a> -> unit) -> IDisposable

    member this.SubscribeImmediate f =
        this.Subscribe f

    member this.Id = id

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

    static member MapResult (f: Result<'b> -> Result<'a>) (r: Reader<'b>) : Reader<'a> =
        let out = Stream<'a>(f r.Latest)
        r.Subscribe(out.Trigger << f) |> ignore
        out :> Reader<'a>

    static member MapResult2 (f: Result<'b> -> Result<'c> -> Result<'a>) (rb: Reader<'b>) (rc: Reader<'c>) : Reader<'a> =
        let out = Stream<'a>(f rb.Latest rc.Latest)
        rb.Subscribe(fun b -> out.Trigger(f b rc.Latest)) |> ignore
        rc.Subscribe(fun c -> out.Trigger(f rb.Latest c)) |> ignore
        out :> Reader<'a>

    static member Map (f: 'b -> 'a) (r: Reader<'b>) : Reader<'a> =
        Reader.MapResult (Result<_>.Map f) r

    static member Map2 (f: 'b -> 'c -> 'a) (rb: Reader<'b>) (rc: Reader<'c>) : Reader<'a> =
        Reader.MapResult2 (fun b c -> Result<_>.Map2 f b c) rb rc

    static member MapToResult (f: 'b -> Result<'a>) (r: Reader<'b>) : Reader<'a> =
        Reader.MapResult (Result<_>.Bind f) r

and [<Interface; JavaScript>] Writer<'a> =
    abstract member Trigger : Result<'a> -> unit

and [<Sealed; JavaScript>] Stream<'a> (s: IntelliFactory.Reactive.HotStream<Result<'a>>, ?id) =
    inherit Reader<'a>(match id with Some id -> id | None -> Id.next())

    new(init, ?id) =
        Stream<_>(IntelliFactory.Reactive.HotStream.New init, ?id = id)

    override this.Latest =
        (!s.Latest).Value

    override this.Subscribe f =
        s.Subscribe f

    member this.Trigger x =
        s.Trigger x

    interface Writer<'a> with
        member this.Trigger x = this.Trigger x

[<JavaScript>]
type Disposable(dispose) =
    interface IDisposable with
        member this.Dispose() = dispose()

[<JavaScript>]
type ConstReader<'a>(x: Result<'a>) =
    inherit Reader<'a>(Id.next())
    override this.Latest = x
    override this.Subscribe f = new Disposable(ignore) :> IDisposable

type Reader<'a> with
    static member Const x = ConstReader(Result<_>.Success x) :> Reader<'a>
    static member ConstResult x = ConstReader(x) :> Reader<'a>

type ErrorMessage with
    static member Create msg (reader: Reader<'a>) =
        new ErrorMessage(msg, reader.Id)

/// I'd rather use an object expression,
/// but they're forbidden inside [<JS>].
[<Sealed; JavaScript>]
type ConcreteWriter<'a> (trigger: Result<'a> -> unit) =

    static member New (trigger: 'a -> unit) =
        ConcreteWriter<'a>(function
            | Success x -> trigger x
            | Failure _ -> ())

    interface Writer<'a> with
        member this.Trigger x = trigger x

type Stream<'a> with
    member this.Write x =
        ConcreteWriter<unit>(function
            | Failure m -> this.Trigger (Failure m)
            | Success () -> this.Trigger (Success x))
        :> Writer<unit>

/// I'd rather use an object expression,
/// but they're forbidden inside [<JS>].
[<Sealed; JavaScript>]
type ConcreteReader<'a> (latest, subscribe) =
    inherit Reader<'a>(Id.next())
    override this.Latest = latest()
    override this.Subscribe f = subscribe f

[<Sealed; JavaScript>]
type Submitter<'a> (input: Reader<'a>, clearError: bool) =
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

    do
        if clearError then
            input.Subscribe (fun inp ->
                match output.Latest with
                | Failure [] -> ()
                | _ -> output.Trigger (Failure []))
            |> ignore

    member this.Input = input

    member this.Output = output

    member this.Trigger() = writer.Trigger(Success())

    interface Writer<unit> with
        member this.Trigger(x) = writer.Trigger(x)

    override this.Latest = output.Latest
    override this.Subscribe f = output.Subscribe f

[<JavaScript>]
module Stream =

    let Ap (sf: Stream<'a -> 'b>) (sx: Stream<'a>) : Stream<'b> =
        let out = Stream(Result<_>.Ap(sf.Latest, sx.Latest))
        sf.Subscribe(fun f -> out.Trigger(Result<_>.Ap(f, sx.Latest))) |> ignore
        sx.Subscribe(fun x -> out.Trigger(Result<_>.Ap(sf.Latest, x))) |> ignore
        out

    let ApJoin (sf: Stream<'a -> 'b>) (sx: Stream<Result<'a>>) : Stream<'b> =
        let out = Stream(Result<_>.Ap(sf.Latest, Result<_>.Join sx.Latest))
        sf.Subscribe(fun f -> out.Trigger(Result<_>.Ap(f, Result<_>.Join sx.Latest))) |> ignore
        sx.Subscribe(fun x -> out.Trigger(Result<_>.Ap(sf.Latest, Result<_>.Join x))) |> ignore
        out

    let Map (a2b: 'a -> 'b) (b2a: 'b -> 'a) (s: Stream<'a>) : Stream<'b> =
        let s' = Stream<'b> (Result<_>.Map a2b s.Latest, id = s.Id)
        let pa = ref s.Latest
        let pb = ref s'.Latest
        s.Subscribe (fun a ->
            if !pa !==. a then
                pb := Result<_>.Map a2b a
                s'.Trigger !pb) |> ignore
        s'.Subscribe (fun b ->
            if !pb !==. b then
                pa := Result<_>.Map b2a b
                s.Trigger !pa) |> ignore
        s'

[<JavaScript>]
type Piglet<'a, 'v> =
    {
        stream: Stream<'a>
        view: 'v
    }

    member this.Stream = this.stream

[<JavaScript>]
module Validation =

    let Is' pred msg p =
        let s' = Stream(p.stream.Latest, p.stream.Id)
        p.stream.Subscribe(function
            | Failure m -> s'.Trigger (Failure m)
            | Success x when pred x -> s'.Trigger (Success x)
            | _ -> s'.Trigger (Failure [msg]))
        |> ignore
        { p with
            stream = s'
        }

    let Is pred msg p =
        let s' = Stream(p.stream.Latest, p.stream.Id)
        p.stream.Subscribe(function
            | Failure m -> s'.Trigger (Failure m)
            | Success x when pred x -> s'.Trigger (Success x)
            | _ -> s'.Trigger (Failure [ErrorMessage(msg, s'.Id)]))
        |> ignore
        { p with
            stream = s'
        }

    let NotEmpty x = x <> ""

    let Match (re: string) = RegExp(re).Test : string -> bool

    let IsNotEmpty msg p = Is NotEmpty msg p

    let IsMatch re msg p = Is (Match re) msg p

[<AutoOpen; JavaScript>]
module Pervasives =

    type Writer<'a> with
        static member Wrap (f: 'b -> 'a) (r: Writer<'a>) =
            new ConcreteWriter<'b>(fun a -> r.Trigger(Result<_>.Map f a)) :> Writer<'b>

        static member WrapToResult (f: 'b -> Result<'a>) (r: Writer<'a>) =
            new ConcreteWriter<'b>(fun a -> r.Trigger(Result<_>.Bind f a)) :> Writer<'b>

        static member WrapResult (f: Result<'b> -> Result<'a>) (r: Writer<'a>) =
            new ConcreteWriter<'b>(fun a -> r.Trigger(f a)) :> Writer<'b>

        static member WrapAsyncResult (f: Result<'b> -> Async<Result<'a>>) (r: Writer<'a>) =
            new ConcreteWriter<'b>(fun ra ->
                async {
                    let! mapped = f ra
                    r.Trigger mapped
                } |> Async.Start) :> Writer<'b>

        static member WrapToAsyncResult (f: 'b -> Async<Result<'a>>) (r: Writer<'a>) =
            r |> Writer.WrapAsyncResult (fun b ->
                async {
                    match b with
                    | Success sb -> return! f sb
                    | Failure f  -> return Failure f
                })

        static member WrapAsync (f: 'b -> Async<'a>) (r: Writer<'a>) =
            r |> Writer.WrapToAsyncResult (fun b -> 
                async {
                    let! mapped = f b
                    return Success mapped
                })

    /// Push an argument to the view function.
    [<Inline>]
    let (<<^) v a = fun x -> v x a

    /// Map argument(s) to the view function.
    [<Inline>]
    let (>>^) v f = fun g -> g (v f)

    let (<*>) f x =
        {
            stream = Stream.Ap f.stream x.stream
            view = f.view >> x.view
        }

    let (<*?>) f x =
        {
            stream = Stream.ApJoin f.stream x.stream
            view = f.view >> x.view
        }

[<JavaScript>]
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

    type Stream<'a, 'v, 'w, 'y,'z>(p : 'a -> Piglet<'a, 'v -> 'w>, out: Stream<'a[]>,adder : Piglet<'a,'y -> 'z>) =

        inherit Reader<'a[]>(out.Id)

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
            |> Result<_>.Map (List.rev >> Array.ofList)
            |> out.Trigger


        override this.Subscribe f = out.Subscribe f

        override this.Latest = out.Latest

        member this.Render (c: Container<'w, 'u>) (f : Operations -> 'v) : 'u =
            let add x =
                let piglet = p x
                streams.Add piglet.stream
                piglet.stream.Subscribe (fun _ -> update()) |> ignore
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
                let subMoveUp = Submitter(inMoveUp, clearError = false)
                let subMoveDown = Submitter(inMoveDown, clearError = false)
                let subUpSubscription =
                    subMoveUp.Subscribe(Result<_>.Iter moveUp)
                let subDownSubscription =
                    subMoveDown.Subscribe(Result<_>.Iter moveDown)
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
            adder.stream.Subscribe(function
                | Failure _ -> ()
                | Success init -> add init)
            |> ignore
            c.Container

        member this.Add = adder.stream :> Writer<'a>

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

        let pStream = Stream<_>(Failure [])

        let choiceSubscriptions = Dictionary()

        let subscriptions =
            ref [
                chooser.stream.Subscribe (fun res ->
                    res
                    |> Result<_>.Map (fun i ->
                        i,
                        if choiceSubscriptions.ContainsKey i then
                            fst choiceSubscriptions.[i]
                        else
                            let p = choice i
                            choiceSubscriptions.[i] <-
                                (p, p.stream.Subscribe out.Trigger)
                            p)
                    |> pStream.Trigger)
            ]

        override this.Latest = out.Latest
        override this.Subscribe f = out.Subscribe f

        member this.Chooser (f: 'u) : 'v =
            chooser.view f

        member this.ChooserStream = chooser.stream

        member this.Choice (c: Container<'x, 'y>) (f: 'w) : 'y =
            let renders = Dictionary()
            let hasChild = ref false
            subscriptions :=
                pStream.Subscribe (fun res ->
                    match res with
                    | Failure _ -> ()
                    | Success (i, p) ->
                        let render =
                            if renders.ContainsKey i then
                                renders.[i]
                            else
                                p.view f
                        out.Trigger p.stream.Latest
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

[<JavaScript>]
module Piglet =

    [<Inline>]
    let Create s v =
        {
            stream = s
            view = v
        }

    let Yield (x: 'a) =
        let s = Stream(Success x)
        {
            stream = s
            view = fun f -> f s
        }

    let YieldFailure () =
        let s = Stream<'a>(Failure [])
        {
            stream = s
            view = fun f -> f s
        }

    let Return (x: 'a) =
        {
            stream = Stream(Success x)
            view = id
        }

    let ReturnFailure () =
        {
            stream = Stream(Failure [])
            view = id
        }

    let WithSubmit pin =
        let submitter = Submitter(pin.stream, clearError = false)
        {
            stream = submitter.Output
            view = pin.view <<^ submitter
        }

    let WithSubmitClearError pin =
        let submitter = Submitter(pin.stream, clearError = true)
        {
            stream = submitter.Output
            view = pin.view <<^ submitter
        }

    let Choose (chooser: Piglet<'i, 'u -> 'v>) (choices: 'i -> Piglet<'o, 'w -> 'x>) =
        let s = Stream(Failure [])
        let c = new Choose.Stream<'o, 'i, 'u, 'v, 'w, 'x>(chooser, choices, s)
        {
            stream = s
            view = fun f -> f c
        }

    let ManyPiglet (inits : 'a[]) (create : Piglet<'a,'y->'z>) (p: 'a -> Piglet<'a, 'v -> 'w>) : Piglet<'a[], (Many.Stream<'a, 'v, 'w,'y,'z> -> 'x) -> 'x> =
        let s = Stream (Success inits)
        let m = Many.Stream<'a,'v,'w,'y,'z>(p, s, create)
        {
            stream = s
            view = fun f -> f m
        }

    let ManyInit (inits: 'a[]) (init: 'a) (p: 'a -> Piglet<'a, 'v -> 'w>) : Piglet<'a[], (Many.UnitStream<'a, 'v, 'w> -> 'x) -> 'x> =
        let s = Stream(Success inits)
        let _init = p init

        let m = Many.UnitStream<'a, 'v, 'w>(p, s,_init,init)
        {
            stream = s
            view = fun f -> f m
        }

    let Many init p =
        ManyInit [|init|] init p

    let TransmitStream p =
        {
            stream = p.stream
            view = p.view <<^ p.stream
        }

    let TransmitReaderMapResult f p =
        {
            stream = p.stream
            view = p.view <<^ Reader.MapResult f p.stream
        }

    let TransmitReaderMapToResult f p =
        {
            stream = p.stream
            view = p.view <<^ Reader.MapToResult f p.stream
        }

    let TransmitReaderMap f p =
        {
            stream = p.stream
            view = p.view <<^ Reader.Map f p.stream
        }

    let TransmitReader p =
        {
            stream = p.stream
            view = p.view <<^ (p.stream :> Reader<_>)
        }

    let TransmitWriter p =
        {
            stream = p.stream
            view = p.view <<^ (p.stream :> Writer<_>)
        }

    let MapResult m p =
        let out = Stream(m p.stream.Latest : Result<_>)
        p.stream.Subscribe(out.Trigger << m) |> ignore
        {
            stream = out
            view = p.view
        }

    let MapToResult m p =
        p |> MapResult (function
            | Failure msg -> Failure msg
            | Success x -> m x)

    let Map m p =
        p |> MapResult (function
            | Failure msg -> Failure msg
            | Success x -> Success (m x))

    let MapAsyncResult m p =
        let out = Stream (Failure [])
        p.stream.Subscribe (fun v ->
            async {
                let! res = m v
                return out.Trigger res
            } |> Async.Start)
        |> ignore
        async {
            let! res = m p.stream.Latest
            return out.Trigger res
        }
        |> Async.Start
        {
            stream = out
            view = p.view
        }

    let MapToAsyncResult m p =
        p |> MapAsyncResult (function
            | Failure msg -> async.Return (Failure msg)
            | Success x -> m x)

    let MapAsync m p =
        p |> MapAsyncResult (function
            | Failure msg -> async.Return (Failure msg)
            | Success x ->
                async {
                    let! res = m x
                    return Success res
                })

    let MapResultWithWriter f (p: Piglet<_, _>) =
        let stream = Stream(Failure [])
        p.stream.Subscribe(f (stream :> Writer<_>)) |> ignore
        {
            stream = stream
            view = p.view
        }

    let MapWithWriter f (p: Piglet<_, _>) =
        let f' (out: Writer<_>) r =
            match r with
            | Failure msgs -> out.Trigger (Failure msgs)
            | Success x -> f out x
        MapResultWithWriter f' p

    let FlushErrors p =
        MapResult (function Failure _ -> Failure [] | x -> x) p

    let RunResult action p =
        p.stream.Subscribe action
        |> ignore
        p

    let Run action p =
        RunResult (Result<_>.Iter action) p

    let Render view p =
        p.view view

    let MapViewArgs view p =
        {
            stream = p.stream
            view = p.view >>^ view
        }

    let YieldOption (x: 'a option) (none: 'a) =
        Yield x
        |> MapViewArgs
            (Stream.Map
                (function None -> none | Some s -> s)
                (fun x -> if x = none then None else Some x))

    let Confirm init validate nomatch =
        let second = Yield init
        Return (fun a b -> a, b)
        <*> validate (Yield init)
        <*> second
        |> Validation.Is' (fun (a, b) -> a = b)
            (ErrorMessage.Create nomatch second.Stream)
        |> Map fst
        |> MapViewArgs (fun a b -> (a, b))

    type Builder =
        | Do

        member this.Bind(p, f) = Choose p f

        member this.Return x = Return x

        member this.ReturnFrom (p: Piglet<_, _>) = p

        member this.Yield x = Yield x

        member this.YieldFrom (p: Piglet<_, _>) = p

        member this.Zero() = ReturnFailure()
