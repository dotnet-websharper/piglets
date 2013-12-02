#load "tools/includes.fsx"

open IntelliFactory.Build

let bt =
    BuildTool().PackageId("WebSharper.Piglets", "0.1")
        .References(fun r ->
            [
                r.Assembly("System.Web")
            ])

let main =
    bt.WebSharper.Library("IntelliFactory.WebSharper.Piglets")
    |> FSharpConfig.BaseDir.Custom "Piglets"
    |> fun proj ->
        proj
            .SourcesFromProject()
            .References(fun r ->
                [
                    r.NuGet("WebSharper").Reference()
                ])

let test =
    bt.WebSharper.Library("IntelliFactory.WebSharper.Piglets.Test")
        .SourcesFromProject()
        .References(fun r ->
            [
                r.Project(main)
            ])

let web =
    bt.WebSharper.HostWebsite("Web")
        .References(fun r ->
            let path =
                [
                    "tools/net45/IntelliFactory.Xml.dll"
                ]
            [
                r.Project(main)
                r.Project(test)
                r.NuGet("WebSharper").At(path).Reference() // Looks like if.build bug?
            ])

bt.Solution [

    main
    test
    web

    bt.NuGet.CreatePackage()
        .Description("Provides a framework to build reactive interfaces in WebSharper,
            similar to Formlets but with more control over the structure of the output.")
        .ProjectUrl("http://github.com/intellifactory/websharper.piglets")
        .Configure(fun c ->
            {
                c with
                    Authors = ["IntelliFactory"]
                    Title = Some "WebSharper.Piglets"
                    LicenseUrl = Some "http://github.com/intellifactory/websharper.piglets/blob/master/LICENSE.md"
                    RequiresLicenseAcceptance = true
            })
        .Add(main)

]
|> bt.Dispatch
