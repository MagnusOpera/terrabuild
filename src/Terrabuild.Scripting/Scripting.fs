module Terrabuild.Scripting

open System
open System.IO
open System.Diagnostics
open System.Threading
open System.Collections.Concurrent
open Microsoft.FSharp.Reflection
open Terrabuild.ScriptingContracts
open Terrabuild.Expression
open Errors

type private RuntimeInvoker = Terrabuild.Expression.Value -> System.Type -> objnull

type PerformanceSnapshot =
    { RuntimeInvokeCount: int64
      RuntimeInvokeDurationMs: float
      ScriptInvokeCount: int64
      ScriptInvokeDurationMs: float
      ToFScriptConversionCount: int64
      ToFScriptConversionDurationMs: float
      FromFScriptConversionCount: int64
      FromFScriptConversionDurationMs: float
      MethodResolutionCount: int64
      MethodResolutionDurationMs: float
      ScriptLoadCount: int64
      ScriptLoadDurationMs: float
      ScriptCacheHitCount: int64
      ScriptFunctionBreakdown: (string * int64 * float) list }

module private Performance =
    let mutable private runtimeInvokeCount = 0L
    let mutable private runtimeInvokeTicks = 0L
    let mutable private toFScriptCount = 0L
    let mutable private toFScriptTicks = 0L
    let mutable private fromFScriptCount = 0L
    let mutable private fromFScriptTicks = 0L
    let mutable private scriptInvokeCount = 0L
    let mutable private scriptInvokeTicks = 0L
    let mutable private methodResolutionCount = 0L
    let mutable private methodResolutionTicks = 0L
    let mutable private scriptLoadCount = 0L
    let mutable private scriptLoadTicks = 0L
    let mutable private scriptCacheHitCount = 0L
    let private scriptFunctionStats = ConcurrentDictionary<string, struct(int64 * int64)>()

    let inline private toMs (ticks: int64) =
        (float ticks * 1000.0) / float Stopwatch.Frequency

    let reset () =
        runtimeInvokeCount <- 0L
        runtimeInvokeTicks <- 0L
        toFScriptCount <- 0L
        toFScriptTicks <- 0L
        fromFScriptCount <- 0L
        fromFScriptTicks <- 0L
        scriptInvokeCount <- 0L
        scriptInvokeTicks <- 0L
        methodResolutionCount <- 0L
        methodResolutionTicks <- 0L
        scriptLoadCount <- 0L
        scriptLoadTicks <- 0L
        scriptCacheHitCount <- 0L
        scriptFunctionStats.Clear()

    let trackRuntimeInvoke (ticks: int64) =
        Interlocked.Increment(&runtimeInvokeCount) |> ignore
        Interlocked.Add(&runtimeInvokeTicks, ticks) |> ignore

    let trackToFScriptConversion (ticks: int64) =
        Interlocked.Increment(&toFScriptCount) |> ignore
        Interlocked.Add(&toFScriptTicks, ticks) |> ignore

    let trackFromFScriptConversion (ticks: int64) =
        Interlocked.Increment(&fromFScriptCount) |> ignore
        Interlocked.Add(&fromFScriptTicks, ticks) |> ignore

    let trackScriptInvoke (functionId: string) (ticks: int64) =
        Interlocked.Increment(&scriptInvokeCount) |> ignore
        Interlocked.Add(&scriptInvokeTicks, ticks) |> ignore
        scriptFunctionStats.AddOrUpdate(
            functionId,
            struct (1L, ticks),
            Func<_, _, _>(fun _ (struct (count, totalTicks)) -> struct (count + 1L, totalTicks + ticks)))
        |> ignore

    let trackMethodResolution (ticks: int64) =
        Interlocked.Increment(&methodResolutionCount) |> ignore
        Interlocked.Add(&methodResolutionTicks, ticks) |> ignore

    let trackScriptLoad (ticks: int64) =
        Interlocked.Increment(&scriptLoadCount) |> ignore
        Interlocked.Add(&scriptLoadTicks, ticks) |> ignore

    let trackScriptCacheHit () =
        Interlocked.Increment(&scriptCacheHitCount) |> ignore

    let snapshot () =
        let functionBreakdown =
            scriptFunctionStats
            |> Seq.map (fun pair ->
                let name = pair.Key
                let struct (count, ticks) = pair.Value
                name, count, toMs ticks)
            |> Seq.sortByDescending (fun (_, _, ms) -> ms)
            |> Seq.truncate 20
            |> List.ofSeq

        { RuntimeInvokeCount = Interlocked.Read(&runtimeInvokeCount)
          RuntimeInvokeDurationMs = Interlocked.Read(&runtimeInvokeTicks) |> toMs
          ScriptInvokeCount = Interlocked.Read(&scriptInvokeCount)
          ScriptInvokeDurationMs = Interlocked.Read(&scriptInvokeTicks) |> toMs
          ToFScriptConversionCount = Interlocked.Read(&toFScriptCount)
          ToFScriptConversionDurationMs = Interlocked.Read(&toFScriptTicks) |> toMs
          FromFScriptConversionCount = Interlocked.Read(&fromFScriptCount)
          FromFScriptConversionDurationMs = Interlocked.Read(&fromFScriptTicks) |> toMs
          MethodResolutionCount = Interlocked.Read(&methodResolutionCount)
          MethodResolutionDurationMs = Interlocked.Read(&methodResolutionTicks) |> toMs
          ScriptLoadCount = Interlocked.Read(&scriptLoadCount)
          ScriptLoadDurationMs = Interlocked.Read(&scriptLoadTicks) |> toMs
          ScriptCacheHitCount = Interlocked.Read(&scriptCacheHitCount)
          ScriptFunctionBreakdown = functionBreakdown }

