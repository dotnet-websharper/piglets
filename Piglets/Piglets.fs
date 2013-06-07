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

    [<JavaScript>]
    let nextId =
        let current = ref 0
        fun () ->
            incr current
            "pl__" + string !current

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

    module Controls =

        open IntelliFactory.WebSharper.Html

        [<JavaScript>]
        let input ``type`` ofString toString (stream: Stream<'a>) (label: string) =
            let id = nextId()
            let i = Default.Input [Attr.Type ``type``; Attr.Id id]
            match stream.Latest with
            | Failure _ -> ()
            | Success x -> i.Value <- toString x
            stream.Subscribe(function
                | Success x ->
                    let s = toString x
                    if i.Value <> s then i.Value <- s
                | Failure _ -> ())
            let ev (_: Dom.Event) = stream.Trigger(Success (ofString i.Value))
            i.Body.AddEventListener("keyup", ev, true)
            i.Body.AddEventListener("change", ev, true)
            Span [Label [Attr.For id; Text label]; i]

        [<JavaScript>]
        [<Inline>]
        let Input stream label =
            input "text" id id stream label

        [<JavaScript>]
        [<Inline>]
        let Password stream label =
            input "password" id id stream label
  
        [<JavaScript>]
        let IntInput (stream: Stream<int>) (label: string) =
            input "number" int string stream label
      
        [<JavaScript>]
        let TextArea (stream: Stream<string>) (label: string) =
            let id = nextId()
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
            Span [Label [Attr.For id; Text label]; i]

        [<JavaScript>]
        let CheckBox (stream: Stream<bool>) (label: string) =
            let id = nextId()
            let i = Default.Input [Attr.Type "checkbox"; Attr.Id id]
            match stream.Latest with
            | Failure _ -> ()
            | Success x -> i.Body?``checked`` <- x
            stream.Subscribe(function
                | Success x ->
                    if i.Body?``checked`` <> x then i.Body?``checked`` <- x
                | Failure _ -> ())
            let ev (_: Dom.Event) = stream.Trigger(Success i.Body?``checked``)
            i.Body.AddEventListener("change", ev, true)
            Span [i; Label [Attr.For id; Text label]]

        [<JavaScript>]
        let Radio (stream: Stream<'a>) (values: seq<'a * string>) =
            let name = nextId()
            let values = List.ofSeq values
            let elts = values |> List.map (fun (x, label) ->
                let id = nextId()
                let input =
                    Html.Default.Input [Attr.Type "radio"; Attr.Name name; Attr.Id id]
                    |>! OnChange (fun div ->
                        if div.Body?``checked`` then
                            stream.Trigger(Success x))
                input, Span [
                    input
                    Label [Attr.For id; Text label]
                ])
            Div (Seq.map snd elts)
            |>! OnAfterRender (fun div ->
                let set = function
                    | Success v ->
                        (values, elts) ||> List.iter2 (fun (x, _) (input, _) ->
                            input.Body?``checked`` <- x = v)
                    | Failure _ -> ()
                set stream.Latest
                stream.Subscribe set)

        [<JavaScript>]
        let ShowResult
                (reader: Reader<'a>)
                (render: Result<'a> -> #seq<#IPagelet>)
                (container: Element) =
            for e in render reader.Latest do
                container.Append (e :> IPagelet)
            reader.Subscribe(fun x ->
                container.Clear()
                for e in render x do
                    container.Append(e :> IPagelet))
            container

        [<JavaScript>]
        let Show
                (reader: Reader<'a>)
                (render: 'a -> #seq<#IPagelet>)
                (container: Element) =
            let render = function
                | Success x -> render x :> seq<_>
                | Failure _ -> Seq.empty
            ShowResult reader render container

        [<JavaScript>]
        let ShowString reader render container =
            Show reader (fun x -> [Text (render x)]) container

        [<JavaScript>]
        let ShowErrors
                (reader: Reader<'a>)
                (render: string list -> #seq<#IPagelet>)
                (container: Element) =
            let render = function
                | Success (x: 'a) -> Seq.empty
                | Failure m -> render m :> seq<_>
            ShowResult reader render container

        [<JavaScript>]
        let Submit (submit: Writer<_>) =
            Default.Input [Attr.Type "submit"]
            |>! OnClick (fun _ _ -> (submit :> Writer<unit>).Trigger(Success()))

        [<JavaScript>]
        let EnableOnSuccess (reader: Reader<'a>) (element: Element) =
            element
            |>! OnAfterRender (fun el ->
                el.Body?disabled <- not reader.Latest.isSuccess
                reader.Subscribe(fun x -> el.Body?disabled <- not x.isSuccess))

        [<JavaScript>]
        let Button (submit: Writer<unit>) =
            Default.Input [Attr.Type "button"]
            |>! OnClick (fun _ _ -> submit.Trigger(Success()))

        [<JavaScript>]
        let Attr
                (reader: Reader<'a>)
                (attrName: string)
                (render: 'a -> string)
                (element: Element) =
            element
            |>! OnAfterRender (fun element ->
                let set x =
                    match x with
                    | Failure _ -> ()
                    | Success x -> element.SetAttribute(attrName, render x)
                set reader.Latest
                reader.Subscribe set)

        [<JavaScript>]
        let Css
                (reader: Reader<'a>)
                (attrName: string)
                (render: 'a -> string)
                (element: Element) =
            element
            |>! OnAfterRender (fun element ->
                let set x =
                    match x with
                    | Failure _ -> ()
                    | Success x -> element.SetCss(attrName, render x)
                set reader.Latest
                reader.Subscribe set)
