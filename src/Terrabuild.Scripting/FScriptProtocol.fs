namespace Terrabuild

open FScript.Extensibility
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

    /// Compiler-only guarantees for our deeply immutable data contracts.
    /// Runtime functions supplied by the host remain conservative.
    let pureTypeNames =
        // Review new types explicitly; registration alone must never imply purity.
        [ "Protocol.ExtensionContext"; "Protocol.BatchContext"; "Protocol.ActionContext"
          "Protocol.ProjectInfo"; "Protocol.ShellOperation"; "Protocol.CommandResult"
          "Protocol.DependencyResolution"; "Protocol.ProvenanceEffect"
          "Protocol.Cacheability"; "Protocol.ExportFlag" ]
        |> List.sort

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
        { protocol with Functions = context.DefaultFunctions |> List.filter (fun fn -> not (blocked.Contains fn.Name)) }


type FScriptProtocolProvider =
    [<FScriptExternProvider>]
    static member Provide(context: HostContext) : ExtensionRegistration = FScriptProtocol.registration context

    // Optional compiler companions use only BCL types. AST interpretation ignores them.
    static member FScriptPureTypes() : System.Collections.Generic.IEnumerable<string> = FScriptProtocol.pureTypeNames
    static member FScriptPureFunctions() : System.Collections.Generic.IEnumerable<string> = Seq.empty