let resetPerformanceMetrics () = Performance.reset()
let getPerformanceSnapshot () = Performance.snapshot()

type Invocable private (runtimeInvoker: RuntimeInvoker) =
    member _.Invoke<'t>(value: Terrabuild.Expression.Value) =
        runtimeInvoker value typeof<'t> :?> 't

    static member internal FromRuntime(runtimeInvoke: RuntimeInvoker) =
        Invocable(runtimeInvoke)

type Script internal
    (
        scriptId: string,
        loaded: FScript.Runtime.ScriptHost.LoadedScript,
        exportedFunctions: Set<string>,
        descriptor: ScriptDescriptor,
        dispatchMethod: string option,
        defaultMethod: string option
    ) =
    let methodCache = ConcurrentDictionary<string, Invocable option>(StringComparer.Ordinal)
    let commandResolutionCache = ConcurrentDictionary<string, string option>(StringComparer.Ordinal)
    let mutable defaultResolutionCache: string option option = None

    member _.GetMethod(name: string) =
        methodCache.GetOrAdd(
            name,
            Func<_, _>(fun methodName ->
                let startedAt = Stopwatch.GetTimestamp()
                let resolved =
                    if exportedFunctions |> Set.contains methodName then
                        let signature =
                            match loaded.ExportedFunctionSignatures |> Map.tryFind methodName with
                            | Some value -> value
                            | None -> raiseInvalidArg $"Missing signature metadata for exported function '{methodName}'"

                        let parameterPlan =
                            match signature.ParameterNames, signature.ParameterTypes with
                            | contextParam :: remainingParams, contextType :: remainingTypes ->
                                if contextParam <> "context" then
                                    raiseInvalidArg $"Exported function '{methodName}' must declare 'context' as first parameter"
                                contextType, List.zip remainingParams remainingTypes
                            | _ ->
                                raiseInvalidArg $"Exported function '{methodName}' must declare 'context' as first parameter"

                        let toFScriptArgument (parameterType: FScript.Language.Type) (value: Terrabuild.Expression.Value) =
                            match parameterType with
                            | FScript.Language.TOption _ ->
                                match value with
                                | Terrabuild.Expression.Value.Nothing -> FScript.Language.VOption None
                                | _ ->
                                    match Conversions.ToFScriptValue value with
                                    | FScript.Language.VOption optionValue -> FScript.Language.VOption optionValue
                                    | converted -> FScript.Language.VOption (Some converted)
                            | _ ->
                                Conversions.ToFScriptValue value

                        let runtimeInvoke (args: Terrabuild.Expression.Value) (targetType: System.Type) =
                            let invokeStartedAt = Stopwatch.GetTimestamp()
                            let inputMap =
                                match args with
                                | Terrabuild.Expression.Value.Map map -> map
                                | _ -> raiseTypeError $"FScript function '{methodName}' expects map-based arguments"

                            let contextType, remainingPlan = parameterPlan
                            let contextArg =
                                match inputMap |> Map.tryFind "context" with
                                | Some value -> toFScriptArgument contextType value
                                | None -> raiseTypeError $"Missing required argument 'context' for function '{methodName}'"

                            let remainingArgs =
                                remainingPlan
                                |> List.map (fun (parameterName, parameterType) ->
                                    match inputMap |> Map.tryFind parameterName with
                                    | Some value -> toFScriptArgument parameterType value
                                    | None ->
                                        match parameterType with
                                        | FScript.Language.TOption _ -> FScript.Language.VOption None
                                        | _ -> raiseTypeError $"Missing required argument '{parameterName}' for function '{methodName}'")

                            let scriptInvokeStartedAt = Stopwatch.GetTimestamp()
                            let result = FScript.Runtime.ScriptHost.invoke loaded methodName (contextArg :: remainingArgs)
                            Performance.trackScriptInvoke $"{scriptId}:{methodName}" (Stopwatch.GetTimestamp() - scriptInvokeStartedAt)
                            let mappedResult = Conversions.FromFScriptValue(targetType, result)
                            Performance.trackRuntimeInvoke(Stopwatch.GetTimestamp() - invokeStartedAt)
                            mappedResult

                        Invocable.FromRuntime(runtimeInvoke) |> Some
                    else
                        None
                Performance.trackMethodResolution(Stopwatch.GetTimestamp() - startedAt)
                resolved
            )
        )

    member _.ResolveCommandMethod(command: string) =
        commandResolutionCache.GetOrAdd(
            command,
            Func<_, _>(fun commandName ->
                if exportedFunctions |> Set.contains commandName then Some commandName
                else dispatchMethod
            )
        )

    member _.ResolveDefaultMethod() =
        match defaultResolutionCache with
        | Some methodName -> methodName
        | None ->
            let resolved = defaultMethod
            defaultResolutionCache <- Some resolved
            resolved

    member _.TryGetFunctionFlags(name: string) =
        if exportedFunctions |> Set.contains name then
            descriptor |> Map.tryFind name |> Option.defaultValue [] |> Some
        else
            None

