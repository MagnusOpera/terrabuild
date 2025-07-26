open Argu
open CLI
open System
open Serilog
open Errors
open System.Reflection
open Collections
open Environment
open System.Runtime.InteropServices






[<RequireQualifiedAccess>]
type RunTargetOptions = {
    Workspace: string
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    LocalOnly: bool
    StartedAt: DateTime
    IsLog: bool
    Targets: string set
    Configuration: string option
    Environment: string option
    Note: string option
    Tag: string option
    Labels: string set option
    Projects: string set option
    Variables: Map<string, string>
    ContainerTool: string option
}



let launchDir = currentDir()
Cache.createDirectories()



let rec findWorkspace dir =
    if FS.combinePath dir "WORKSPACE" |> IO.exists then
        Some dir
    else
        dir |> FS.parentDirectory |> Option.bind findWorkspace

let processCommandLine (parser: ArgumentParser<TerrabuildArgs>) (result: ParseResults<TerrabuildArgs>) =
    let debug = result.Contains(TerrabuildArgs.Debug)
    let log = result.Contains(TerrabuildArgs.Log)
    let runId = Guid.NewGuid()

    let logFile name = FS.combinePath launchDir $"terrabuild-debug.{name}"

    if log then
        let loggerBuilder = LoggerConfiguration().WriteTo.File(logFile "log")
        let loggerBuilder =
            if debug then loggerBuilder.MinimumLevel.Debug()
            else loggerBuilder
        Log.Logger <- loggerBuilder.CreateLogger()
        Log.Debug("===== [Execution Start] =====")
        Log.Debug($"Environment: {RuntimeInformation.OSDescription}, {RuntimeInformation.OSArchitecture}, {Environment.Version}")

    let runTarget (options: RunTargetOptions) =
        let logGraph graph name =
            graph
            |> Json.Serialize
            |> IO.writeTextFile (logFile $"{name}-graph.json")
            graph
            |> Mermaid.render None None
            |> String.join "\n"
            |> IO.writeTextFile (logFile $"{name}-graph.mermaid")

        System.Environment.CurrentDirectory <- options.Workspace
        Log.Debug("Changing current directory to {directory}", options.Workspace)
        Log.Debug("ProcessorCount = {procCount}", Environment.ProcessorCount)

        let sourceControl = SourceControls.Factory.create()

        let options = {
            ConfigOptions.Options.Workspace = options.Workspace
            ConfigOptions.Options.WhatIf = options.WhatIf
            ConfigOptions.Options.Debug = options.Debug
            ConfigOptions.Options.MaxConcurrency = options.MaxConcurrency
            ConfigOptions.Options.Force = options.Force
            ConfigOptions.Options.Retry = options.Retry
            ConfigOptions.Options.LocalOnly = options.LocalOnly
            ConfigOptions.Options.StartedAt = options.StartedAt
            ConfigOptions.Options.Targets = options.Targets
            ConfigOptions.Options.LogTypes = sourceControl.LogTypes
            ConfigOptions.Options.Configuration = options.Configuration
            ConfigOptions.Options.Environment = options.Environment
            ConfigOptions.Options.Note = options.Note
            ConfigOptions.Options.Tag = options.Tag
            ConfigOptions.Options.Labels = options.Labels
            ConfigOptions.Options.Projects = options.Projects
            ConfigOptions.Options.Variables = options.Variables
            ConfigOptions.Options.ContainerTool = options.ContainerTool
            ConfigOptions.Options.HeadCommit = sourceControl.HeadCommit
            ConfigOptions.Options.CommitLog = sourceControl.CommitLog
            ConfigOptions.Options.BranchOrTag = sourceControl.BranchOrTag
            ConfigOptions.Options.Run = sourceControl.Run
        }

        if options.Debug then
            let jsonOptions = Json.Serialize options
            jsonOptions |> IO.writeTextFile (logFile "options.json")

        let config = Configuration.read options

        let token =
            if options.LocalOnly then None
            else config.Id |> Option.bind Auth.readAuthToken
        let api = Api.Factory.create config.Id token options
        if api |> Option.isSome then
            Log.Debug("Connected to API")
            $" {Ansi.Styles.green}{Ansi.Emojis.arrow}{Ansi.Styles.reset} Connected to Insights" |> Terminal.writeLine

        if options.Debug then
            let jsonConfig = Json.Serialize config
            jsonConfig |> IO.writeTextFile (logFile "config.json")

        let storage = Storages.Factory.create api
        let cache = Cache.Cache(storage) :> Cache.ICache

        let buildGraph = GraphBuilder.build options config
        if options.Debug then logGraph buildGraph "build"

        if not options.WhatIf then
            let summary =
                let buildNotification = Notification.BuildNotification() :> Build.IBuildNotification            
                let summary = Build.run options cache api buildNotification buildGraph
                buildNotification.WaitCompletion()
                summary

            if options.Debug then
                let jsonBuild = Json.Serialize summary
                jsonBuild |> IO.writeTextFile (logFile "build-result.json")

            if log || not summary.IsSuccess then
                Logs.dumpLogs runId options cache buildGraph summary

            let result =
                if summary.IsSuccess then Ansi.Emojis.happy
                else Ansi.Emojis.sad
            let duration = DateTime.UtcNow - options.StartedAt
            $"{result} Completed in {duration}" |> Terminal.writeLine

            if summary.IsSuccess then 0
            else 5
        else
            let duration = DateTime.UtcNow - options.StartedAt
            $"{Ansi.Emojis.happy} Completed in {duration}" |> Terminal.writeLine
            0

    let scaffold (scaffoldArgs: ParseResults<ScaffoldArgs>) =
        let wsDir = scaffoldArgs.GetResult(ScaffoldArgs.Workspace, defaultValue = ".")
        let force = scaffoldArgs.Contains(ScaffoldArgs.Force)
        Scalffold.scaffold wsDir force
        0

    let run (runArgs: ParseResults<RunArgs>) =
        let wsDir =
            match runArgs.TryGetResult(RunArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match currentDir() |> findWorkspace with
                | Some ws -> ws
                | _ -> raiseInvalidArg "Can't find workspace root directory. Check you are in a workspace."
        let targets = runArgs.GetResult(RunArgs.Target) |> Seq.map String.toLower
        let configuration = runArgs.TryGetResult(RunArgs.Configuration)
        let environment = runArgs.TryGetResult(RunArgs.Environment)
        let note = runArgs.TryGetResult(RunArgs.Note)
        let labels = runArgs.TryGetResult(RunArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let projects = runArgs.TryGetResult(RunArgs.Project) |> Option.map (fun projects -> projects |> Seq.map String.toLower |> Set)
        let variables = runArgs.GetResults(RunArgs.Variable) |> Map
        let maxConcurrency = runArgs.GetResult(RunArgs.Parallel, defaultValue = Environment.ProcessorCount/2) |> max 1
        let localOnly = runArgs.Contains(RunArgs.Local_Only)
        let tag = runArgs.TryGetResult(RunArgs.Tag)
        let whatIf = runArgs.Contains(RunArgs.What_If)
        let containerTool =
            match runArgs.TryGetResult(RunArgs.Container) with
            | Some ContainerTool.Docker -> Some "docker"
            | Some ContainerTool.Podman -> Some "podman"
            | Some ContainerTool.None -> None
            | _ -> Some "docker"

        let options = { RunTargetOptions.Workspace = wsDir |> FS.fullPath
                        RunTargetOptions.WhatIf = whatIf
                        RunTargetOptions.Debug = debug
                        RunTargetOptions.Force = runArgs.Contains(RunArgs.Force)
                        RunTargetOptions.MaxConcurrency = maxConcurrency
                        RunTargetOptions.Retry = runArgs.Contains(RunArgs.Retry)
                        RunTargetOptions.StartedAt = DateTime.UtcNow
                        RunTargetOptions.IsLog = false
                        RunTargetOptions.Targets = Set targets
                        RunTargetOptions.LocalOnly = localOnly
                        RunTargetOptions.Configuration = configuration
                        RunTargetOptions.Environment = environment
                        RunTargetOptions.Note = note
                        RunTargetOptions.Tag = tag
                        RunTargetOptions.Labels = labels
                        RunTargetOptions.Projects = projects
                        RunTargetOptions.Variables = variables
                        RunTargetOptions.ContainerTool = containerTool }
        runTarget options

    let serve (serveArgs: ParseResults<ServeArgs>) =
        let wsDir =
            match serveArgs.TryGetResult(ServeArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match currentDir() |> findWorkspace with
                | Some ws -> ws
                | _ -> raiseInvalidArg "Can't find workspace root directory. Check you are in a workspace."
        let configuration = serveArgs.TryGetResult(ServeArgs.Configuration)
        let environment = serveArgs.TryGetResult(ServeArgs.Environment)
        let labels = serveArgs.TryGetResult(ServeArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let projects = serveArgs.TryGetResult(ServeArgs.Project) |> Option.map (fun projects -> projects |> Seq.map String.toLower |> Set)
        let variables = serveArgs.GetResults(ServeArgs.Variable) |> Map
        let options = { RunTargetOptions.Workspace = wsDir |> FS.fullPath
                        RunTargetOptions.WhatIf = false
                        RunTargetOptions.Debug = debug
                        RunTargetOptions.Force = false
                        RunTargetOptions.MaxConcurrency = Int32.MaxValue
                        RunTargetOptions.Retry = true
                        RunTargetOptions.StartedAt = DateTime.UtcNow
                        RunTargetOptions.IsLog = false
                        RunTargetOptions.Targets = Set [ "serve" ]
                        RunTargetOptions.LocalOnly = true
                        RunTargetOptions.Configuration = configuration
                        RunTargetOptions.Environment = environment
                        RunTargetOptions.Note = None
                        RunTargetOptions.Tag = None
                        RunTargetOptions.Labels = labels
                        RunTargetOptions.Projects = projects
                        RunTargetOptions.Variables = variables
                        RunTargetOptions.ContainerTool = None }
        runTarget options

    let logs (logsArgs: ParseResults<LogsArgs>) =
        let targets = logsArgs.GetResult(LogsArgs.Target) |> Seq.map String.toLower
        let wsDir =
            match logsArgs.TryGetResult(LogsArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match currentDir() |> findWorkspace with
                | Some ws -> ws
                | _ -> raiseInvalidArg "Can't find workspace root directory. Check you are in a workspace."
        let configuration = logsArgs.TryGetResult(LogsArgs.Configuration)
        let environment = logsArgs.TryGetResult(LogsArgs.Environment)
        let labels = logsArgs.TryGetResult(LogsArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let projects = logsArgs.TryGetResult(LogsArgs.Project) |> Option.map (fun projects -> projects |> Seq.map String.toLower |> Set)
        let variables = logsArgs.GetResults(LogsArgs.Variable) |> Map

        let options = { RunTargetOptions.Workspace = wsDir |> FS.fullPath
                        RunTargetOptions.WhatIf = true
                        RunTargetOptions.Debug = debug
                        RunTargetOptions.Force = false
                        RunTargetOptions.MaxConcurrency = 1
                        RunTargetOptions.Retry = false
                        RunTargetOptions.StartedAt = DateTime.UtcNow
                        RunTargetOptions.IsLog = true
                        RunTargetOptions.Targets = Set targets
                        RunTargetOptions.LocalOnly = true 
                        RunTargetOptions.Configuration = configuration
                        RunTargetOptions.Environment = environment
                        RunTargetOptions.Note = None
                        RunTargetOptions.Tag = None
                        RunTargetOptions.Labels = labels
                        RunTargetOptions.Projects = projects
                        RunTargetOptions.Variables = variables
                        RunTargetOptions.ContainerTool = None }
        runTarget options

    let clear (clearArgs: ParseResults<ClearArgs>) =
        if clearArgs.Contains(ClearArgs.Cache) || clearArgs.Contains(ClearArgs.All) then Cache.clearCache()
        if clearArgs.Contains(ClearArgs.Home) || clearArgs.Contains(ClearArgs.All) then Cache.clearHomeCache()
        0

    let login (loginArgs: ParseResults<LoginArgs>) =
        let workspaceId = loginArgs.GetResult(LoginArgs.Workspace)
        let token = loginArgs.GetResult(LoginArgs.Token)

        Auth.login workspaceId token
        0

    let logout (logoutArgs: ParseResults<LogoutArgs>) =
        let workspaceId = logoutArgs.GetResult(LogoutArgs.Space)
        Auth.logout workspaceId
        0

    let version () =
        let version =
            match Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>() with
            | null -> "0.0.0"
            | fileVersion -> fileVersion.InformationalVersion
        printfn $"Terrabuild v{version}"
        0
 
    Log.Debug("Parsing command line")
    match result with
    | p when p.Contains(TerrabuildArgs.Scaffold) -> p.GetResult(TerrabuildArgs.Scaffold) |> scaffold
    | p when p.Contains(TerrabuildArgs.Logs) -> p.GetResult(TerrabuildArgs.Logs) |> logs
    | p when p.Contains(TerrabuildArgs.Run) -> p.GetResult(TerrabuildArgs.Run) |> run
    | p when p.Contains(TerrabuildArgs.Serve) -> p.GetResult(TerrabuildArgs.Serve) |> serve
    | p when p.Contains(TerrabuildArgs.Clear) -> p.GetResult(TerrabuildArgs.Clear) |> clear
    | p when p.Contains(TerrabuildArgs.Login) -> p.GetResult(TerrabuildArgs.Login) |> login
    | p when p.Contains(TerrabuildArgs.Logout) -> p.GetResult(TerrabuildArgs.Logout) |> logout
    | p when p.Contains(TerrabuildArgs.Version) -> version()
    | _ ->
        Log.Debug("Failed to parse {result}", result)
        parser.PrintUsage() |> Terminal.writeLine; 0

[<EntryPoint>]
let main _ =

#if RELEASE
    let sentryDsn =
        match "TERRABUILD_SENTRY_DSN" |> envVar with
        | Some dsn -> dsn
        | _ -> "https://9d7ab9713b1dfca7abe4437bcd73718a@o4508921459834880.ingest.de.sentry.io/4508921463898192"

    // Sentry can be disabled (empty DSN)
    if String.IsNullOrWhiteSpace(sentryDsn) |> not then
        Sentry.SentrySdk.Init(fun options ->
            options.Dsn <- sentryDsn
            options.AutoSessionTracking <- true
            options.TracesSampleRate <- 1.0
            options.StackTraceMode <- Sentry.StackTraceMode.Enhanced
            options.CaptureFailedRequests <- true
        ) |> ignore
#endif

    let mutable debug = false
    let retCode =
        try
            DotNetEnv.Env.TraversePath().Load() |> ignore
            Terminal.hideCursor()
            Console.CancelKeyPress.Add (fun _ -> $"{Ansi.Emojis.bolt} Aborted{Ansi.Styles.cursorShow}" |> Terminal.writeLine)
            let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild")
            let result = parser.ParseCommandLine()
            debug <- result.Contains(TerrabuildArgs.Debug)
            processCommandLine parser result
        with
            | :? ArguParseException as exn ->
                Log.Fatal(exn, "ArgumentParser error with {Exception}")
                exn.Message |> Terminal.writeLine
                if exn.ErrorCode = ErrorCode.HelpText then 0
                else 5

            | :? TerrabuildException as ex ->
                let area = getErrorArea ex
                ex.AddSentryTag("area", $"{area}")
#if RELEASE
                let captureException =
                    match area with
                    | ErrorArea.External
                    | ErrorArea.Bug -> true
                    | _ -> false
                if captureException then Sentry.SentrySdk.CaptureException(ex) |> ignore
#endif
                Log.Fatal("Failed in area {Area} with {Exception}", area, ex.ToString())
                let reason =
                    if debug then $"[{area}] {ex}"
                    else dumpKnownException ex |> String.join "\n   "
                $"{Ansi.Emojis.explosion} {reason}" |> Terminal.writeLine
                5

            | ex ->
#if RELEASE
                Sentry.SentrySdk.CaptureException(ex) |> ignore
#endif
                Log.Fatal("Failed with {Exception}", ex)
                $"{Ansi.Emojis.explosion} {ex}" |> Terminal.writeLine
                5

    System.Environment.CurrentDirectory <- launchDir
    Terminal.showCursor()
    Log.Debug("===== [Execution End] =====")
    retCode
