#load "tools/includes.fsx"

open IntelliFactory.Build

let bt =
    BuildTool().PackageId("WebSharper.Piglets")
        .VersionFrom("WebSharper")
        .WithFSharpVersion(FSharpVersion.FSharp30)
        .WithFramework(fun fw -> (fw: Frameworks).Net40)

let main =
    bt.WebSharper4.Library("WebSharper.Piglets")
        .SourcesFromProject()
        .WithSourceMap()
        .References(fun r ->
            [
                r.NuGet("WebSharper.Reactive").Latest(true).ForceFoundVersion().Reference()
                r.NuGet("WebSharper.Html").Latest(true).ForceFoundVersion().Reference()
            ])

let test =
    bt.WebSharper4.HtmlWebsite("WebSharper.Piglets.Test")
        .SourcesFromProject()
        .WithSourceMap()
        .References(fun r ->
            [
                r.NuGet("WebSharper.Reactive").Latest(true).ForceFoundVersion().Reference()
                r.NuGet("WebSharper.Html").Latest(true).ForceFoundVersion().Reference()
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