and private Descriptor =
    static member private normalizeCase (name: string) = name.Trim().ToLowerInvariant()

    static member private parseCacheabilityName (name: string) =
        match Descriptor.normalizeCase name with
        | "never" -> Some Cacheability.Never
        | "local" -> Some Cacheability.Local
        | "external" -> Some Cacheability.External
        | "remote" -> Some Cacheability.Remote
        | _ -> None

    static member private parseFlag (value: FScript.Language.Value) =
        match value with
        | FScript.Language.VUnionCase(_, caseName, payload) ->
            match Descriptor.normalizeCase caseName, payload with
            | "dispatch", None -> Some ExportFlag.Dispatch
            | "default", None -> Some ExportFlag.Default
            | "cache", Some (FScript.Language.VUnionCase(_, cacheCase, None)) ->
                Descriptor.parseCacheabilityName cacheCase |> Option.map ExportFlag.Cache
            | "cache", Some (FScript.Language.VString cacheName) ->
                Descriptor.parseCacheabilityName cacheName |> Option.map ExportFlag.Cache
            | cacheName, None ->
                Descriptor.parseCacheabilityName cacheName |> Option.map ExportFlag.Cache
            | _ ->
                None
        | _ ->
            None

    static member Parse(exports: string list, value: FScript.Language.Value) =
        let exported = exports |> Set.ofList
        let rawMap: Map<string, FScript.Language.Value> =
            match value with
            | FScript.Language.VMap map ->
                map
                |> Map.toList
                |> List.map (fun (key, value) ->
                    match key with
                    | FScript.Language.MKString name -> name, value
                    | FScript.Language.MKInt _ -> raiseInvalidArg "Descriptor map keys must be strings")
                |> Map.ofList
            | FScript.Language.VRecord map -> map
            | _ -> raiseInvalidArg "FScript extension descriptor must be a map from function name to list of flags"

        rawMap
        |> Map.map (fun functionName flagsValue ->
            if exported |> Set.contains functionName |> not then
                raiseInvalidArg $"Descriptor references '{functionName}' which is not an exported function"
            match flagsValue with
            | FScript.Language.VList flagValues ->
                flagValues
                |> List.map (fun flagValue ->
                    match Descriptor.parseFlag flagValue with
                    | Some flag -> flag
                    | None -> raiseInvalidArg $"Unsupported export flag for function '{functionName}'. Flags must be discriminated union cases")
            | _ ->
                raiseInvalidArg $"Descriptor entry for '{functionName}' must be a list of flags")

    static member ResolveDispatchAndDefault(descriptor: ScriptDescriptor) =
        let dispatchMethods =
            descriptor
            |> Map.toList
            |> List.choose (fun (name, flags) -> if flags |> List.contains ExportFlag.Dispatch then Some name else None)

        let defaultMethods =
            descriptor
            |> Map.toList
            |> List.choose (fun (name, flags) -> if flags |> List.contains ExportFlag.Default then Some name else None)

        match dispatchMethods with
        | _ :: _ :: _ -> raiseInvalidArg "Only one function can be flagged as Dispatch"
        | _ -> ()

        match defaultMethods with
        | _ :: _ :: _ -> raiseInvalidArg "Only one function can be flagged as Default"
        | _ -> ()

        dispatchMethods |> List.tryHead, defaultMethods |> List.tryHead

