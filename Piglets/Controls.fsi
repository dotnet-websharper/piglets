// $begin{copyright}
//
// This file is part of WebSharper.Piglets
//
// Copyright (c) 2008-2013 IntelliFactory
//
// GNU Affero General Public License Usage
// WebSharper is free software: you can redistribute it and/or modify it under
// the terms of the GNU Affero General Public License, version 3, as published
// by the Free Software Foundation.
//
// WebSharper is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
// FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License
// for more details at <http://www.gnu.org/licenses/>.
//
// If you are unsure which license is appropriate for your use, please contact
// IntelliFactory at http://intellifactory.com/contact.
//
// $end{copyright}

module IntelliFactory.WebSharper.Piglets.Controls

open IntelliFactory.WebSharper.Html


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

/// A Piglet radio button set.
val Radio<'a when 'a : equality> : Stream<'a> -> seq<'a * string> -> Element

/// Render a multiple-valued stream.
val RenderMany :
    stream: Many.UnitStream<'a, 'v, Element> ->
    renderOne: (Many.Operations -> 'v) ->
    container: Element ->
    Element

/// Display a reactive value.
val ShowResult :
    reader: Reader<'a> ->
    render: (Result<'a> -> #seq<#IPagelet>) ->
    container: Element ->
    Element

/// Display a reactive value, or nothing if it is invalid.
val Show :
    reader: Reader<'a> ->
    render: ('a -> #seq<#IPagelet>) ->
    container: Element ->
    Element

/// Display a reactive value, or nothing if it is invalid.
val ShowString :
    reader: Reader<'a> ->
    render: ('a -> string) ->
    container: Element ->
    Element

/// Display errors, if any.
val ShowErrors :
    reader: Reader<'a> ->
    render: (string list -> #seq<#IPagelet>) ->
    container: Element ->
    Element

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

/// Enables the element when reading Success, disable it when reading Failure.
val EnableOnSuccess : Reader<'a> -> Element -> Element
