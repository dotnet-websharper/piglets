module App

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.Piglets

module Model =

    type Name = { firstName: string; lastName: string }

    type Gender = Male | Female

    type User =
        { name: Name; age: int; gender: Gender; comments: string; participates: bool; friends: string[] }

        [<JavaScript>]
        static member Pretty u =
            u.name.firstName + " " + u.name.lastName
            + ", aged " + string u.age
            + if u.gender = Male then ", male" else ", female"
            + if u.comments = "" then "\nNo comment" else ("\n" + u.comments)
            + if u.participates then "\nParticipates" else "\nDoesn't participate"

module ViewModel =

    open Model
    module V = Piglet.Validation

    [<JavaScript>]
    let Name init =
        Piglet.Return (fun f l -> { firstName = f; lastName = l })
        <*> (Piglet.Yield init.firstName
            |> V.Is V.NotEmpty "First name should not be empty.")
        <*> (Piglet.Yield init.lastName
            |> V.Is V.NotEmpty "Last name should not be empty.")
        |> Piglet.MapViewArgs (fun f l -> (f, l))

    [<JavaScript>]
    let User init =
        Piglet.Return (fun n a g c p f -> { name = n; age = a; gender = g; comments = c; participates = p; friends = f })
        <*> Name init.name
        <*> (Piglet.Yield init.age
            |> Piglet.Validation.Is (fun a -> a >= 18) "You must be over 18.")
        <*> Piglet.Yield init.gender
        <*> Piglet.Yield init.comments
        <*> Piglet.Yield init.participates
        <*> Piglet.ManyInit init.friends "" (fun f ->
            Piglet.Yield f
            |> Piglet.Validation.Is Piglet.Validation.NotEmpty "A friend with no name?")
        |> Piglet.TransmitReader
        |> Piglet.WithSubmit
        |> Piglet.Run (fun u ->
            JavaScript.Alert (Model.User.Pretty u))

module View =

    open Model
    open IntelliFactory.WebSharper.Html
    module C = IntelliFactory.WebSharper.Piglets.Controls

    [<JavaScript>]
    let RedBgOnError (r: Reader<'a>) =
        C.CssResult r "background-color" (fun x ->
            if x.isSuccess then "white" else "#ffa0a0")

    [<JavaScript>]
    let User (firstName, lastName) age gender comments participates (friends: Many.Renderer<_,_,_>) liveUser submit =
        Div [
            Div [C.Input firstName |> RedBgOnError firstName |> C.WithLabel "First name:"]
            Div [C.Input lastName |> RedBgOnError lastName |> C.WithLabel "Last name:"]
            Div [C.Radio gender [Male, "Male"; Female, "Female"]]
            Div [C.IntInput age |> RedBgOnError age |> C.WithLabel "Age:"]
            Div [C.CheckBox participates |> C.WithLabel "Participate in the survey"]
            Div [C.TextArea comments |> C.WithLabel "Comments:"]
            Div [] |> C.RenderMany friends (fun ops friend ->
                Div [
                    C.Input friend
                    C.Button ops.Delete -< [Attr.Value "Delete this friend"]
                    C.Button ops.MoveUp -< [Attr.Value "Move up"]
                        |> C.EnableOnSuccess ops.MoveUp.Input
                    C.Button ops.MoveDown -< [Attr.Value "Move down"]
                        |> C.EnableOnSuccess ops.MoveDown.Input
                ])
            Div [C.Button friends.Add -< [Attr.Value "Add a friend"]]
            Table [
                TBody [
                    TR [
                        TH [Attr.ColSpan "6"] -< [Text "Summary"]
                    ]
                    TR [
                        TH [Text "First name"]
                        TH [Text "Last name"]
                        TH [Text "Gender"]
                        TH [Text "Age"]
                        TH [Text "Participates"]
                        TH [Text "Comments"]
                        TH [Text "Friends"]
                    ]
                    TR [
                        // These will only show up if the whole user is valid
                        TD [] |> C.ShowString liveUser (fun u -> u.name.firstName)
                        TD [] |> C.ShowString liveUser (fun u -> u.name.lastName)
                        TD [] |> C.ShowString liveUser (fun u -> if u.gender = Male then "Male" else "Female")
                        TD [] |> C.ShowString liveUser (fun u -> string u.age)
                        // This one will show up even if other parts are invalid
                        // because it uses the `participates` stream instead of `liveUser`
                        TD [] |> C.ShowString participates (function
                                | true -> "Yes"
                                | false -> "No")
                            |> C.Css participates "font-weight" (function
                                | true -> "bold"
                                | false -> "normal")
                        TD [] |> C.Show liveUser (function
                            | {comments = ""} -> [I [Text "(no comment)"]]
                            | {comments = c} -> [Span [Text c]])
                        TD [] |> C.ShowString friends.Output (String.concat ", ")
                    ]
                ]
            ]
            Div [] |> C.ShowErrors liveUser (fun msgs ->
                [
                    Div [Attr.Style "border:solid 1px #c00;color:#c00;margin:10px;padding:5px"] -< [
                        for m in msgs do
                            yield Text m
                            yield (Br [] :> _)
                    ]
                ])
            Div [C.Submit submit]
            Div [
                Br []; Br []
                C.IntInput age |> C.WithLabel "Age again:"
                Span [Text "(just to test several inputs connected to the same stream)"]
            ]
        ]

[<JavaScript>]
let UI() =
    let initUser : Model.User =
        {
            name = { firstName = "John"; lastName = "Rambo" }
            age = 40
            gender = Model.Male
            comments = "Blah blah blah"
            participates = true
            friends = [|"Ernesto"; "Loic"|]
        }
    ViewModel.User initUser
    |> Piglet.Render View.User 