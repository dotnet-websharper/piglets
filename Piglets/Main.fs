namespace Reactive

open System
open System.IO
open System.Web
open IntelliFactory.WebSharper.Sitelets

/// Defines a sample HTML site with nested pages
module SampleSite =
    open IntelliFactory.WebSharper
    open IntelliFactory.Html

    // Action type
    type Action =
        | Index
        | Page1
        | Page2

    module Skin =

        type Page =
            {
                Title : string
                Body : list<Content.HtmlElement>
            }

        let MainTemplate =
            let path = Path.Combine(__SOURCE_DIRECTORY__, "Main.html")
            Content.Template<Page>(path)
                .With("title", fun x -> x.Title)
                .With("body", fun x -> x.Body)

        let WithTemplate title body : Content<Action> =
            Content.WithTemplate MainTemplate <| fun context ->
                {
                    Title = title
                    Body = body context
                }

    // Module containing client-side controls
    module Client =
        open IntelliFactory.WebSharper.Html

        type MyControl() =
            inherit IntelliFactory.WebSharper.Web.Control ()

            [<JavaScript>]
            override this.Body =
                App.UI() :> IPagelet

    let Index =
        Skin.WithTemplate "Index page" <| fun ctx ->
            [
                H1 [Text "Pages"]
                UL [
                    LI [A [HRef (ctx.Link Action.Page1)] -< [Text "Page 1"]]
                    LI [A [HRef (ctx.Link Action.Page2)] -< [Text "Page 2"]]
                ]
            ]

    let Page1 =
        Skin.WithTemplate "Title of Page1" <| fun ctx ->
            let url =  ctx.Link Action.Page2
            [
                H1 [Text "Page 1"]
                A [HRef url] -< [Text "Page 2"]
            ]

    let Page2 =
        Skin.WithTemplate "Title of Page2" <| fun ctx ->
            [
                H1 [Text "Page 2"]
                A [HRef <| ctx.Link Action.Page1] -< [Text "Page 1"]
                Div [new Client.MyControl ()]
            ]

    let MySitelet =
        [
            Sitelet.Content "/index" Action.Index Index
            Sitelet.Folder "/pages" [
                Sitelet.Content "/page1" Action.Page1 Page1
                Sitelet.Content "/page2" Action.Page2 Page2
            ]
        ]
        |> Sitelet.Sum

    // Actions to generate pages from
    let MyActions =
        [
            Action.Index
            Action.Page1
            Action.Page2
        ]

/// The class that contains the website
type MySampleWebsite() =
    interface IWebsite<SampleSite.Action> with
        member this.Sitelet = SampleSite.MySitelet
        member this.Actions = SampleSite.MyActions

[<assembly: WebsiteAttribute(typeof<MySampleWebsite>)>]
do ()
