#load "./src/docker.fsx"
#load "./src/types.fsx"
open Types
open Docker
open Docker.DotNet.Models
open System.IO
open System.Text
open Newtonsoft.Json
let cd = __SOURCE_DIRECTORY__
let tempDir = Path.Combine(cd, "temp")
Directory.CreateDirectory(tempDir) |> ignore
let rec convert from saveTo = async {
    try
        let randomName = Path.ChangeExtension(Path.GetRandomFileName(), ".pdf")
        let randomOutput = Path.ChangeExtension(randomName, ".html")
        let tempFile = Path.Combine(tempDir, randomName)
        File.Copy(from, tempFile, true)
        use connector = new DockerConnector()
        let info =
            CreateContainerParameters(
                Image = "iise/pdf2htmlex-alpine",
                WorkingDir = "/pdf",
                Cmd =
                    [| "pdf2htmlEX"
                       "--zoom"
                       "1"
                       "--external-hint-tool=ttfautohint"
                       //    "--embed=cfijo"
                       randomName
                       randomOutput |],
                HostConfig = HostConfig(Binds = [| $"{tempDir}:/pdf" |])
            )
        do! connector.RunContainerOnceAsync(info) |> Async.AwaitTask
        let output = Path.Combine(tempDir, randomOutput)
        File.Move(output, saveTo, true)
        File.Delete(tempFile)
        postProcess saveTo
    with ex ->
        printfn "convert failed %s: %s" from ex.Message
}
and postProcess file =
    let content = file |> File.ReadAllText
    let replace regex (replacement: string) (text: string) =
        RegularExpressions.Regex.Replace(text, regex, replacement)
    let remove regex = replace regex ""
    let result =
        content
        |> replace "margin: 13px auto;" "margin: auto;"
        |> replace "@media print" "@media screen"
        |> remove """<img alt="" src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAEAAAABACA.*?/>"""
        |> remove """#page-container.+\n.*9e9e9e.*\n.*\n.*(\s|\S)+?\}"""
        |> replace "<title></title>" "<title>标题</title>"
    File.WriteAllText(file, result)

let convertAll dataDir = async {
    do!
        [ for dir in Directory.EnumerateDirectories dataDir do
              async {
                  printfn "%s" dir
                  let files = Directory.EnumerateFiles dir
                  let info =
                      files
                      |> Seq.find _.EndsWith(".json")
                      |> File.ReadAllText
                      |> JsonConvert.DeserializeObject<ArticleInfo>
                  let pdfFiles = files |> Seq.filter _.EndsWith(".pdf")
                  printfn "%A" info
                  printfn "%A" pdfFiles
                  for pdf in pdfFiles do
                      let saveTo = Path.Combine(dir, Path.ChangeExtension(Path.GetFileName(pdf), ".html"))
                      if saveTo |> File.Exists |> not then
                          do! convert pdf saveTo
              } ]
        |> Async.Parallel
        |> Async.Ignore
}

let dataDir = Path.Combine(cd |> Path.GetDirectoryName, "data")
Directory.CreateDirectory dataDir |> ignore
convertAll dataDir |> Async.RunSynchronously
