#load "tools/includes.fsx"

open IntelliFactory.Build

let bt =
    BuildTool().PackageId("IntelliFactory.WebSharper.Piglets", "0.1")

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

bt.Solution [

    main

    bt.NuGet.CreatePackage()
        .Description("Provides a framework to build reactive interfaces in WebSharper,
            similar to Formlets but with more control over the structure of the output.")
        .ProjectUrl("http://bitbucket.com/IntelliFactory/websharper.piglets")
        .Add(main)

]
|> bt.Dispatch
