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

open WebSharper.Html.Client


/// A label for a given element.
val WithLabel : string -> Element -> Element

/// A label for a given element.
val WithLabelAfter : string -> Element -> Element

/// A Piglet text input.
val Input : Stream<string> -> Element

/// A Piglet password input.
val Password : Stream<string> -> Element

/// A Piglet text area.
val TextArea : Stream<string> -> Element

/// A Piglet text input that accepts integers.
val IntInput : Stream<int> -> Element

/// A Piglet checkbox.
val CheckBox : Stream<bool> -> Element

/// A Piglet radio button set. Returns one <input> element for each given value.
val Radio : Stream<'a> -> seq<'a> -> seq<Element> when 'a : equality

/// A Piglet radio button set with labels for each item, wrapped in a div.
val RadioLabelled : Stream<'a> -> seq<'a * string> -> Element when 'a : equality

/// A Piglet combobox.
val Select : Stream<'a> -> seq<'a * string> -> Element when 'a : equality

/// Render a multiple-valued stream.
val RenderMany :
    stream: Many.Stream<'a, 'v, Element, 'y, 'z> ->
    renderOne: (Many.Operations -> 'v) ->
    container: Element ->
    Element

/// Render a choice stream.
val RenderChoice :
    stream: Choose.Stream<'o, 'i, 'u, 'v, 'w, Element> ->
    renderOne: 'w ->
    container: Element ->
    Element

/// Display a reactive value.
val ShowResult :
    reader: Reader<'a> ->
    render: (Result<'a> -> 's) ->
    container: Element ->
    Element
    when 's :> seq<'p> and 'p :> Pagelet

/// Display a reactive value, or nothing if it is invalid.
val Show :
    reader: Reader<'a> ->
    render: ('a -> 's) ->
    container: Element ->
    Element
    when 's :> seq<'p> and 'p :> Pagelet

/// Display a reactive value, or nothing if it is invalid.
val ShowString :
    reader: Reader<'a> ->
    render: ('a -> string) ->
    container: Element ->
    Element

/// Display errors, if any.
val ShowErrors :
    reader: Reader<'a> ->
    render: (string list -> 's) ->
    container: Element ->
    Element
    when 's :> seq<'p> and 'p :> Pagelet

/// Add an attribute to an element that depends on a reader.
val Attr :
    reader: Reader<'a> ->
    attrName: string ->
    render: ('a -> string) ->
    Element ->
    Element

/// Add an attribute to an element that depends on a reader.
val AttrResult :
    reader: Reader<'a> ->
    attrName: string ->
    render: (Result<'a> -> string) ->
    Element ->
    Element

/// Add a CSS style to an element that depends on a reader.
val Css :
    reader: Reader<'a> ->
    attrName: string ->
    render: ('a -> string) ->
    Element ->
    Element

/// Add a CSS style to an element that depends on a reader.
val CssResult :
    reader: Reader<'a> ->
    attrName: string ->
    render: (Result<'a> -> string) ->
    Element ->
    Element

/// Displays a submit button driven by the given submitter.
val Submit : Writer<unit> -> Element

/// A button that triggers the given callback.
val Button : Writer<unit> -> Element

/// Displays a submit button driven by the given submitter.
/// The button is disabled when no input is available.
val SubmitValidate : Submitter<'a> -> Element

/// A button that triggers the given callback.
/// The button is disabled when no input is available.
val ButtonValidate : Submitter<'a> -> Element

/// An <a> link that triggers the given callback.
val Link : Writer<unit> -> Element

/// Enables the element when reading Success, disable it when reading Failure.
val EnableOnSuccess : Reader<'a> -> Element -> Element
