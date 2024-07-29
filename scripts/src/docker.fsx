#r "nuget: Docker.DotNet, 3.125.15"
open Docker.DotNet
open Docker.DotNet.Models
open System
open System.Runtime.InteropServices
open System.Threading
type DockerConnector() =
    let config =
        let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        let url =
            if isWindows then
                "npipe://./pipe/docker_engine"
            else
                "unix:///var/run/docker.sock"
        new DockerClientConfiguration(Uri(url))
    let client = config.CreateClient()
    member this.pipeLogs id =
        let rec loop (stream: MultiplexedStream) = task {
            let buffer = Array.zeroCreate<byte> 4096
            let! read = stream.ReadOutputAsync(buffer, 0, buffer.Length, Unchecked.defaultof<CancellationToken>)
            if read.EOF then
                return ()
            elif read.Count > 0 then
                let output = Text.Encoding.UTF8.GetString(buffer, 0, read.Count)
                match read.Target with
                | MultiplexedStream.TargetStream.StandardIn -> Console.WriteLine("STDIN: " + output)
                | MultiplexedStream.TargetStream.StandardOut -> Console.WriteLine(output)
                | MultiplexedStream.TargetStream.StandardError -> Console.Error.WriteLine(output)
                | _ -> ()
            else
                return! loop stream
        }
        task {
            use! stream =
                client.Containers.GetContainerLogsAsync(
                    id,
                    true,
                    ContainerLogsParameters(ShowStdout = true, ShowStderr = true, Follow = true)
                )
            return! loop stream
        }
    member _.Client = client
    interface IDisposable with
        member _.Dispose() =
            config.Dispose()
            client.Dispose()
    member this.RunContainerOnceAsync(config: CreateContainerParameters) = task {
        let! createResponse = client.Containers.CreateContainerAsync(config)
        let id = createResponse.ID
        let! success = client.Containers.StartContainerAsync(id, null)
        if not success then
            return ()
        this.pipeLogs id |> ignore
        let! _ = client.Containers.WaitContainerAsync(id)
        let! _ = client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters(Force = true))
        return ()
    }