and private Conversions =
    static let objectConverters = ConcurrentDictionary<System.Type, objnull -> FScript.Language.Value>()
    static let returnDecoders = ConcurrentDictionary<System.Type, FScript.Language.Value -> objnull>()

    static member private toFScriptCoreValue(value: Terrabuild.Expression.Value) =
        let rec convert value =
            match value with
            | Terrabuild.Expression.Value.Nothing -> FScript.Language.VOption None
            | Terrabuild.Expression.Value.Bool boolValue -> FScript.Language.VBool boolValue
            | Terrabuild.Expression.Value.String stringValue -> FScript.Language.VString stringValue
            | Terrabuild.Expression.Value.Number numberValue -> FScript.Language.VInt (int64 numberValue)
            | Terrabuild.Expression.Value.Enum enumValue -> FScript.Language.VString enumValue
            | Terrabuild.Expression.Value.Map mapValue ->
                mapValue
                |> Map.toList
                |> List.map (fun (key, itemValue) -> FScript.Language.MKString key, convert itemValue)
                |> Map.ofList
                |> FScript.Language.VMap
            | Terrabuild.Expression.Value.List listValue -> listValue |> List.map convert |> FScript.Language.VList
            | Terrabuild.Expression.Value.Object objectValue -> Conversions.toFScriptValueFromObject objectValue
        convert value

    static member private buildObjectConverter(valueType: System.Type) =
        if valueType = typeof<string> then
            fun (objValue: objnull) ->
                match objValue with
                | :? string as str -> FScript.Language.VString str
                | _ -> raiseTypeError "Expected string object value"
        elif valueType = typeof<bool> then
            fun (objValue: objnull) -> FScript.Language.VBool (objValue :?> bool)
        elif valueType = typeof<int> then
            fun (objValue: objnull) -> FScript.Language.VInt (int64 (objValue :?> int))
        elif valueType = typeof<int64> then
            fun (objValue: objnull) -> FScript.Language.VInt (objValue :?> int64)
        elif valueType = typeof<float> then
            fun (objValue: objnull) -> FScript.Language.VFloat (objValue :?> float)
        elif valueType = typeof<double> then
            fun (objValue: objnull) -> FScript.Language.VFloat (objValue :?> double)
        elif FSharpType.IsRecord(valueType, true) then
            let fields = FSharpType.GetRecordFields(valueType)
            let fieldConverters = fields |> Array.map (fun fieldInfo -> fieldInfo.Name, Conversions.getObjectConverter fieldInfo.PropertyType)
            fun (objValue: objnull) ->
                let values = FSharpValue.GetRecordFields(nonNull objValue)
                let mapped =
                    Array.zip fieldConverters values
                    |> Array.map (fun ((fieldName, fieldConverter), fieldValue) -> fieldName, fieldConverter fieldValue)
                    |> Map.ofArray
                FScript.Language.VRecord mapped
        elif FSharpType.IsUnion(valueType, true) && valueType.IsGenericType && valueType.GetGenericTypeDefinition() = typedefof<option<_>> then
            let valueConverter = valueType.GetGenericArguments()[0] |> Conversions.getObjectConverter
            fun (objValue: objnull) ->
                if isNull objValue then
                    FScript.Language.VOption None
                else
                    let unionCase, fields = FSharpValue.GetUnionFields(nonNull objValue, valueType)
                    match unionCase.Name, fields with
                    | "None", _ -> FScript.Language.VOption None
                    | "Some", [| item |] -> FScript.Language.VOption (Some (valueConverter item))
                    | _ -> raiseTypeError $"Unsupported option value '{unionCase.Name}'"
        elif FSharpType.IsUnion(valueType, true)
             && (not valueType.IsGenericType || valueType.GetGenericTypeDefinition() <> typedefof<list<_>>) then
            let unionCases = FSharpType.GetUnionCases(valueType)
            if unionCases |> Array.exists (fun unionCase -> unionCase.GetFields().Length <> 0) then
                raiseTypeError $"Unsupported script argument type '{valueType.FullName}'"
            fun (objValue: objnull) ->
                let unionCase, fields = FSharpValue.GetUnionFields(nonNull objValue, valueType)
                match fields with
                | [| |] -> FScript.Language.VUnionCase(valueType.Name, unionCase.Name, None)
                | _ -> raiseTypeError $"Unsupported union value '{unionCase.Name}'"
        elif valueType.IsGenericType && valueType.GetGenericTypeDefinition() = typedefof<list<_>> then
            let elementConverter = valueType.GetGenericArguments()[0] |> Conversions.getObjectConverter
            fun (objValue: objnull) ->
                let items =
                    match objValue with
                    | :? System.Collections.IEnumerable as enumerable ->
                        enumerable
                        |> Seq.cast<obj>
                        |> Seq.map elementConverter
                        |> Seq.toList
                    | _ -> raiseTypeError $"Unsupported list source type '{valueType.FullName}'"
                FScript.Language.VList items
        elif valueType.IsGenericType && valueType.GetGenericTypeDefinition() = typedefof<Set<_>> then
            let elementConverter = valueType.GetGenericArguments()[0] |> Conversions.getObjectConverter
            fun (objValue: objnull) ->
                let items =
                    match objValue with
                    | :? System.Collections.IEnumerable as enumerable ->
                        enumerable
                        |> Seq.cast<obj>
                        |> Seq.map elementConverter
                        |> Seq.toList
                    | _ -> raiseTypeError $"Unsupported set source type '{valueType.FullName}'"
                FScript.Language.VList items
        else
            raiseTypeError $"Unsupported object parameter type '{valueType.FullName}'"

    static member private getObjectConverter(valueType: System.Type) =
        objectConverters.GetOrAdd(valueType, Func<_, _>(fun currentType -> Conversions.buildObjectConverter currentType))

    static member private toFScriptValueFromObject (value: objnull) =
        if isNull value then
            FScript.Language.VOption None
        else
            let converter = value.GetType() |> Conversions.getObjectConverter
            converter value

    static member ToFScriptValue(value: Terrabuild.Expression.Value) =
        let startedAt = Stopwatch.GetTimestamp()
        let converted = Conversions.toFScriptCoreValue value
        Performance.trackToFScriptConversion(Stopwatch.GetTimestamp() - startedAt)
        converted

    static member private toTerrabuildValue(value: FScript.Language.Value) =
        let rec convert (currentValue: FScript.Language.Value) =
            match currentValue with
            | FScript.Language.VUnit -> Terrabuild.Expression.Value.Nothing
            | FScript.Language.VBool boolValue -> Terrabuild.Expression.Value.Bool boolValue
            | FScript.Language.VInt intValue -> Terrabuild.Expression.Value.Number (int intValue)
            | FScript.Language.VString stringValue -> Terrabuild.Expression.Value.String stringValue
            | FScript.Language.VList listValue -> listValue |> List.map convert |> Terrabuild.Expression.Value.List
            | FScript.Language.VRecord mapValue -> mapValue |> Map.map (fun _ itemValue -> convert itemValue) |> Terrabuild.Expression.Value.Map
            | FScript.Language.VMap mapValue ->
                mapValue
                |> Map.toList
                |> List.map (fun (key, itemValue) ->
                    match key with
                    | FScript.Language.MKString name -> name, convert itemValue
                    | FScript.Language.MKInt _ -> raiseTypeError "Terrabuild map values expect string keys")
                |> Map.ofList
                |> Terrabuild.Expression.Value.Map
            | FScript.Language.VOption None -> Terrabuild.Expression.Value.Nothing
            | FScript.Language.VOption (Some optionValue) -> convert optionValue
            | _ -> raiseTypeError $"Unsupported FScript value '{currentValue}' for Terrabuild.Expression.Value"
        convert value

    static member private buildFScriptDecoder(targetType: System.Type) =
        if targetType = typeof<string> then
            fun value ->
                match value with
                | FScript.Language.VString stringValue -> box stringValue
                | _ -> raiseTypeError $"Expected string return type, got {value}"
        elif targetType = typeof<int> then
            fun value ->
                match value with
                | FScript.Language.VInt intValue -> box (int intValue)
                | _ -> raiseTypeError $"Expected int return type, got {value}"
        elif targetType = typeof<bool> then
            fun value ->
                match value with
                | FScript.Language.VBool boolValue -> box boolValue
                | _ -> raiseTypeError $"Expected bool return type, got {value}"
        elif targetType = typeof<float> || targetType = typeof<double> then
            fun value ->
                match value with
                | FScript.Language.VFloat floatValue -> box floatValue
                | _ -> raiseTypeError $"Expected float return type, got {value}"
        elif targetType.IsGenericType && targetType.GetGenericTypeDefinition() = typedefof<option<_>> then
            let innerType = targetType.GetGenericArguments()[0]
            let innerDecoder = Conversions.getFScriptDecoder innerType
            let unionCases = FSharpType.GetUnionCases(targetType)
            let noneCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "None")
            let someCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "Some")
            fun value ->
                match value with
                | FScript.Language.VOption None -> FSharpValue.MakeUnion(noneCase, [| |])
                | FScript.Language.VOption (Some item) -> FSharpValue.MakeUnion(someCase, [| innerDecoder item |])
                | _ -> raiseTypeError $"Expected option return type, got {value}"
        elif targetType.IsGenericType && targetType.GetGenericTypeDefinition() = typedefof<list<_>> then
            let innerType = targetType.GetGenericArguments()[0]
            let innerDecoder = Conversions.getFScriptDecoder innerType
            let unionCases = FSharpType.GetUnionCases(targetType)
            let nilCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "Empty")
            let consCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "Cons")
            let empty = FSharpValue.MakeUnion(nilCase, [| |])
            fun value ->
                match value with
                | FScript.Language.VList items ->
                    items
                    |> List.map innerDecoder
                    |> List.rev
                    |> List.fold (fun state item -> FSharpValue.MakeUnion(consCase, [| item; state |])) empty
                | _ -> raiseTypeError $"Expected list return type, got {value}"
        elif targetType.IsGenericType && targetType.GetGenericTypeDefinition() = typedefof<Set<_>> then
            let innerType = targetType.GetGenericArguments()[0]
            let innerDecoder = Conversions.getFScriptDecoder innerType
            let setModuleType =
                match typeof<Set<string>>.Assembly.GetType("Microsoft.FSharp.Collections.SetModule") with
                | null -> raiseBugError "Cannot resolve FSharp SetModule type"
                | value -> value
            let ofSeqMethod = setModuleType.GetMethods() |> Array.find (fun methodInfo -> methodInfo.Name = "OfSeq")
            let genericOfSeq = ofSeqMethod.MakeGenericMethod([| innerType |])
            fun value ->
                let values: obj =
                    match value with
                    | FScript.Language.VList items ->
                        let array = System.Array.CreateInstance(innerType, items.Length)
                        items |> List.iteri (fun index item -> array.SetValue(innerDecoder item, index))
                        array
                    | _ -> raiseTypeError $"Expected list return type to decode set, got {value}"
                genericOfSeq.Invoke(null, [| values |])
        elif FSharpType.IsUnion(targetType, true) then
            let unionCases = FSharpType.GetUnionCases(targetType)
            if unionCases |> Array.exists (fun unionCase -> unionCase.GetFields().Length <> 0) then
                raiseTypeError $"Unsupported script return type '{targetType.FullName}'"
            fun value ->
                match value with
                | FScript.Language.VUnionCase(_, caseName, None) ->
                    let unionCase =
                        unionCases
                        |> Array.tryFind (fun item -> String.Equals(item.Name, caseName, StringComparison.OrdinalIgnoreCase))
                        |> Option.defaultWith (fun () -> raiseTypeError $"Unknown union case '{caseName}' for '{targetType.FullName}'")
                    FSharpValue.MakeUnion(unionCase, [| |])
                | FScript.Language.VString caseName ->
                    let unionCase =
                        unionCases
                        |> Array.tryFind (fun item -> String.Equals(item.Name, caseName, StringComparison.OrdinalIgnoreCase))
                        |> Option.defaultWith (fun () -> raiseTypeError $"Unknown union case '{caseName}' for '{targetType.FullName}'")
                    FSharpValue.MakeUnion(unionCase, [| |])
                | _ -> raiseTypeError $"Expected union return type, got {value}"
        elif FSharpType.IsRecord(targetType, true) then
            let fields = FSharpType.GetRecordFields(targetType)
            let fieldDecoders = fields |> Array.map (fun fieldInfo -> fieldInfo.Name, Conversions.getFScriptDecoder fieldInfo.PropertyType)
            fun value ->
                let sourceMap =
                    match value with
                    | FScript.Language.VRecord map -> map
                    | FScript.Language.VMap map ->
                        map
                        |> Map.toList
                        |> List.map (fun (key, itemValue) ->
                            match key with
                            | FScript.Language.MKString name -> name, itemValue
                            | FScript.Language.MKInt _ -> raiseTypeError "Record decoding expects string map keys")
                        |> Map.ofList
                    | _ -> raiseTypeError $"Expected record return type, got {value}"
                let fieldValues =
                    fieldDecoders
                    |> Array.map (fun (fieldName, fieldDecoder) ->
                        match sourceMap |> Map.tryFind fieldName with
                        | Some fieldValue -> fieldDecoder fieldValue
                        | None -> raiseTypeError $"Missing field '{fieldName}' in script result")
                FSharpValue.MakeRecord(targetType, fieldValues)
        elif targetType = typeof<Terrabuild.Expression.Value> then
            fun value -> box (Conversions.toTerrabuildValue value)
        else
            raiseTypeError $"Unsupported script return type '{targetType.FullName}'"

    static member private getFScriptDecoder(targetType: System.Type) =
        returnDecoders.GetOrAdd(targetType, Func<_, _>(fun currentType -> Conversions.buildFScriptDecoder currentType))

    static member FromFScriptValue(targetType: System.Type, value: FScript.Language.Value) =
        let startedAt = Stopwatch.GetTimestamp()
        let decoded = (Conversions.getFScriptDecoder targetType) value
        Performance.trackFromFScriptConversion(Stopwatch.GetTimestamp() - startedAt)
        decoded

