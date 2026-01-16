module GraphServer

open System
open System.IO
open System.Text
open System.Diagnostics
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Argu
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.FileProviders
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Serilog
open Collections
open Environment
open CLI
open Errors

type BuildRequest = {
    Targets: string list option
    Projects: string list option
    Parallelism: int option
    Force: bool option
    Retry: bool option
}

type ProjectInfo = {
    Id: string
    Name: string option
    Directory: string
    Hash: string
}

type ProjectStatus = {
    ProjectId: string
    Status: string
}

type BuildLogState = {
    Buffer: StringBuilder
    Subscribers: ConcurrentDictionary<Guid, Channel<string>>
    Lock: obj
}

type BuildState = {
    mutable Active: Process option
    mutable ExitCode: int option
    Lock: obj
}

let rec private findWorkspace dir =
    if FS.combinePath dir "WORKSPACE" |> IO.exists then
        Some dir
    else
        dir |> FS.parentDirectory |> Option.bind findWorkspace

let private createLogState () =
    { Buffer = StringBuilder()
      Subscribers = ConcurrentDictionary<Guid, Channel<string>>()
      Lock = obj() }

let private createBuildState () =
    { Active = None
      ExitCode = None
      Lock = obj() }

let private appendLog (state: BuildLogState) (text: string) =
    if String.IsNullOrEmpty(text) |> not then
        lock state.Lock (fun () ->
            state.Buffer.Append(text) |> ignore
        )
        for KeyValue(_, channel) in state.Subscribers do
            channel.Writer.TryWrite(text) |> ignore

let private clearLog (state: BuildLogState) =
    lock state.Lock (fun () ->
        state.Buffer.Clear() |> ignore
    )

let private completeLogStreams (state: BuildLogState) =
    for KeyValue(_, channel) in state.Subscribers do
        channel.Writer.TryComplete() |> ignore

let private logSnapshot (state: BuildLogState) =
    lock state.Lock (fun () -> state.Buffer.ToString())

let private parseCsvValues (values: Microsoft.Extensions.Primitives.StringValues) =
    values.ToArray()
    |> Array.choose (fun (value: string | null) ->
        if isNull value || value = "" then None else Some (string value))
    |> Array.collect (fun (value: string) ->
        value.Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries))
    |> Array.toList

let private readBody (ctx: HttpContext) =
    task {
        use reader = new StreamReader(ctx.Request.Body, Encoding.UTF8)
        return! reader.ReadToEndAsync()
    }

let private buildConfig (workspace: string) (targets: string list) (projects: string list option) =
    let previousDir = System.Environment.CurrentDirectory
    System.Environment.CurrentDirectory <- workspace
    try
        let homeDir = Cache.createHome()
        let tmpDir = Cache.createTmp()
        let sharedDir = ".terrabuild"
        IO.createDirectory sharedDir
        let sourceControl = SourceControls.Factory.create()
        let options =
            { ConfigOptions.Options.Workspace = workspace
              ConfigOptions.Options.HomeDir = homeDir
              ConfigOptions.Options.TmpDir = tmpDir
              ConfigOptions.Options.SharedDir = sharedDir
              ConfigOptions.Options.WhatIf = false
              ConfigOptions.Options.Debug = false
              ConfigOptions.Options.MaxConcurrency = 1
              ConfigOptions.Options.Force = false
              ConfigOptions.Options.Retry = false
              ConfigOptions.Options.LocalOnly = true
              ConfigOptions.Options.StartedAt = DateTime.UtcNow
              ConfigOptions.Options.Targets = targets |> Seq.map String.toLower |> Set
              ConfigOptions.Options.LogTypes = sourceControl.LogTypes
              ConfigOptions.Options.Configuration = None
              ConfigOptions.Options.Environment = None
              ConfigOptions.Options.Note = None
              ConfigOptions.Options.Label = None
              ConfigOptions.Options.Types = None
              ConfigOptions.Options.Labels = None
              ConfigOptions.Options.Projects = projects |> Option.map (fun items -> items |> Seq.map String.toLower |> Set)
              ConfigOptions.Options.Variables = Map.empty
              ConfigOptions.Options.Engine = None
              ConfigOptions.Options.HeadCommit = sourceControl.HeadCommit
              ConfigOptions.Options.CommitLog = sourceControl.CommitLog
              ConfigOptions.Options.BranchOrTag = sourceControl.BranchOrTag
              ConfigOptions.Options.Run = sourceControl.Run }
        Configuration.read options
    finally
        System.Environment.CurrentDirectory <- previousDir

