namespace Terrabuild

open FScript.Language
open FScript.Runtime
open ScriptingContracts

/// One protocol for execution, built-in/custom extensions, DocGen, and editor analysis.
module FScriptProtocol =
    let private protocol =
        { HostInterop.empty with
            Types =
                [ HostTypes.describe<ExtensionContext>(); HostTypes.describe<BatchContext>()
                  HostTypes.describe<ActionContext>(); HostTypes.describe<ProjectInfo>()
                  HostTypes.describe<ShellOperation>(); HostTypes.describe<CommandResult>() ]
            Unions =
                [ HostTypes.describeUnion<DependencyResolution>(); HostTypes.describeUnion<ProvenanceEffect>()
                  HostTypes.describeUnion<Cacheability>(); HostTypes.describeUnion<ExportFlag>() ] }

    /// Stable schema fingerprint, independent of assembly build identity.
    let fingerprint =
        HostInterop.typeDefinitions protocol []
        |> Map.toList
        |> List.map (fun (name, definition) ->
            let shape =
                match definition with
                | TUnion(_, cases) ->
                    cases |> Map.toList
                    |> List.map (fun (caseName, payload) -> caseName + ":" + (payload |> Option.map (fun ty -> "of " + Types.typeToString ty) |> Option.defaultValue "nullary"))
                    |> String.concat "|"
                | other -> Types.typeToString other
            name + ":" + shape)
        |> String.concat "\n"

    let registration (context: HostContext) =
        let blocked = set ["Task.spawn"; "Task.await"; "Console.readLine"]
        { protocol with Functions = Registry.all context |> List.filter (fun fn -> not (blocked.Contains fn.Name)) }

type FScriptProtocolProvider =
    [<FScriptExternProvider>]
    static member Provide(context: HostContext) : ExtensionRegistration = FScriptProtocol.registration context
