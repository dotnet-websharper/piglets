namespace IntelliFactory.WebSharper.Piglets

type ErrorSourceId = int

[<Sealed>]
[<Class>]
type ErrorMessage =
    member Message : string
    member Source : ErrorSourceId

type Result<'a> =
    | Success of 'a
    | Failure of ErrorMessage list

    static member Failwith : string -> Result<'a>
    member isSuccess : bool

[<AbstractClass>]
type Reader<'a> =
    abstract member Latest : Result<'a>
    abstract member Subscribe : (Result<'a> -> unit) -> unit
    member SubscribeImmediate : (Result<'a> -> unit) -> unit
    member Through : Reader<'b> -> Reader<'a>

type ErrorMessage with
    /// Create an error message associated with the given reader.
    static member Create : string -> Reader<'a> -> ErrorMessage

[<Interface>]
type Writer<'a> =
    abstract member Trigger : Result<'a> -> unit

[<Sealed>]
[<Class>]
type Stream<'a> =
    interface Writer<'a>
    inherit Reader<'a>
    override Latest : Result<'a>
    override Subscribe : (Result<'a> -> unit) -> unit
    member Trigger : Result<'a> -> unit

[<Sealed>]
[<Class>]
type Submitter<'a> =
    interface Writer<unit>
    inherit Reader<'a>
    member Input : Reader<'a>
    member Trigger : unit -> unit

[<Sealed>]
type Piglet<'a, 'v> =
    /// Retrieve the stream associated with a Piglet.
    member Stream : Stream<'a>

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
        /// else, `Failwith msg` is passed on.
        val Is : pred: ('a -> bool) -> msg: string -> Piglet<'a, 'b> -> Piglet<'a, 'b>

        /// If the Piglet value passes the predicate, it is passed on;
        /// else, `Failure [msg]` is passed on.
        val Is' : pred: ('a -> bool) -> msg: ErrorMessage -> Piglet<'a, 'b> -> Piglet<'a, 'b>

        /// Checks that a string is not empty.
        /// Can be used as predicate for Is and Is', eg:
        /// Validation.Is Validation.NotEmpty "Field must not be empty."
        val NotEmpty : value: string -> bool

        /// Check that a string matches a regexp.
        /// Can be used as predicate for Is and Is', eg:
        /// Validation.Is (Validation.Match "^test.*") "Field must start with 'test'."
        val Match : regexp: string -> value: string -> bool