let mutable private cache = Map.empty<string, Script>
let private loadLock = obj ()

let private toFScriptScript (scriptIdentity: string) (loaded: FScript.Runtime.ScriptHost.LoadedScript) =
    loaded.ExportedFunctionNames
    |> List.iter (fun functionName ->
        match loaded.ExportedFunctionSignatures |> Map.tryFind functionName with
        | Some signature ->
            match signature.ParameterNames with
            | "context" :: _ -> ()
            | _ -> raiseInvalidArg $"Exported function '{functionName}' must declare 'context' as first parameter"
        | None ->
            raiseInvalidArg $"Missing signature metadata for exported function '{functionName}'")
    let descriptor = Descriptor.Parse(loaded.ExportedFunctionNames, loaded.LastValue)
    let dispatchMethod, defaultMethod = Descriptor.ResolveDispatchAndDefault descriptor
    let exportedFunctions = loaded.ExportedFunctionNames |> Set.ofList
    Script(scriptIdentity, loaded, exportedFunctions, descriptor, dispatchMethod, defaultMethod)

let private toFScriptStringLiteral (value: string) =
    let escaped =
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
    $"\"{escaped}\""

let private prependEnvironmentBinding (scriptName: string option) (arguments: string list) (source: string) =
    let scriptNameLiteral =
        match scriptName with
        | Some value -> $"Some {toFScriptStringLiteral value}"
        | None -> "None"

    let argumentsLiteral =
        match arguments with
        | [] -> "[]"
        | values ->
            values
            |> List.map toFScriptStringLiteral
            |> String.concat "; "
            |> sprintf "[%s]"

    let prelude =
        String.concat
            "\n"
            [ "let asEnvironment (value: Environment) = value"
              $"let Env = asEnvironment {{ ScriptName = {scriptNameLiteral}; Arguments = {argumentsLiteral} }}"
              "" ]

    let newline =
        if source.Contains("\r\n", StringComparison.Ordinal) then "\r\n"
        else "\n"

    let lines =
        source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')

    let isCommentOrBlank (line: string) =
        let trimmed = line.Trim()
        String.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//", StringComparison.Ordinal)

    let isImport (line: string) =
        line.TrimStart().StartsWith("import ", StringComparison.Ordinal)

    let mutable index = 0
    let mutable seenImport = false
    let mutable keepScanning = true

    while index < lines.Length && keepScanning do
        let line = lines[index]
        if isImport line then
            seenImport <- true
            index <- index + 1
        elif isCommentOrBlank line then
            index <- index + 1
        else
            keepScanning <- false

    let insertionIndex = if seenImport then index else 0
    let before = lines |> Array.take insertionIndex |> String.concat newline
    let after = lines |> Array.skip insertionIndex |> String.concat newline

    if insertionIndex = 0 then
        prelude + source
    elif String.IsNullOrEmpty(after) then
        before + newline + prelude
    else
        before + newline + prelude + after