let private buildBatchGraph workspace targets projects =
    let options, config = buildConfig workspace targets projects
    let cache = Cache.Cache(Storages.Factory.create None, None) :> Cache.ICache
    let options = { options with MaxConcurrency = System.Environment.ProcessorCount |> max 1 }
    let graph = GraphPipeline.Node.build options config
    let graph = GraphPipeline.Action.build options cache graph
    let graph = GraphPipeline.Cascade.build graph
    GraphPipeline.Batch.build options config graph, options

let private resolveWorkspace (workspace: string option) =
    match workspace with
    | Some ws -> ws
    | _ ->
        match Environment.currentDir() |> findWorkspace with
        | Some ws -> ws
        | _ -> raiseInvalidArg "Can't find workspace root directory. Check you are in a workspace."

let private pickPort () =
    let listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0)
    listener.Start()
    let port = (listener.LocalEndpoint :?> Net.IPEndPoint).Port
    listener.Stop()
    port

let private isDotnetHost (path: string) =
    let file =
        Path.GetFileName(path)
        |> Option.ofObj
        |> Option.defaultValue ""
        |> fun name -> name.ToLowerInvariant()
    file = "dotnet" || file = "dotnet.exe"

let private openBrowser (url: string) =
    try
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            Process.Start(ProcessStartInfo("cmd", $"/c start {url}")) |> ignore
        elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            Process.Start(ProcessStartInfo("open", url)) |> ignore
        else
            Process.Start(ProcessStartInfo("xdg-open", url)) |> ignore
    with _ -> ()

let private createBuildCommand (workspace: string) (request: BuildRequest) =
    let exePath = System.Environment.ProcessPath |> Option.ofObj |> Option.defaultValue "dotnet"
    let assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location
    let targets = request.Targets |> Option.defaultValue [] |> Seq.map String.toLower |> String.join " "
    let projectArgs =
        match request.Projects with
        | Some projects when projects.Length > 0 ->
            projects |> Seq.map String.toLower |> String.join " " |> sprintf " -p %s"
        | _ -> ""
    let parallelArg =
        match request.Parallelism with
        | Some value when value > 0 -> $" --parallel {value}"
        | _ -> ""
    let forceArg = if request.Force |> Option.defaultValue false then " -f" else ""
    let retryArg = if request.Retry |> Option.defaultValue false then " -r" else ""
    let baseArgs = $"run {targets} -w \"{workspace}\"{projectArgs}{parallelArg}{forceArg}{retryArg}"
    if isDotnetHost exePath then
        exePath, $"\"{assemblyPath}\" {baseArgs}"
    else
        exePath, baseArgs

let private startBuildProcess (workspace: string) (request: BuildRequest) (logState: BuildLogState) (buildState: BuildState) =
    lock buildState.Lock (fun () ->
        match buildState.Active with
        | Some proc when not proc.HasExited -> Error "Build already running."
        | _ ->
            clearLog logState
            completeLogStreams logState
            let command, args = createBuildCommand workspace request
            let psi =
                ProcessStartInfo(
                    FileName = command,
                    Arguments = args,
                    WorkingDirectory = workspace,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                )
            psi.EnvironmentVariables["TB_FORCE_ANSI"] <- "1"
            psi.EnvironmentVariables["TERM"] <- "xterm-256color"
            let proc = new Process(StartInfo = psi, EnableRaisingEvents = true)
            if proc.Start() |> not then
                Error "Failed to start build process."
            else
                buildState.Active <- Some proc
                buildState.ExitCode <- None

                let streamToLog (stream: Stream) =
                    task {
                        use reader = new StreamReader(stream, Encoding.UTF8)
                        let buffer = Array.zeroCreate<char> 4096
                        let mutable keepReading = true
                        while keepReading do
                            let! read = reader.ReadAsync(buffer, 0, buffer.Length)
                            if read = 0 then
                                keepReading <- false
                            else
                                appendLog logState (String(buffer, 0, read))
                    }

                streamToLog proc.StandardOutput.BaseStream |> ignore
                streamToLog proc.StandardError.BaseStream |> ignore

                proc.Exited.Add(fun _ ->
                    buildState.ExitCode <- Some proc.ExitCode
                    buildState.Active <- None
                    completeLogStreams logState
                )
                Ok proc.Id
    )

