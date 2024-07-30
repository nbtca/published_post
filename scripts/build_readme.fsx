#r "nuget: Newtonsoft.Json"
#load "./src/types.fsx"
open System.IO
open System
open Newtonsoft.Json
open Types
let cd = Path.GetDirectoryName(__SOURCE_DIRECTORY__)
let readme = Path.Combine(cd, "README.md")
let readmeContent = File.ReadAllText(readme)
let buildContent () =
    let dataDir = Path.Combine(cd, "data")
    let postsDir = Path.Combine(cd, "posts")
    let sb = new Text.StringBuilder()
    let (!+) (line: string) = sb.AppendLine(line) |> ignore
    let append info fileM filePC =
        let convertName file =
            Path.GetRelativePath(cd, file).Replace("\\", "/")
        let imageUrl = info.Image
        !+ $"- <a href=\"./{convertName filePC}\">{info.Title}</a> \n"
        !+ $"  - <img src=\"{imageUrl}\" width=\"100\" height=\"100\" />\n"
        !+ $"  - <a href=\"./{convertName fileM}\">移动端</a>\n"
        !+ $"  - 描述: {info.Description}\n"
        !+ $"  - 作者: {info.Author}\n"
        !+ $"  - 日期: {info.Date}\n"
        !+ $"  - <a href=\"{info.Url}\">查看原文</a>\n"


    for dir in Directory.EnumerateDirectories dataDir do
        try
            //   printfn "%s" dir
            let files = Directory.EnumerateFiles dir
            let info =
                files
                |> Seq.find _.EndsWith(".json")
                |> File.ReadAllText
                |> JsonConvert.DeserializeObject<ArticleInfo>
            let pdfFiles = files |> Seq.filter _.EndsWith(".pdf")
            printfn "%A" info
            printfn "%A" pdfFiles
            let files =
                [ for pdf in pdfFiles do
                      let filename = Path.GetFileName(pdf)
                      let dateTimeStr = info.DateTimeStr
                      let saveToDir = Path.Combine(postsDir, dateTimeStr)
                      let saveTo = Path.Combine(saveToDir, Path.ChangeExtension(filename, ".html"))
                      saveTo ]
            let fileMobile = files |> Seq.findBack _.Contains("mobile")
            let filePC = files |> Seq.findBack _.Contains("pc")
            append info fileMobile filePC
        with ex ->
            printfn "convertAll failed %s: %s" dir ex.Message
    sb.ToString()
let content =
    let beforeSplit =
        let startTag = "<!---START--->"
        let index = readmeContent.IndexOf(startTag)
        if index = -1 then
            readmeContent
        else
            readmeContent.Substring(0, index)
    let afterSplit =
        let endTag = "<!---END--->"
        let index = readmeContent.IndexOf(endTag)
        if index = -1 then
            ""
        else
            readmeContent.Substring(index + endTag.Length)
    beforeSplit
    + "\n\n<!---START--->\n\n"
    + "# 文章列表\n\n"
    + buildContent ()
    + "\n\n<!---END--->\n"
    + afterSplit

File.WriteAllText(readme, content)
