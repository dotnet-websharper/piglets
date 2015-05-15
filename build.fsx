#load "tools/includes.fsx"

open IntelliFactory.Build

let bt =
    BuildTool().PackageId("WebSharper.Piglets")
        .VersionFrom("WebSharper")
        .WithFramework(fun fw -> fw.Net40)

let main =
    bt.WebSharper.Library("WebSharper.Piglets")
        .SourcesFromProject()
        .References(fun r ->
            [
                r.NuGet("IntelliFactory.Reactive").Reference()
            ])

let test =
    bt.WebSharper.HtmlWebsite("WebSharper.Piglets.Test")
        .SourcesFromProject()
        .References(fun r ->
            [
                r.NuGet("IntelliFactory.Reactive").Reference()
                r.Project(main)
            ])

bt.Solution [

    main
    test

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
