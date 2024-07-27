#load "./src/docker.fsx"
open Docker
open Docker.DotNet.Models
open System.IO
let cd = __SOURCE_DIRECTORY__
let tempDir = Path.Combine(cd, "temp")
Directory.CreateDirectory(tempDir) |> ignore
let convert from saveTo = task {
    let randomName = Path.ChangeExtension(Path.GetRandomFileName(), ".pdf")
    let randomOutput = Path.ChangeExtension(randomName, ".html")
    let tempFile = Path.Combine(tempDir, randomName)
    File.Copy(from, tempFile, true)
    use connector = new DockerConnector()
    let info =
        CreateContainerParameters(
            Image = "iise/pdf2htmlex-alpine",
            WorkingDir = "/pdf",
            Cmd = [| "pdf2htmlEX"; "--zoom"; "1.3"; randomName; randomOutput |],
            HostConfig = HostConfig(Binds = [| $"{tempDir}:/pdf" |])
        )
    do! connector.RunContainerOnceAsync(info)
    let output = Path.Combine(tempDir, randomOutput)
    File.Move(output, saveTo, true)
    File.Delete(tempFile)
}
