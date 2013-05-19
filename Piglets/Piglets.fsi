module IntelliFactory.WebSharper.Piglets

type Result<'a> =
    | Success of 'a
    | Failure of string list

    member isSuccess : bool

type Reader<'a> =
    abstract member Latest : Result<'a>
    abstract member Subscribe : (Result<'a> -> unit) -> unit

[<Interface>]
type Writer<'a> =
    abstract member Trigger : Result<'a> -> unit

[<Sealed>]
[<Class>]
type Stream<'a> =
    interface Writer<'a>
    interface Reader<'a>

type Piglet<'a, 'v>

[<AutoOpen>]
module Pervasives =

    /// Apply a Piglet function to a Piglet value.
    val (<*>) : Piglet<'a -> 'b, 'c -> 'd> -> Piglet<'a, 'd -> 'e> -> Piglet<'b, 'c -> 'e>

module Piglet =

    /// Create a Piglet initialized with x that passes its stream to the view.
    val Yield : 'a -> Piglet<'a, (Stream<'a> -> 'b) -> 'b>

    /// Create a Piglet initialized with x that doesn't pass any stream to the view.
    val Return : 'a -> Piglet<'a, 'b -> 'b>

    /// Create a Piglet value that streams the value every time it receives a signal.
    /// The signaling function is passed to the view.
    val WithSubmit : Piglet<'a, 'b -> (unit -> unit) * Reader<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass this Piglet's stream to the view.
    val TransmitStream : Piglet<'a, 'b -> Stream<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a reader for this Piglet's stream to the view.
    val TransmitReader : Piglet<'a, 'b -> Reader<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a writer for this Piglet's stream to the view.
    val TransmitWriter : Piglet<'a, 'b -> Writer<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Runs the action every time the Piglet's stream receives data.
    val Run : action: ('a -> unit) -> Piglet<'a, 'b> -> Piglet<'a, 'b>

    /// Run a Piglet UI with the given view.
    val Render : 'v -> Piglet<'a, 'v -> 'elt> -> 'elt

    /// Map the arguments passed to the view.
    val MapViewArgs : 'va -> Piglet<'a, 'va -> 'vb> -> Piglet<'a, ('vb -> 'vc) -> 'vc>

    module Validation =

        /// If the Piglet value passes the predicate, it is passed on;
        /// else, `Failure [msg]` is passed on.
        val Is : pred: ('a -> bool) -> msg: string -> Piglet<'a, 'b> -> Piglet<'a, 'b>

        /// If the Piglet value is not empty, it is passed on;
        /// else, `Failure [msg]` is passed on.
        val IsNotEmpty : msg: string -> Piglet<string, 'b> -> Piglet<string, 'b>

    module Controls =

        open IntelliFactory.WebSharper.Html

        /// A Piglet text input.
        val Input : Stream<string> -> label: string -> Element

        /// A Piglet text area.
        val TextArea : Stream<string> -> label: string -> Element

        /// A Piglet text input that accepts integers.
        val IntInput : Stream<int> -> label: string -> Element

        /// A Piglet checkbox.
        val CheckBox : Stream<bool> -> label: string -> Element

        /// A Piglet radio button set.
        val Radio<'a when 'a : equality> : Stream<'a> -> seq<'a * string> -> Element

        /// Display a reactive value.
        val ShowResult :
            reader: Reader<'a> ->
            render: (Result<'a> -> #seq<#IPagelet>) ->
            container: (seq<#IPagelet> -> Element) ->
            Element

        /// Display a reactive value, or nothing if it is invalid.
        val Show :
            reader: Reader<'a> ->
            render: ('a -> #seq<#IPagelet>) ->
            container: (seq<#IPagelet> -> Element) ->
            Element

        /// Display a reactive value, or nothing if it is invalid.
        val ShowString :
            reader: Reader<'a> ->
            render: ('a -> string) ->
            container: (seq<#IPagelet> -> Element) ->
            Element

        /// Display errors, if any.
        val ShowErrors :
            reader: Reader<'a> ->
            render: (string list -> #seq<#IPagelet>) ->
            container: (seq<#IPagelet> -> Element) ->
            Element

        /// Add an attribute to an element that depends on a reader.
        val Attr :
            reader: Reader<'a> ->
            attrName: string ->
            render: ('a -> string) ->
            Element ->
            Element

        /// Add a CSS style to an element that depends on a reader.
        val Css :
            reader: Reader<'a> ->
            attrName: string ->
            render: ('a -> string) ->
            Element ->
            Element

        /// Displays a submit button driven by the given submitter.
        val Submit : (unit -> unit) * Reader<'a> -> Element

        /// A button that triggers the given callback.
        val Button : Writer<unit> -> Element