let private normalizeDeniedPathGlobs (deniedPathGlobs: string list) =
    deniedPathGlobs
    |> List.map (fun glob -> glob.Trim().Replace("\\", "/", StringComparison.Ordinal))
    |> List.filter (String.IsNullOrWhiteSpace >> not)
    |> List.distinct

let private deniedPathGlobsCacheToken (deniedPathGlobs: string list) =
    deniedPathGlobs
    |> normalizeDeniedPathGlobs
    |> List.sort
    |> String.concat "|"

let private createFScriptHostContext (rootDirectory: string) (deniedPathGlobs: string list) =
    let fullRoot = Path.GetFullPath(rootDirectory)
    let deniedPathGlobs = normalizeDeniedPathGlobs deniedPathGlobs
    let context: FScript.Runtime.HostContext =
        { RootDirectory = fullRoot
          DeniedPathGlobs = deniedPathGlobs }
    context

let private loadFScript (rootDirectory: string) (deniedPathGlobs: string list) (scriptFile: string) =

    let fullPath = Path.GetFullPath(scriptFile)
    let externs = FScript.Runtime.Registry.all (createFScriptHostContext rootDirectory deniedPathGlobs)
    let scriptName = Path.GetFileName(fullPath) |> Option.ofObj
    let entrySource = File.ReadAllText(fullPath) |> prependEnvironmentBinding scriptName []
    let loaded =
        FScript.Runtime.ScriptHost.loadSourceWithIncludes
            externs
            rootDirectory
            fullPath
            entrySource
            (fun resolvedPath -> File.ReadAllText(resolvedPath) |> Some)
    toFScriptScript fullPath loaded

