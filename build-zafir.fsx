#load "tools/includes.fsx"

open IntelliFactory.Build

let bt =
    BuildTool().PackageId("Zafir.Piglets")
        .VersionFrom("Zafir")
        .WithFSharpVersion(FSharpVersion.FSharp30)
        .WithFramework(fun fw -> fw.Net40)

let main =
    bt.Zafir.Library("WebSharper.Piglets")
        .SourcesFromProject()
        .WithSourceMap()
        .References(fun r ->
            [
                r.NuGet("Zafir.Reactive").Latest(true).ForceFoundVersion().Reference()
                r.NuGet("Zafir.Html").Latest(true).ForceFoundVersion().Reference()
            ])

let test =
    bt.Zafir.HtmlWebsite("WebSharper.Piglets.Test")
        .SourcesFromProject()
        .WithSourceMap()
        .References(fun r ->
            [
                r.NuGet("Zafir.Reactive").Latest(true).ForceFoundVersion().Reference()
                r.NuGet("Zafir.Html").Latest(true).ForceFoundVersion().Reference()
                r.Project(main)
            ])

bt.Solution [

    main
//    test

    bt.NuGet.CreatePackage()
        .Description("Provides a framework to build reactive interfaces in Zafir,
            similar to Formlets but with more control over the structure of the output.")
        .ProjectUrl("http://github.com/intellifactory/websharper.piglets")
        .Configure(fun c ->
            {
                c with
                    Authors = ["IntelliFactory"]
                    Title = Some "Zafir.Piglets"
                    LicenseUrl = Some "http://github.com/intellifactory/websharper.piglets/blob/master/LICENSE.md"
                    RequiresLicenseAcceptance = true
            })
        .Add(main)

]
|> bt.Dispatch
