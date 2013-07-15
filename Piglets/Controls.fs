module IntelliFactory.WebSharper.Piglets.Controls

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.Html

[<JavaScript>]
let nextId =
    let current = ref 0
    fun () ->
        incr current
        "plc__" + string !current

[<JavaScript>]
let input ``type`` ofString toString (stream: Stream<'a>) =
    let i = Default.Input [Attr.Type ``type``]
    stream.SubscribeImmediate (fun v ->
        match Result.Value v with
        | Some x ->
            let s = toString x
            if i.Value <> s then i.Value <- s
        | None -> ())
    let ev (_: Dom.Event) = stream.Trigger(Result.Success (ofString i.Value))
    i.Body.AddEventListener("keyup", ev, true)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let WithLabel label element =
    let id = nextId()
    Span [Label [Attr.For id; Text label]; element -< [Attr.Id id]]

[<JavaScript>]
let WithLabelAfter label element =
    let id = nextId()
    Span [element -< [Attr.Id id]; Label [Attr.For id; Text label]]

[<JavaScript>]
[<Inline>]
let Input stream =
    input "text" id id stream

[<JavaScript>]
[<Inline>]
let Password stream =
    input "password" id id stream

[<JavaScript>]
let IntInput (stream: Stream<int>) =
    input "number" int string stream

[<JavaScript>]
let TextArea (stream: Stream<string>) =
    let i = Default.TextArea []
    stream.SubscribeImmediate(fun v ->
        match Result.Value v with
        | Some x ->
            if i.Value <> x then i.Value <- x
        | None -> ())
    let ev (_: Dom.Event) = stream.Trigger(Result.Success i.Value)
    i.Body.AddEventListener("keyup", ev, true)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let CheckBox (stream: Stream<bool>) =
    let id = nextId()
    let i = Default.Input [Attr.Type "checkbox"; Attr.Id id]
    match Result.Value stream.Latest with
    | None -> ()
    | Some x -> i.Body?``checked`` <- x
    stream.SubscribeImmediate(fun v ->
        match Result.Value v with
        | Some x ->
            if i.Body?``checked`` <> x then i.Body?``checked`` <- x
        | None -> ())
    let ev (_: Dom.Event) = stream.Trigger(Result.Success i.Body?``checked``)
    i.Body.AddEventListener("change", ev, true)
    i

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
                    stream.Trigger(Result.Success x))
        input, Span [
            input
            Label [Attr.For id; Text label]
        ])
    Div (Seq.map snd elts)
    |>! OnAfterRender (fun div ->
        stream.SubscribeImmediate (fun v ->
            match Result.Value v with
            | Some v ->
                (values, elts) ||> List.iter2 (fun (x, _) (input, _) ->
                    input.Body?``checked`` <- x = v)
            | None -> ()))

[<JavaScript>]
let ShowResult
        (reader: Reader<'a>)
        (render: Result<'a> -> #seq<#IPagelet>)
        (container: Element) =
    reader.SubscribeImmediate(fun x ->
        container.Clear()
        for e in render x do
            container.Append(e :> IPagelet))
    container

[<JavaScript>]
let Show
        (reader: Reader<'a>)
        (render: 'a -> #seq<#IPagelet>)
        (container: Element) =
    let render = fun v ->
        match Result.Value v with
        | Some x -> render x :> seq<_>
        | None _ -> Seq.empty
    ShowResult reader render container

[<JavaScript>]
let ShowString reader render container =
    Show reader (fun x -> [Text (render x)]) container

[<JavaScript>]
let ShowErrors
        (reader: Reader<'a>)
        (render: string list -> #seq<#IPagelet>)
        (container: Element) =
    let render = fun v ->
        match Result.SuccessValue v with
        | Some (x: 'a) -> Seq.empty
        | None -> render (Result.Errors v) :> seq<_>
    ShowResult reader render container

[<JavaScript>]
let Submit (submit: Writer<_>) =
    Default.Input [Attr.Type "submit"]
    |>! OnClick (fun _ _ -> (submit :> Writer<unit>).Trigger(Result.Success()))

[<JavaScript>]
let EnableOnSuccess (reader: Reader<'a>) (element: Element) =
    element
    |>! OnAfterRender (fun el ->
        el.Body?disabled <- not (Result.IsSuccess reader.Latest)
        reader.Subscribe(fun x -> el.Body?disabled <- not (Result.IsSuccess x)))

[<JavaScript>]
let Button (submit: Writer<unit>) =
    Default.Input [Attr.Type "button"]
    |>! OnClick (fun _ _ -> submit.Trigger(Result.Success()))

[<JavaScript>]
let Attr
        (reader: Reader<'a>)
        (attrName: string)
        (render: 'a -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        reader.SubscribeImmediate (fun v ->
            match Result.Value v with
            | None -> ()
            | Some x -> element.SetAttribute(attrName, render x)))

[<JavaScript>]
let AttrResult
        (reader: Reader<'a>)
        (attrName: string)
        (render: Result<'a> -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        reader.SubscribeImmediate (fun x ->
            element.SetAttribute(attrName, render x)))

[<JavaScript>]
let Css
        (reader: Reader<'a>)
        (attrName: string)
        (render: 'a -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        reader.SubscribeImmediate (fun v ->
            match Result.Value v with
            | None -> ()
            | Some x -> element.SetCss(attrName, render x)))

[<JavaScript>]
let CssResult
        (reader: Reader<'a>)
        (attrName: string)
        (render: Result<'a> -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        reader.SubscribeImmediate (fun x ->
            element.SetCss(attrName, render x)))
