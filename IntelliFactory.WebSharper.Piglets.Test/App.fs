module App

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.Piglets

module Model =

    type Name = { firstName: string; lastName: string }

    type User =
        { name: Name; age: int; isMale: bool; comments: string }

        [<JavaScript>]
        static member Pretty u =
            u.name.firstName + " " + u.name.lastName
            + ", aged " + string u.age
            + if u.isMale then ", male" else ", female"
            + if u.comments = "" then ", no comment" else ("; " + u.comments)

module ViewModel =

    open Model

    [<JavaScript>]
    let Name init =
        Piglet.Return (fun f l -> { firstName = f; lastName = l })
        <*> (Piglet.Yield init.firstName
            |> Piglet.Validation.IsNotEmpty "First name should not be empty.")
        <*> (Piglet.Yield init.lastName
            |> Piglet.Validation.IsNotEmpty "Last name should not be empty.")
        |> Piglet.MapViewArgs (fun f l -> (f, l))

    [<JavaScript>]
    let User init =
        Piglet.Return (fun n a m c -> { name = n; age = a; isMale = m; comments = c })
        <*> Name init.name
        <*> (Piglet.Yield init.age
            |> Piglet.Validation.Is (fun a -> a >= 18) "You must be over 18.")
        <*> Piglet.Yield init.isMale
        <*> Piglet.Yield init.comments
        |> Piglet.TransmitReader
        |> Piglet.WithSubmit
        |> Piglet.Run (fun u ->
            JavaScript.Alert (Model.User.Pretty u))

module View =

    open Model
    open IntelliFactory.WebSharper.Html

    [<JavaScript>]
    let User (firstName, lastName) age isMale comments liveUser submit =
        Div [
            Div [
                Label [Text "First name:"]
                Piglet.Controls.Input firstName
            ]
            Div [
                Label [Text "Last name:"]
                Piglet.Controls.Input lastName
            ]
            Div [
                Piglet.Controls.CheckBox isMale -< [Attr.Id "ismale"]
                Label [Text "Male"; Attr.For "ismale"]
            ]
            Div [
                Label [Text "Age:"]
                Piglet.Controls.IntInput age
            ]
            Div [
                Label [Text "Comments:"]
                Piglet.Controls.TextArea comments
            ]
            Piglet.Controls.Show liveUser Div <| function
                | Success u ->
                    [
                        Attr.Style "border:solid 1px #bbb;margin:10px;padding:5px"
                        B [Text "Summary: "] :> IPagelet
                        Text (User.Pretty u)
                    ]
                | Failure msgs ->
                    [
                        yield Attr.Style "border:solid 1px #c00;color:#c00;margin:10px;padding:5px"
                        for m in msgs do yield Text m; yield (Br [] :> _)
                    ]
            Div [
                Piglet.Controls.Submit submit
            ]
            Div [
                Br []; Br []
                Label [Text "Age again:"]
                Piglet.Controls.IntInput age
                Span [Text "(just to test several inputs connected to the same stream)"]
            ]
        ]

[<JavaScript>]
let UI() =
    let initUser : Model.User =
        {
            name = { firstName = "John"; lastName = "Rambo" }
            age = 40
            isMale = true
            comments = "Badass"
        }
    ViewModel.User initUser
    |> Piglet.Render View.User 