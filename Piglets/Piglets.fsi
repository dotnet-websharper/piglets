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
type Stream<'a> =
    interface Writer<'a>
    interface Reader<'a>

[<Sealed>]
type Submitter<'a> =
    interface Writer<unit>
    interface Reader<'a>
    member Input : Reader<'a>

type Piglet<'a, 'v>

[<AutoOpen>]
module Pervasives =

    val private (<<^) : ('a -> 'b -> 'c) -> 'b -> ('a -> 'c)
    val private (>>^) : ('a -> 'b) -> 'a -> ('b -> 'c) -> 'c

    /// Apply a Piglet function to a Piglet value.
    val (<*>) : Piglet<'a -> 'b, 'c -> 'd> -> Piglet<'a, 'd -> 'e> -> Piglet<'b, 'c -> 'e>

    /// Apply a Piglet function to a Piglet Result.
    val (<*?>) : Piglet<'a -> 'b, 'c -> 'd> -> Piglet<Result<'a>, 'd -> 'e> -> Piglet<'b, 'c -> 'e>

module Piglet =

    /// Create a Piglet initialized with x that passes its stream to the view.
    val Yield : 'a -> Piglet<'a, (Stream<'a> -> 'b) -> 'b>

    /// Create a Piglet initialized with x that doesn't pass any stream to the view.
    val Return : 'a -> Piglet<'a, 'b -> 'b>

    /// Create a Piglet value that streams the value every time it receives a signal.
    /// The signaling function is passed to the view.
    val WithSubmit : Piglet<'a, 'b -> Submitter<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass this Piglet's stream to the view.
    val TransmitStream : Piglet<'a, 'b -> Stream<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a reader for this Piglet's stream to the view.
    val TransmitReader : Piglet<'a, 'b -> Reader<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a writer for this Piglet's stream to the view.
    val TransmitWriter : Piglet<'a, 'b -> Writer<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Map the value of a Piglet, without changing its view.
    val Map : ('a -> 'b) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapToResult : ('a -> Result<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the Result of a Piglet, without changing its view.
    val MapAsyncResult : (Result<'a> -> Async<Result<'b>>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapAsync : ('a -> Async<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapToAsyncResult : ('a -> Async<Result<'b>>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the Result of a Piglet, without changing its view.
    val MapResult : (Result<'a> -> Result<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Run the action every time the Piglet's stream receives data.
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

        /// If the Piglet value matches the regexp, it is passed on;
        /// else, `Failure [msg]` is passed on.
        val IsMatch : regexp: string -> msg: string -> Piglet<string, 'b> -> Piglet<string, 'b>

    module Controls =

        open IntelliFactory.WebSharper.Html

        /// A Piglet text input.
        val Input : Stream<string> -> label: string -> Element

        /// A Piglet password input.
        val Password : Stream<string> -> label: string -> Element

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

        /// Add a CSS style to an element that depends on a reader.
        val Css :
            reader: Reader<'a> ->
            attrName: string ->
            render: ('a -> string) ->
            Element ->
            Element

        /// Displays a submit button driven by the given submitter.
        val Submit : Writer<unit> -> Element

        /// A button that triggers the given callback.
        val Button : Writer<unit> -> Element

        /// Enables the element when reading Success, disable it when reading Failure.
        val EnableOnSuccess : Reader<'a> -> Element -> Element