let private loadFScriptFromSourceWithIncludes
    (hostRootDirectory: string)
    (deniedPathGlobs: string list)
    (includeRootDirectory: string)
    (entryFile: string)
    (entrySource: string)
    (resolveImportedSource: string -> string option) =
    let externs = FScript.Runtime.Registry.all (createFScriptHostContext hostRootDirectory deniedPathGlobs)
    let scriptName = Path.GetFileName(entryFile) |> Option.ofObj
    let entrySource = entrySource |> prependEnvironmentBinding scriptName []
    let loaded =
        FScript.Runtime.ScriptHost.loadSourceWithIncludes
            externs
            includeRootDirectory
            entryFile
            entrySource
            resolveImportedSource
    toFScriptScript entryFile loaded

let loadScriptWithDeniedPathGlobs (rootDirectory: string) (_references: string list) (deniedPathGlobs: string list) (scriptFile: string) =
    let fullScriptPath = Path.GetFullPath(scriptFile)
    let fullRootDirectory = Path.GetFullPath(rootDirectory)
    let deniedToken = deniedPathGlobs |> deniedPathGlobsCacheToken
    let cacheKey = $"{fullRootDirectory}::{deniedToken}::{fullScriptPath}"
    lock loadLock (fun () ->
        match cache |> Map.tryFind cacheKey with
        | Some script ->
            Performance.trackScriptCacheHit()
            script
        | None ->
            let startedAt = Stopwatch.GetTimestamp()
            let extension =
                match Path.GetExtension(fullScriptPath) with
                | null
                | "" -> ""
                | value -> value.ToLowerInvariant()

            if extension <> ".fss" then
                raiseInvalidArg $"Legacy F# extension scripts are no longer supported; migrate '{scriptFile}' to '.fss'"

            let script = loadFScript fullRootDirectory deniedPathGlobs fullScriptPath
            cache <- cache |> Map.add cacheKey script
            Performance.trackScriptLoad(Stopwatch.GetTimestamp() - startedAt)
            script)

