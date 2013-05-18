module IntelliFactory.WebSharper.Piglets

type Result<'a> =
    | Success of 'a
    | Failure of string list

    [<JavaScript>]
    member this.isSuccess =
        match this with
        | Success _ -> true
        | Failure _ -> false

    [<JavaScript>]
    static member Ap(r1, r2) =
        match r1, r2 with
        | Success f, Success x -> Success(f x)
        | Failure m, Success _
        | Success _, Failure m -> Failure m
        | Failure m1, Failure m2 -> Failure(m1 @ m2)

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

[<Sealed>]
type Submitter<'a> [<JavaScript>] (toSubmit: Reader<'a>, submit: unit -> unit) =
    [<JavaScript>]
    member this.ToSubmit = toSubmit
    [<JavaScript>]
    member this.Submit() = submit()

module private Stream =

    [<JavaScript>]
    let Ap (sf: Stream<'a -> 'b>) (sx: Stream<'a>) : Stream<'b> =
        let out = Stream(Result<_>.Ap(sf.Latest, sx.Latest))
        sf.Subscribe(fun f -> out.Trigger(Result<_>.Ap(f, sx.Latest)))
        sx.Subscribe(fun x -> out.Trigger(Result<_>.Ap(sf.Latest, x)))
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

module Piglet =

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
        let fout = Stream(Failure [])
        let submit() =
            fout.Trigger fin.stream.Latest
        let canSubmit =
            ConcreteReader(
                (fun () -> fin.stream.Latest),
                fin.stream.Subscribe)
        {
            stream = fout
            view = fin.view <<^ Submitter(canSubmit, submit)
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

    module Controls =

        open IntelliFactory.WebSharper.Html

        [<JavaScript>]
        let Input (stream: Stream<string>) =
            let i = Default.Input [Attr.Type "text"]
            match stream.Latest with
            | Failure _ -> ()
            | Success x -> i.Value <- x
            stream.Subscribe(function
                | Success x ->
                    if i.Value <> x then i.Value <- x
                | Failure _ -> ())
            let ev (_: Dom.Event) = stream.Trigger(Success i.Value)
            i.Body.AddEventListener("keyup", ev, true)
            i.Body.AddEventListener("change", ev, true)
            i
        
        [<JavaScript>]
        let TextArea (stream: Stream<string>) =
            let i = Default.TextArea []
            match stream.Latest with
            | Failure _ -> ()
            | Success x -> i.Value <- x
            stream.Subscribe(function
                | Success x ->
                    if i.Value <> x then i.Value <- x
                | Failure _ -> ())
            let ev (_: Dom.Event) = stream.Trigger(Success i.Value)
            i.Body.AddEventListener("keyup", ev, true)
            i.Body.AddEventListener("change", ev, true)
            i

        [<JavaScript>]
        let IntInput (stream: Stream<int>) =
            let i = Default.Input [Attr.Type "number"]
            match stream.Latest with
            | Failure _ -> ()
            | Success x -> i.Value <- string x
            stream.Subscribe(function
                | Success x ->
                    if int i.Value <> x then i.Value <- string x
                | Failure _ -> ())
            let ev (_: Dom.Event) = stream.Trigger(Success(int i.Value))
            i.Body.AddEventListener("keyup", ev, true)
            i.Body.AddEventListener("change", ev, true)
            i

        [<JavaScript>]
        let CheckBox (stream: Stream<bool>) =
            let i = Default.Input [Attr.Type "checkbox"]
            match stream.Latest with
            | Failure _ -> ()
            | Success x -> i.Body?``checked`` <- x
            stream.Subscribe(function
                | Success x ->
                    if i.Body?``checked`` <> x then i.Body?``checked`` <- x
                | Failure _ -> ())
            let ev (_: Dom.Event) = stream.Trigger(Success i.Body?``checked``)
            i.Body.AddEventListener("change", ev, true)
            i

        [<JavaScript>]
        let Show
                (reader: Reader<'a>)
                (container: seq<IPagelet> -> Element)
                (render: Result<'a> -> #seq<IPagelet>) =
            let c = container (render reader.Latest)
            reader.Subscribe(fun x ->
                c.Clear()
                for e in render x do
                    c.Append e)
            c

        [<JavaScript>]
        let Submit (s: Submitter<'a>) =
            Default.Input [Attr.Type "submit"]
            |>! OnClick (fun _ _ -> s.Submit())
            |>! OnAfterRender (fun el ->
                el.Body?disabled <- not s.ToSubmit.Latest.isSuccess
                s.ToSubmit.Subscribe(fun x -> el.Body?disabled <- not x.isSuccess))

        [<JavaScript>]
        let Button (submit: Writer<unit>) =
            Default.Input [Attr.Type "button"]
            |>! OnClick (fun _ _ -> submit.Trigger(Success()))
