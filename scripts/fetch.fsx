#r "nuget: PuppeteerSharp, 18.0.5"
#load "./src/types.fsx"
#load "./src/utils.fsx"
open PuppeteerSharp
open System.IO
open System.Text.RegularExpressions
open Types
open Utils
let useLocalBrowser = true
let mutable executablePath =
    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
let browserType = SupportedBrowser.Chromium
if not useLocalBrowser || not (File.Exists executablePath) then
    let a = 1 + 2
    executablePath <- null
    let browserFetcher =
        BrowserFetcher(BrowserFetcherOptions(Browser = browserType, Path = Path.Combine(__SOURCE_DIRECTORY__, "bin")))
    let result =
        browserFetcher.DownloadAsync(BrowserTag.Latest)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    executablePath <- result.GetExecutablePath()
let browserTask =
    Puppeteer.LaunchAsync(new LaunchOptions(Headless = false, ExecutablePath = executablePath, Browser = browserType))
//await using var page = await browser.NewPageAsync();
//await page.GoToAsync("http://www.google.com");
//await page.ScreenshotAsync(outputFile);
let cd = Path.GetDirectoryName __SOURCE_DIRECTORY__
let browserSave url = task {
    let! browser = browserTask
    let! page = browser.NewPageAsync()
    do! page.GoToAsync(url, NavigationOptions()) |> Async.AwaitTask |> Async.Ignore
    let eval script = async { return! script |> page.EvaluateExpressionAsync<string> |> Async.AwaitTask }
    let getMetaContent name =
        eval $"document.querySelector(\"head > [property='%s{name}']\")?.content"
    let! title = getMetaContent "og:title"
    let! description = getMetaContent "og:description"
    let! image = getMetaContent "og:image"
    let! author = getMetaContent "og:article:author"
    let! date = eval "document.querySelector(\"#publish_time\")?.innerText?.trim()"
    let! location = eval "document.querySelector(\"#js_ip_wording_wrp\")?.innerText?.trim()"
    let dataDir = Path.Combine(cd, "data", date |> toValidPath)
    Directory.CreateDirectory(dataDir) |> ignore
    let pdfFilePC = Path.Combine(dataDir, toValidPath title + ".pc.pdf")
    let pdfFileMobile = Path.Combine(dataDir, toValidPath title + ".mobile.pdf")
    do! eval "document.body.style.zoom = '1%'" |> Async.Ignore
    let mutable waitingCount = 5
    page.Console.AddHandler(fun s e ->
        waitingCount <- 6
        printfn $"\t\t[%A{e.Message.Type}] %s{e.Message.Text}")
    while waitingCount > 0 do
        do! Async.Sleep 1000
        waitingCount <- waitingCount - 1
    do! eval "document.body.style.zoom = '100%'" |> Async.Ignore
    do!
        page.PdfAsync(
            pdfFilePC,
            PdfOptions(
                PreferCSSPageSize = true,
                OmitBackground = true,
                Height = "100cm",
                PrintBackground = true,
                //MarginOptions = Media.MarginOptions(Top = "0", Bottom = "0", Left = "0", Right = "0"),
                Scale = 1.5m
            )
        )
    do! eval "document.body.style.zoom = '175%'" |> Async.Ignore
    do!
        page.PdfAsync(
            pdfFileMobile,
            PdfOptions(
                PreferCSSPageSize = true,
                OmitBackground = true,
                Height = "100cm",
                PrintBackground = true,
                //MarginOptions = Media.MarginOptions(Top = "0", Bottom = "0", Left = "0", Right = "0"),
                Scale = 1.5m
            )
        )
    do! page.CloseAsync()
    let article =
        { Url = url
          Title = title
          Description = description
          Image = image
          Author = author
          Date = date
          Location = location
          FilePathPC = Path.GetRelativePath(cd, pdfFilePC)
          FilePathMobile = Path.GetRelativePath(cd, pdfFileMobile) }
    let dataJson = Path.Combine(dataDir, toValidPath title + ".json")
    File.WriteAllText(
        dataJson,
        Newtonsoft.Json.JsonConvert.SerializeObject(article, Newtonsoft.Json.Formatting.Indented)
    )
    return article
}
let articlesFilePath = Path.Combine(cd, "articles.json")
let articles =
    if File.Exists articlesFilePath then
        File.ReadAllText articlesFilePath
        |> Newtonsoft.Json.JsonConvert.DeserializeObject<ArticleInfo[]>
    else
        let articles =
            "../待迁移公众号文章的跳转.txt"
            |> File.ReadAllText
            |> (fun text -> Regex.Matches(text, @"https://mp.weixin.qq.com/\S+"))
            |> Seq.cast<Match>
            |> Seq.map (_.Value)
            |> Seq.map ArticleInfo.Create
            |> Seq.toArray
        File.WriteAllText(
            articlesFilePath,
            Newtonsoft.Json.JsonConvert.SerializeObject(articles, Newtonsoft.Json.Formatting.Indented)
        )
        articles
let main () = task {
    for i, link in
        articles
        |> Array.indexed
        |> Array.filter (fun (_, x) ->
            [ x.FilePathPC, x.FilePathMobile ]
            |> List.exists (fun (_, fp) -> fp |> isNull || fp |> (fun x -> Path.Combine(cd, x)) |> File.Exists |> not)) do
        try
            printfn "%s" link.Url
            let! ret = browserSave link.Url
            articles[i] <- ret
            File.WriteAllText(
                articlesFilePath,
                Newtonsoft.Json.JsonConvert.SerializeObject(articles, Newtonsoft.Json.Formatting.Indented)
            )
            printfn "%A" ret
        with e ->
            printfn "%s" e.Message
}
main () |> Async.AwaitTask |> Async.RunSynchronously
