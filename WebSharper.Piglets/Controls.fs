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

module WebSharper.Piglets.Controls

open WebSharper
open WebSharper.JavaScript
open WebSharper.Html.Client

[<JavaScript>]
let nextId =
    let current = ref 0
    fun () ->
        incr current
        "pl__" + string !current

[<JavaScript>]
let input ``type`` ofString toString (stream: Stream<'a>) =
    let i = Tags.Input [Type ``type``]
    match stream.Latest with
    | Failure _ -> ()
    | Success x -> i.Value <- toString x
    stream.Subscribe(function
        | Success x ->
            let s = toString x
            if i.Value <> s then i.Value <- s
        | Failure _ -> ())
    |> ignore
    let ev (_: Dom.Event) =
        let v = Success (ofString i.Value)
        if v <> stream.Latest then stream.Trigger v
    i.Body.AddEventListener("keyup", ev, true)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let WithLabel label element =
    let id = nextId()
    Span [Label [For id; Text label]; element -< [Id id]]

[<JavaScript>]
let WithLabelAfter label element =
    let id = nextId()
    Span [element -< [Id id]; Label [For id; Text label]]

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
    let i = Tags.TextArea []
    match stream.Latest with
    | Failure _ -> ()
    | Success x -> i.Value <- x
    stream.Subscribe(function
        | Success x ->
            if i.Value <> x then i.Value <- x
        | Failure _ -> ())
    |> ignore
    let ev (_: Dom.Event) = stream.Trigger(Success i.Value)
    i.Body.AddEventListener("keyup", ev, true)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let CheckBox (stream: Stream<bool>) =
    let id = nextId()
    let i = Tags.Input [Type "checkbox"; Id id]
    match stream.Latest with
    | Failure _ -> ()
    | Success x -> i.Body?``checked`` <- x
    stream.Subscribe(function
        | Success x ->
            if i.Body?``checked`` <> x then i.Body?``checked`` <- x
        | Failure _ -> ())
    |> ignore
    let ev (_: Dom.Event) = stream.Trigger(Success i.Body?``checked``)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let Radio (stream: Stream<'a>) (values: seq<'a>) =
    let name = nextId()
    let values = List.ofSeq values
    let elts =
        values
        |> List.map (fun x ->
            Tags.Input [Type "radio"; Name name]
            |>! OnChange (fun div ->
                if div.Body?``checked`` then
                    stream.Trigger(Success x)))
    let set = function
        | Success v ->
            (values, elts) ||> List.iter2 (fun x input ->
                input.Body?``checked`` <- x = v)
        | Failure _ -> ()
    set stream.Latest
    stream.Subscribe set |> ignore
    Seq.ofList elts

[<JavaScript>]
let RadioLabelled (stream: Stream<'a>) (values: seq<'a * string>) =
    (values, Radio stream (Seq.map fst values))
    ||> Seq.map2 (fun (_, label) input ->
        let id = nextId()
        Span [
            input -< [Id id]
            Label [For id; Text label]
        ])
    |> Div

[<JavaScript>]
let Select (stream: Stream<'a>) (values: seq<'a * string>) =
    let name = nextId()
    let values = Array.ofSeq values
    let elts = values |> Array.map (fun (x, label) ->
        let id = nextId()
        Tags.Option [Value id] -< [Text label])
    Select elts
    |>! OnChange (fun e ->
        if e.Body?selectedIndex >= 0 then
            stream.Trigger(Success (fst values.[e.Body?selectedIndex])))
    |>! OnAfterRender (fun div ->
        stream.Subscribe (function
            | Success v ->
                match Array.tryFindIndex (fun (v', _) -> v = v') values with
                | Some i -> elts.[i].SetAttribute("selected", "")
                | None -> ()
            | Failure _ -> ())
        |> ignore)

[<JavaScript>]
type HtmlContainer (container: Element) =
    interface Container<Element, Element> with

        member this.Add elt =
            container.Append elt

        member this.Remove i =
            container.Body.RemoveChild container.Body.ChildNodes.[i]
            |> ignore

        member this.MoveUp i =
            let elt_i = container.Body.ChildNodes.[i]
            let elt_i_1 = container.Body.ChildNodes.[i-1]
            container.Body.RemoveChild elt_i |> ignore
            container.Body.InsertBefore(elt_i, elt_i_1) |> ignore

        member this.Container = container

[<JavaScript>]
let RenderMany (many: Many.Stream<_,_,_,_,_>) renderOne container =
    many.Render (HtmlContainer container) renderOne

[<JavaScript>]
let RenderChoice (choice: Choose.Stream<_,_,_,_,_,_>) renderIt container =
    choice.Choice (HtmlContainer container) renderIt

[<JavaScript>]
let Container c = HtmlContainer(c) :> Container<_,_>

[<JavaScript>]
let ShowResult
        (reader: Reader<'a>)
        (render: Result<'a> -> #seq<#Pagelet>)
        (container: Element) =
    for e in render reader.Latest do
        container.Append (e :> Pagelet)
    reader.Subscribe(fun x ->
        container.Clear()
        for e in render x do
            container.Append(e :> Pagelet))
    |> ignore
    container

[<JavaScript>]
let Show
        (reader: Reader<'a>)
        (render: 'a -> #seq<#Pagelet>)
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
        (render: string list -> #seq<#Pagelet>)
        (container: Element) =
    let render = function
        | Success (x: 'a) -> Seq.empty
        | Failure m -> render (m |> List.map (fun m -> m.Message)) :> seq<_>
    ShowResult reader render container

[<JavaScript>]
let EnableOnSuccess (reader: Reader<'a>) (element: Element) =
    element
    |>! OnAfterRender (fun el ->
        el.Body?disabled <- not reader.Latest.isSuccess
        reader.Subscribe(fun x -> el.Body?disabled <- not x.isSuccess)
        |> ignore)

[<JavaScript>]
let Submit (submit: Writer<unit>) =
    Tags.Input [Type "submit"]
    |>! OnClick (fun _ _ -> submit.Trigger(Success()))

[<JavaScript>]
let SubmitValidate (submit: Submitter<'a>) =
    Submit submit |> EnableOnSuccess submit.Input

[<JavaScript>]
let Button (submit: Writer<unit>) =
    Button []
    |>! OnClick (fun _ _ -> submit.Trigger(Success()))

[<JavaScript>]
let ButtonValidate (submit: Submitter<'a>) =
    Button submit |> EnableOnSuccess submit.Input

[<JavaScript>]
let Link (submit: Writer<unit>) =
    A [HRef "#"]
    |>! OnAfterRender (fun e ->
        JQuery.JQuery.Of(e.Body).On("click", fun el ev ->
            submit.Trigger(Success())
            ev.PreventDefault()
        ).Ignore)

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
        reader.Subscribe set |> ignore)

[<JavaScript>]
let AttrResult
        (reader: Reader<'a>)
        (attrName: string)
        (render: Result<'a> -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        let set x = element.SetAttribute(attrName, render x)
        set reader.Latest
        reader.Subscribe set |> ignore)

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
        reader.Subscribe set |> ignore)

[<JavaScript>]
let CssResult
        (reader: Reader<'a>)
        (attrName: string)
        (render: Result<'a> -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        let set x = element.SetCss(attrName, render x)
        set reader.Latest
        reader.Subscribe set |> ignore)
