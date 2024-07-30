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
                       //    "--external-hint-tool=ttfautohint"
                       //    "--embed=cfijo"
                       randomName
                       randomOutput |],
                HostConfig = HostConfig(Binds = [| $"{tempDir}:/pdf" |])
            )
        do! connector.RunContainerOnceAsync(info) |> Async.AwaitTask
        let output = Path.Combine(tempDir, randomOutput)
        File.Move(output, saveTo, true)
        File.Delete(tempFile)
    with ex ->
        printfn "convert failed %s: %s" from ex.Message
}
and postProcess (info: ArticleInfo) file saveTo =
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
        |> replace "<title></title>" $"<title>%s{info.Title}</title>"
    File.WriteAllText(saveTo, result)
let convertAll dataDir postsDir = async {
    do!
        [ for dir in Directory.EnumerateDirectories dataDir do
              async {
                  try
                      //   printfn "%s" dir
                      let files = Directory.EnumerateFiles dir
                      let info =
                          files
                          |> Seq.find _.EndsWith(".json")
                          |> File.ReadAllText
                          |> JsonConvert.DeserializeObject<ArticleInfo>
                      let pdfFiles = files |> Seq.filter _.EndsWith(".pdf")
                      //   printfn "%A" info
                      //   printfn "%A" pdfFiles
                      for pdf in pdfFiles do
                          let filename = Path.GetFileName(pdf)
                          let saveToRaw = Path.Combine(dir, Path.ChangeExtension(filename, ".raw.html"))
                          //2016年11月09日 22:43
                          let dateTimeStr = info.DateTimeStr
                          let saveToDir = Path.Combine(postsDir, dateTimeStr)
                          let saveTo = Path.Combine(saveToDir, Path.ChangeExtension(filename, ".html"))
                          if saveToRaw |> File.Exists |> not then
                              do! convert pdf saveToRaw
                          if saveToDir |> Directory.Exists |> not then
                              Directory.CreateDirectory saveToDir |> ignore
                          postProcess info saveToRaw saveTo
                  with ex ->
                      printfn "convertAll failed %s: %s" dir ex.Message
              } ]
        |> Async.Parallel
        |> Async.Ignore
}

let dataDir = Path.Combine(cd |> Path.GetDirectoryName, "data")
let postsDir = Path.Combine(cd |> Path.GetDirectoryName, "posts")
Directory.CreateDirectory dataDir |> ignore
convertAll dataDir postsDir |> Async.RunSynchronously