let loadScript (rootDirectory: string) (references: string list) (scriptFile: string) =
    loadScriptWithDeniedPathGlobs rootDirectory references [ ".git" ] scriptFile

let loadScriptFromSourceWithIncludesWithDeniedPathGlobs
    (hostRootDirectory: string)
    (deniedPathGlobs: string list)
    (includeRootDirectory: string)
    (entryFile: string)
    (entrySource: string)
    (resolveImportedSource: string -> string option) =
    let fullHostRootDirectory = Path.GetFullPath(hostRootDirectory)
    let fullIncludeRootDirectory = Path.GetFullPath(includeRootDirectory)
    let fullEntryFile = Path.GetFullPath(entryFile)
    let deniedToken = deniedPathGlobs |> deniedPathGlobsCacheToken
    let cacheKey = $"{fullHostRootDirectory}::{deniedToken}::embedded::{fullIncludeRootDirectory}::{fullEntryFile}"

    lock loadLock (fun () ->
        match cache |> Map.tryFind cacheKey with
        | Some script ->
            Performance.trackScriptCacheHit()
            script
        | None ->
            let startedAt = Stopwatch.GetTimestamp()
            let script =
                loadFScriptFromSourceWithIncludes
                    fullHostRootDirectory
                    deniedPathGlobs
                    fullIncludeRootDirectory
                    fullEntryFile
                    entrySource
                    resolveImportedSource
            cache <- cache |> Map.add cacheKey script
            Performance.trackScriptLoad(Stopwatch.GetTimestamp() - startedAt)
            script)

let loadScriptFromSourceWithIncludes
    (hostRootDirectory: string)
    (includeRootDirectory: string)
    (entryFile: string)
    (entrySource: string)
    (resolveImportedSource: string -> string option) =
    loadScriptFromSourceWithIncludesWithDeniedPathGlobs
        hostRootDirectory
        [ ".git" ]
        includeRootDirectory
        entryFile
        entrySource
        resolveImportedSource
