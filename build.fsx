#r "paket:
nuget Fake.Core.Target
nuget Fake.Core.Environment
nuget EventStore.Client //"
#load ".fake/build.fsx/intellisense.fsx"
open EventStore.ClientAPI
open Fake.Core
open System
open System.Text


type Client(isCluster:bool) =
    let uri =
        if isCluster then
            "discover://admin:changeit@localhost:2113"
        else
            "tcp://admin:changeit@localhost:1113"
        |> Uri

    let connSettings =
        ConnectionSettings.Create()
            .EnableVerboseLogging()
            .UseConsoleLogger()
            .Build()
    let creds = SystemData.UserCredentials("admin", "changeit")
    let conn = EventStoreConnection.Create(connSettings, uri)
    do conn.ConnectAsync().Wait()

    member __.WriteEvent(streamId:string, data:string) =
        let encodedData = Encoding.UTF8.GetBytes(data)
        let encodedMeta = Encoding.UTF8.GetBytes("some meta")
        let eventData = EventData(Guid.NewGuid(), "TestEvent", false, encodedData, encodedMeta)
        let version = ExpectedVersion.Any |> int64
        conn.AppendToStreamAsync(streamId, version, eventData)
        |> Async.AwaitTask

    member __.ReadEvents(streamId:string) =
        async {
            let! streamEventSlice =
                conn.ReadStreamEventsForwardAsync(streamId, 0L, 10, false, creds)
                |> Async.AwaitTask 
            return 
                streamEventSlice.Events
                |> Array.map (fun event -> Encoding.UTF8.GetString(event.Event.Data))
        }

Target.create "TestSingle" (fun _ ->
    Trace.trace "Connecting to client"
    let client = Client(false)
    async {
        Trace.trace "Writing events"
        do! client.WriteEvent("test", "hello") |> Async.Ignore
        do! client.WriteEvent("test", "world") |> Async.Ignore
        Trace.trace "Reading events"
        let! events = client.ReadEvents("test")
        Trace.tracefn "Events:\n%A" events
    } |> Async.RunSynchronously
    Trace.trace "Finished"
)

Target.create "TestMulti" (fun _ ->
    Trace.trace "Connecting to client"
    let client = Client(true)
    async {
        Trace.trace "Writing events"
        do! client.WriteEvent("test", "hello") |> Async.Ignore
        do! client.WriteEvent("test", "world") |> Async.Ignore
        Trace.trace "Reading events"
        let! events = client.ReadEvents("test")
        Trace.tracefn "Events:\n%A" events
    } |> Async.RunSynchronously
    Trace.trace "Finished"
)

Target.create "Default" ignore

Target.runOrDefault "Default"
