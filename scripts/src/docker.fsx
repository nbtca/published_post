#r "nuget: Docker.DotNet, 3.125.15"
open Docker.DotNet
open Docker.DotNet.Models
open System
open System.Runtime.InteropServices
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
        let! _ = client.Containers.WaitContainerAsync(id)
        let! _ = client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters(Force = true))
        return ()
    }
