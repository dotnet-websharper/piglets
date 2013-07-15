namespace IntelliFactory.WebSharper.Piglets

/// Represents the result of a Piglet.
/// It can be successful or failing with optional error messages.
[<Sealed>]
type Result<'a>

module Result =
    /// Get the successful value or error messages of a Result.
    val (|Success|Failure|) : Result<'a> -> Choice<'a, string list>

    /// Create a successful Result.
    val Success : 'a -> Result<'a>

    /// Create a failing Result without error message.
    val Empty : Result<'a>

    /// Create a failing Result with an error message.
    val Failure : string -> Result<'a>

    /// Check if a Result is successful.
    val IsSuccess : Result<'a> -> bool

    /// Get the value of a Result, even if it is failing.
    val Value : Result<'a> -> 'a option

    /// Get the value of a Result, only if it is successful.
    val SuccessValue : Result<'a> -> 'a option

    /// Get the error messages of a Result.
    /// Caution: the list can be empty even if the Result is failing.
    val Errors : Result<'a> -> string list

/// A readable stream.
[<AbstractClass>]
type Reader<'a> =
    /// Get the latest value of the stream.
    abstract member Latest : Result<'a>

    /// Subscribe to subsequent values of the stream.
    abstract member Subscribe : (Result<'a> -> unit) -> unit

    /// Subscribe to the current and subsequent values of the stream.
    /// `reader.SubscribeImmediate f` is equivalent to
    /// `f reader.Latest; reader.Subscribe f`.
    abstract member SubscribeImmediate : (Result<'a> -> unit) -> unit

/// A writeable stream.
[<Interface>]
type Writer<'a> =
    /// Push a value to the stream.
    abstract member Trigger : Result<'a> -> unit

/// A readable and writeable stream.
[<Sealed>]
[<Class>]
type Stream<'a> =
    inherit Reader<'a>
    interface Writer<'a>

    /// Push a value to the stream.
    member Trigger : Result<'a> -> unit

[<Sealed>]
[<Class>]
type Submitter<'a> =
    inherit Reader<'a>
    interface Writer<unit>
    member Trigger : unit -> unit
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
