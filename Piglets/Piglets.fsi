namespace IntelliFactory.WebSharper.Piglets

open System

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
    abstract member Subscribe : (Result<'a> -> unit) -> IDisposable
    member SubscribeImmediate : (Result<'a> -> unit) -> IDisposable
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
    override Subscribe : (Result<'a> -> unit) -> IDisposable
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

type Container<'``in``, 'out> =
    abstract member Add : '``in`` -> unit
    abstract member Remove : int -> unit
    abstract member MoveUp : int -> unit
    abstract member Container : 'out

module Many =

    [<Class>]
    type Operations =
        member Delete : Writer<unit>
        member MoveUp : Submitter<unit>
        member MoveDown : Submitter<unit>

    [<Class>]
    type Stream<'a, 'v, 'w,'y,'z> =
        inherit Reader<'a[]>
    
        ///Render the element collection inside this Piglet inside the given container and with the provided rendering function
        member Render : Container<'w, 'u> -> (Operations -> 'v) -> 'u

        ///Stream where new elements for the collection are written
        member Add : Writer<'a>

        ///Function that provides the Adder Piglet with a rendering function
        member AddRender : 'y -> 'z

    [<Class>]
    type UnitStream<'a, 'v, 'w> =
        inherit Stream<'a,'v,'w,'v,'w>

        ///Add an element to the collection set to the default values
        member Add : Writer<unit>

module Piglet =

    /// Create a Piglet initialized with x that passes its stream to the view.
    val Yield : 'a -> Piglet<'a, (Stream<'a> -> 'b) -> 'b>

    /// Create a Piglet initialized with x that doesn't pass any stream to the view.
    val Return : 'a -> Piglet<'a, 'b -> 'b>

    ///Piglet that returns many values with an additional piglet used to create new values in the collection
    val ManyPiglet : 'a[] -> (Piglet<'a,'y->'z>) -> ('a -> Piglet<'a, 'v -> 'w>) -> Piglet<'a[], (Many.Stream<'a, 'v, 'w,'y,'z> -> 'x) -> 'x>

    /// Create a Piglet that returns many values, each created according to the given Piglet.
    val Many : 'a -> ('a -> Piglet<'a, 'v -> 'w>) -> Piglet<'a[], (Many.UnitStream<'a, 'v, 'w> -> 'x) -> 'x>

    /// Create a Piglet that returns many values, each created according to the given Piglet.
    val ManyInit : 'a[] -> 'a -> ('a -> Piglet<'a, 'v -> 'w>) -> Piglet<'a[], (Many.UnitStream<'a, 'v, 'w> -> 'x) -> 'x>

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
    val MapResult : (Result<'a> -> Result<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapAsync : ('a -> Async<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapToAsyncResult : ('a -> Async<Result<'b>>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the Result of a Piglet, without changing its view.
    val MapAsyncResult : (Result<'a> -> Async<Result<'b>>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Run the action every time the Piglet's stream receives successful data.
    val Run : action: ('a -> unit) -> Piglet<'a, 'b> -> Piglet<'a, 'b>

    /// Run the action every time the Piglet's stream receives data.
    val RunResult : action: (Result<'a> -> unit) -> Piglet<'a, 'b> -> Piglet<'a, 'b>

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