let start (graphArgs: ParseResults<GraphArgs>) (logEnabled: bool) (debugEnabled: bool) =
    let workspace =
        graphArgs.TryGetResult(CLI.GraphArgs.Workspace)
        |> resolveWorkspace
    let shouldOpenBrowser = graphArgs.Contains(GraphArgs.No_Open) |> not
    let processDir =
        System.Environment.ProcessPath
        |> Option.ofObj
        |> Option.bind (fun path -> Path.GetDirectoryName(path) |> Option.ofObj)
    let defaultUiRoot = Path.Combine(AppContext.BaseDirectory, "ui")
    let uiEnv = System.Environment.GetEnvironmentVariable("TB_UI_ROOT") |> Option.ofObj |> Option.defaultValue defaultUiRoot
    let uiCandidates =
        [
            uiEnv
            defaultUiRoot
            Path.Combine(AppContext.BaseDirectory, "..", "ui")
            Path.Combine(AppContext.BaseDirectory, "..", "share", "terrabuild", "ui")
            Path.Combine(AppContext.BaseDirectory, "..", "lib", "terrabuild", "ui")
        ]
        |> List.append (processDir |> Option.map (fun dir -> Path.Combine(dir, "ui")) |> Option.toList)
        |> List.map (fun path -> Path.GetFullPath(path))
    let port = graphArgs.TryGetResult(GraphArgs.Port) |> Option.defaultValue 5179
    let url = $"http://127.0.0.1:{port}"
    let builder = WebApplication.CreateBuilder()
    builder.Logging.ClearProviders() |> ignore
    if debugEnabled then
        builder.Logging.SetMinimumLevel(LogLevel.Debug) |> ignore
    if logEnabled then
        builder.Logging.AddSerilog(Log.Logger, dispose = false) |> ignore
    builder.Services.AddCors() |> ignore
    builder.WebHost.UseUrls(url) |> ignore

    let app = builder.Build()

    let logState = createLogState()
    let buildState = createBuildState()
    let workspaceLock = obj()

    let embeddedProvider =
        let assembly = System.Reflection.Assembly.GetExecutingAssembly()
        let provider = EmbeddedUiFileProvider.Provider(assembly) :> IFileProvider
        if provider.GetFileInfo("index.html").Exists then
            Some provider
        else
            None

    let physicalProvider =
        uiCandidates
        |> List.tryFind Directory.Exists
        |> Option.map (fun root -> new PhysicalFileProvider(root) :> IFileProvider)

    let fileProvider =
        embeddedProvider
        |> Option.orElse physicalProvider
        |> Option.defaultValue (new Microsoft.Extensions.FileProviders.NullFileProvider() :> IFileProvider)
    let defaultFiles =
        DefaultFilesOptions(
            FileProvider = fileProvider,
            RequestPath = PathString.Empty
        )
    defaultFiles.DefaultFileNames.Clear()
    defaultFiles.DefaultFileNames.Add("index.html")

    app.UseCors(fun policy ->
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
        |> ignore)
    |> ignore

    match fileProvider with
    | :? PhysicalFileProvider ->
        app.UseDefaultFiles(defaultFiles) |> ignore
    | _ -> ()
    app.UseStaticFiles(StaticFileOptions(FileProvider = fileProvider, RequestPath = PathString.Empty)) |> ignore

    app.MapGet("/api/targets", Func<HttpContext, Task<IResult>>(fun _ ->
        task {
            let targets =
                lock workspaceLock (fun () ->
                    let _, config = buildConfig workspace [] None
                    config.Targets |> Map.keys |> Seq.sort |> Seq.toList
                )
            let json = Json.Serialize targets
            return Results.Text(json, "application/json")
        }))
    |> ignore

    app.MapGet("/api/projects", Func<HttpContext, Task<IResult>>(fun _ ->
        task {
            let projects =
                lock workspaceLock (fun () ->
                    let _, config = buildConfig workspace [] None
                    config.Projects
                    |> Map.toList
                    |> List.choose (fun (_, project) ->
                        match project.Name with
                        | Some name ->
                            { ProjectInfo.Id = project.Id
                              ProjectInfo.Name = Some name
                              ProjectInfo.Directory = project.Directory
                              ProjectInfo.Hash = project.Hash }
                            |> Some
                        | None -> None)
                    |> List.sortBy (fun project -> project.Name |> Option.defaultValue project.Id)
                )
            let json = Json.Serialize projects
            return Results.Text(json, "application/json")
        }))
    |> ignore

    app.MapGet("/api/graph", Func<HttpContext, Task<IResult>>(fun ctx ->
        task {
            let targets = ctx.Request.Query.["targets"] |> parseCsvValues
            let projects = ctx.Request.Query.["projects"] |> parseCsvValues |> function | [] -> None | values -> Some values
            if targets.IsEmpty then
                return Results.BadRequest("At least one target is required.")
            else
                let graph, options =
                    lock workspaceLock (fun () ->
                        buildBatchGraph workspace targets projects
                    )
                let payload =
                    {| nodes = graph.Nodes
                       rootNodes = graph.RootNodes
                       batches = graph.Batches
                       engine = options.Engine
                       configuration = options.Configuration
                       environment = options.Environment |}
                let json = Json.Serialize payload
                return Results.Text(json, "application/json")
        }))
    |> ignore

    app.MapGet("/api/build/status", Func<HttpContext, Task<IResult>>(fun ctx ->
        task {
            let targets = ctx.Request.Query.["targets"] |> parseCsvValues
            let projects = ctx.Request.Query.["projects"] |> parseCsvValues |> function | [] -> None | values -> Some values
            if targets.IsEmpty then
                return Results.BadRequest("At least one target is required.")
            else
                let graph, _ =
                    lock workspaceLock (fun () ->
                        buildBatchGraph workspace targets projects
                    )
                let cache = Cache.Cache(Storages.Factory.create None, None) :> Cache.ICache
                let statuses =
                    graph.Nodes
                    |> Map.toSeq
                    |> Seq.choose (fun (_, node) ->
                        let cacheKey = GraphDef.buildCacheKey node
                        match cache.TryGetSummary false cacheKey with
                        | Some summary -> Some (node.ProjectId, summary.IsSuccessful)
                        | _ -> None)
                    |> Seq.groupBy fst
                    |> Seq.map (fun (projectId, results) ->
                        let hasFailure = results |> Seq.exists (fun (_, ok) -> ok = false)
                        let status = if hasFailure then "failed" else "success"
                        { ProjectStatus.ProjectId = projectId; ProjectStatus.Status = status })
                    |> Seq.toList
                let json = Json.Serialize statuses
                return Results.Text(json, "application/json")
        }))
    |> ignore

    app.MapPost("/api/build", Func<HttpContext, Task<IResult>>(fun ctx ->
        task {
            let! body = readBody ctx
            let request =
                try
                    Json.Deserialize<BuildRequest> body |> Ok
                with ex ->
                    Error ex.Message
            match request with
            | Error err -> return Results.BadRequest(err)
            | Ok request ->
                let targets = request.Targets |> Option.defaultValue []
                if targets.IsEmpty then
                    return Results.BadRequest("At least one target is required.")
                else
                    match startBuildProcess workspace request logState buildState with
                    | Ok pid -> return Results.Json {| pid = pid; startedAt = DateTime.UtcNow |}
                    | Error reason -> return Results.Conflict(reason)
        }))
    |> ignore

    app.MapPost("/api/build/command", Func<HttpContext, Task<IResult>>(fun ctx ->
        task {
            let! body = readBody ctx
            let request =
                try
                    Json.Deserialize<BuildRequest> body |> Ok
                with ex ->
                    Error ex.Message
            match request with
            | Error err -> return Results.BadRequest(err)
            | Ok request ->
                let targets = request.Targets |> Option.defaultValue []
                if targets.IsEmpty then
                    return Results.BadRequest("At least one target is required.")
                else
                    let command, args = createBuildCommand workspace request
                    return Results.Text($"{command} {args}", "text/plain")
        }))
    |> ignore

    app.MapGet("/api/build/log", Func<HttpContext, Task>(fun ctx ->
        task {
            ctx.Response.Headers.CacheControl <- "no-cache"
            ctx.Response.Headers.ContentType <- "text/plain"
            let id = Guid.NewGuid()
            let channel = Channel.CreateUnbounded<string>()
            logState.Subscribers.TryAdd(id, channel) |> ignore

            let snapshot = logSnapshot logState
            if String.IsNullOrEmpty(snapshot) |> not then
                let bytes = Encoding.UTF8.GetBytes(snapshot)
                do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length, ctx.RequestAborted)
                do! ctx.Response.Body.FlushAsync(ctx.RequestAborted)

            try
                let reader = channel.Reader
                let mutable keepReading = true
                while keepReading && not ctx.RequestAborted.IsCancellationRequested do
                    let! next = reader.ReadAsync(ctx.RequestAborted)
                    let bytes = Encoding.UTF8.GetBytes(next)
                    do! ctx.Response.Body.WriteAsync(bytes, 0, bytes.Length, ctx.RequestAborted)
                    do! ctx.Response.Body.FlushAsync(ctx.RequestAborted)
            with
            | :? OperationCanceledException -> ()
            | :? ChannelClosedException -> ()

            let mutable removed = Unchecked.defaultof<Channel<string>>
            logState.Subscribers.TryRemove(id, &removed) |> ignore
        }))
    |> ignore

    app.MapGet("/api/build/result/{projectHash}/{targetName}/{targetHash}", Func<HttpContext, Task<IResult>>(fun ctx ->
        let getRouteValue name =
            match ctx.Request.RouteValues.TryGetValue(name) with
            | true, null -> None
            | true, (:? string as value) -> Some value
            | _ -> None

        let result =
            match getRouteValue "projectHash", getRouteValue "targetName", getRouteValue "targetHash" with
            | Some projectHash, Some targetName, Some targetHash ->
                let cacheKey = $"{projectHash}/{targetName}/{targetHash}"
                let cache = Cache.Cache(Storages.Factory.create None, None) :> Cache.ICache
                match cache.TryGetSummary false cacheKey with
                | Some summary ->
                    let json = Json.Serialize summary
                    Results.Text(json, "application/json")
                | None -> Results.NotFound()
            | _ ->
                Results.BadRequest("Missing route values.")
        Task.FromResult(result)))
    |> ignore

    app.MapGet("/api/build/target-log/{projectHash}/{targetName}/{targetHash}", Func<HttpContext, Task<IResult>>(fun ctx ->
        let getRouteValue name =
            match ctx.Request.RouteValues.TryGetValue(name) with
            | true, null -> None
            | true, (:? string as value) -> Some value
            | _ -> None

        let result =
            match getRouteValue "projectHash", getRouteValue "targetName", getRouteValue "targetHash" with
            | Some projectHash, Some targetName, Some targetHash ->
                let cacheKey = $"{projectHash}/{targetName}/{targetHash}"
                let cache = Cache.Cache(Storages.Factory.create None, None) :> Cache.ICache
                match cache.TryGetSummary false cacheKey with
                | Some summary ->
                    let builder = StringBuilder()
                    summary.Operations
                    |> List.collect id
                    |> List.iter (fun step ->
                        if IO.exists step.Log then
                            let content = IO.readTextFile step.Log
                            if String.IsNullOrWhiteSpace(content) |> not then
                                if builder.Length > 0 then builder.AppendLine().AppendLine() |> ignore
                                builder.AppendLine(step.MetaCommand).AppendLine(content) |> ignore
                    )
                    let logText = builder.ToString()
                    Results.Text(logText, "text/plain")
                | None -> Results.NotFound()
            | _ ->
                Results.BadRequest("Missing route values.")
        Task.FromResult(result)))
    |> ignore

    app.MapFallback(Func<HttpContext, Task>(fun ctx ->
        task {
            let indexFile = fileProvider.GetFileInfo("index.html")
            if indexFile.Exists then
                ctx.Response.ContentType <- "text/html"
                use stream = indexFile.CreateReadStream()
                use reader = new StreamReader(stream, Encoding.UTF8)
                let! html = reader.ReadToEndAsync()
                do! ctx.Response.WriteAsync(html)
            else
                ctx.Response.StatusCode <- 404
                do! ctx.Response.WriteAsync("UI assets not found. Set TB_UI_ROOT or build Terrabuild.UI.")
        }))
    |> ignore

    let runTask = app.RunAsync()
    if shouldOpenBrowser then
        openBrowser url
    runTask.Wait()
    0
