module Terrabuild.Scripting

open System
open System.IO
open System.Reflection
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open Microsoft.FSharp.Reflection
open Terrabuild.ScriptingContracts
open Terrabuild.Expressions
open Errors

type private RuntimeInvoker = Terrabuild.Expressions.Value -> System.Type -> objnull

type Invocable private (methodOpt: MethodInfo option, runtimeInvoker: RuntimeInvoker option) =
    let expectString (name: string) (value: objnull) =
        match value with
        | :? string as str -> str
        | _ -> raiseTypeError $"Can't assign value to parameter '{name}' as string"

    let convertToNone (parameterType: System.Type) =
        let template = typedefof<option<_>>
        let genericType = template.MakeGenericType([| parameterType.GetGenericArguments()[0] |])
        let noneCase = FSharpType.GetUnionCases(genericType) |> Array.find (fun unionCase -> unionCase.Name = "None")
        FSharpValue.MakeUnion(noneCase, [| |])

    let convertToSome (parameterType: System.Type) (value: obj) =
        let template = typedefof<option<_>>
        let genericType = template.MakeGenericType([| parameterType.GetGenericArguments()[0] |])
        let someCase = FSharpType.GetUnionCases(genericType) |> Array.find (fun unionCase -> unionCase.Name = "Some")
        FSharpValue.MakeUnion(someCase, [| value |])

    let rec mapParameter (value: Terrabuild.Expressions.Value) (name: string) (parameterType: System.Type) =
        match value with
        | Terrabuild.Expressions.Value.Nothing ->
            if parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() = typedefof<option<_>> then convertToNone parameterType
            elif parameterType.IsGenericType && parameterType = typeof<Map<string, string>> then
                let emptyMap: Map<string, string> = Map.empty
                box emptyMap
            else
                raiseTypeError $"Can't assign default value to parameter '{name}'"
        | Terrabuild.Expressions.Value.Bool boolValue ->
            if boolValue.GetType().IsAssignableTo(parameterType) then box boolValue
            elif parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() = typedefof<option<_>> then convertToSome parameterType boolValue
            else raiseTypeError $"Can't assign default value to parameter '{name}'"
        | Terrabuild.Expressions.Value.String stringValue ->
            if stringValue.GetType().IsAssignableTo(parameterType) then box stringValue
            elif parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() = typedefof<option<_>> then convertToSome parameterType stringValue
            else raiseTypeError $"Can't assign default value to parameter '{name}'"
        | Terrabuild.Expressions.Value.Number numberValue ->
            if numberValue.GetType().IsAssignableTo(parameterType) then box numberValue
            elif parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() = typedefof<option<_>> then convertToSome parameterType numberValue
            else raiseTypeError $"Can't assign default value to parameter '{name}'"
        | Terrabuild.Expressions.Value.Enum enumValue ->
            if enumValue.GetType().IsAssignableTo(parameterType) then box enumValue
            elif parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() = typedefof<option<_>> then convertToSome parameterType enumValue
            else raiseTypeError $"Can't assign default value to parameter '{name}'"
        | Terrabuild.Expressions.Value.Object objectValue -> objectValue
        | Terrabuild.Expressions.Value.Map mapValue ->
            match TypeHelpers.getKind parameterType with
            | TypeHelpers.TypeKind.FsRecord ->
                let ctor = FSharpValue.PreComputeRecordConstructor(parameterType)
                let fields = FSharpType.GetRecordFields(parameterType)
                let fieldIndices = fields |> Array.mapi (fun index field -> field.Name, index) |> Map.ofArray
                let fieldValues = Array.create fields.Length (false, null)

                for KeyValue(fieldName, mapItemValue) in mapValue do
                    match fieldIndices |> Map.tryFind fieldName with
                    | None -> raiseSymbolError $"Property {fieldName} does not exists"
                    | Some index ->
                        let field = fields[index]
                        let mapped = mapParameter mapItemValue field.Name field.PropertyType
                        fieldValues[index] <- true, mapped

                let ctorValues =
                    fieldValues
                    |> Array.mapi (fun index (initialized, itemValue) ->
                        if initialized then itemValue
                        else
                            let field = fields[index]
                            mapParameter Terrabuild.Expressions.Value.Nothing field.Name field.PropertyType)
                ctor ctorValues
            | TypeHelpers.TypeKind.FsMap ->
                let mapped = mapValue |> Map.map (fun itemName itemValue -> mapParameter itemValue itemName typeof<string> |> expectString itemName)
                box mapped
            | TypeHelpers.TypeKind.FsOption ->
                let mapped = mapValue |> Map.map (fun itemName itemValue -> mapParameter itemValue itemName typeof<string> |> expectString itemName)
                convertToSome parameterType mapped
            | typeKind ->
                raiseTypeError $"Can't assign map to parameter '{name}' of type '{typeKind}'"
        | Terrabuild.Expressions.Value.List listValue ->
            match TypeHelpers.getKind parameterType with
            | TypeHelpers.TypeKind.FsList ->
                let mapped = listValue |> List.map (fun itemValue -> mapParameter itemValue name typeof<string> |> expectString name)
                box mapped
            | TypeHelpers.TypeKind.FsOption ->
                let mapped = listValue |> List.map (fun itemValue -> mapParameter itemValue name typeof<string> |> expectString name)
                convertToSome parameterType mapped
            | typeKind ->
                raiseTypeError $"Can't assign list to parameter '{name}' of type '{typeKind}'"

    let mapParameters (map: Map<string, Terrabuild.Expressions.Value>) (parameters: ParameterInfo array) =
        parameters
        |> Array.map (fun parameterInfo ->
            let parameterName = parameterInfo.Name |> nonNull
            match map |> Map.tryFind parameterName with
            | None -> mapParameter Terrabuild.Expressions.Value.Nothing parameterName parameterInfo.ParameterType
            | Some parameterValue -> mapParameter parameterValue parameterName parameterInfo.ParameterType)

    let buildArgs (value: Terrabuild.Expressions.Value) (methodInfo: MethodInfo) =
        match value with
        | Terrabuild.Expressions.Value.Map map ->
            let parameters = methodInfo.GetParameters()
            mapParameters map parameters
        | _ ->
            raiseTypeError "Expecting a map for build arguments"

    member _.Invoke<'t>(value: Terrabuild.Expressions.Value) =
        match runtimeInvoker, methodOpt with
        | Some invokeRuntime, _ ->
            invokeRuntime value typeof<'t> :?> 't
        | None, Some methodInfo ->
            let args = buildArgs value methodInfo
            methodInfo.Invoke(null, args) :?> 't
        | _ ->
            raiseBugError "Invalid invocable state"

    new (methodInfo: MethodInfo) = Invocable(Some methodInfo, None)

    static member internal FromRuntime(runtimeInvoke: RuntimeInvoker) =
        Invocable(None, Some runtimeInvoke)

type ScriptRuntime =
    | Legacy of System.Type
    | FScript of FScript.Runtime.ScriptHost.LoadedScript * ScriptDescriptor * string option * string option

type Script internal (runtime: ScriptRuntime) =
    new(mainType: System.Type) = Script(Legacy mainType)

    member _.GetMethod(name: string) =
        match runtime with
        | Legacy mainType ->
            match mainType.GetMethod(name, BindingFlags.IgnoreCase ||| BindingFlags.Public ||| BindingFlags.Static) with
            | Null -> None
            | NonNull methodInfo -> Invocable(methodInfo) |> Some
        | FScript(loaded, _, _, _) ->
            if loaded.ExportedFunctionNames |> List.contains name then
                let runtimeInvoke (args: Terrabuild.Expressions.Value) (targetType: System.Type) =
                    let inputMap =
                        match args with
                        | Terrabuild.Expressions.Value.Map map -> map
                        | _ -> raiseTypeError $"FScript function '{name}' expects map-based arguments"

                    let signature =
                        match loaded.ExportedFunctionSignatures |> Map.tryFind name with
                        | Some value -> value
                        | None -> raiseInvalidArg $"Missing signature metadata for exported function '{name}'"

                    let toFScriptArgument (parameterType: FScript.Language.Type) (value: Terrabuild.Expressions.Value) =
                        match parameterType with
                        | FScript.Language.TOption _ ->
                            match value with
                            | Terrabuild.Expressions.Value.Nothing -> FScript.Language.VOption None
                            | _ ->
                                match Conversions.ToFScriptValue value with
                                | FScript.Language.VOption optionValue -> FScript.Language.VOption optionValue
                                | converted -> FScript.Language.VOption (Some converted)
                        | _ ->
                            Conversions.ToFScriptValue value

                    let invocationArgs =
                        match signature.ParameterNames, signature.ParameterTypes with
                        | contextParam :: remainingParams, contextType :: remainingTypes ->
                            if contextParam <> "context" then
                                raiseInvalidArg $"Exported function '{name}' must declare 'context' as first parameter"

                            let contextArg =
                                match inputMap |> Map.tryFind "context" with
                                | Some value -> toFScriptArgument contextType value
                                | None -> raiseTypeError $"Missing required argument 'context' for function '{name}'"

                            let remainingArgs =
                                (remainingParams, remainingTypes)
                                ||> List.map2 (fun parameterName parameterType ->
                                    match inputMap |> Map.tryFind parameterName with
                                    | Some value ->
                                        toFScriptArgument parameterType value
                                    | None ->
                                        match parameterType with
                                        | FScript.Language.TOption _ -> FScript.Language.VOption None
                                        | _ -> raiseTypeError $"Missing required argument '{parameterName}' for function '{name}'")

                            contextArg :: remainingArgs
                        | _ ->
                            raiseInvalidArg $"Exported function '{name}' must declare 'context' as first parameter"

                    let result = FScript.Runtime.ScriptHost.invoke loaded name invocationArgs
                    Conversions.FromFScriptValue(targetType, result)
                Invocable.FromRuntime(runtimeInvoke) |> Some
            else
                None

    member _.ResolveCommandMethod(command: string) =
        match runtime with
        | Legacy mainType ->
            if mainType.GetMethod(command, BindingFlags.IgnoreCase ||| BindingFlags.Public ||| BindingFlags.Static) |> isNull |> not then
                Some command
            elif mainType.GetMethod("__dispatch__", BindingFlags.IgnoreCase ||| BindingFlags.Public ||| BindingFlags.Static) |> isNull |> not then
                Some "__dispatch__"
            else
                None
        | FScript(loaded, _, dispatchMethod, _) ->
            if loaded.ExportedFunctionNames |> List.contains command then Some command
            else dispatchMethod

    member _.ResolveDefaultMethod() =
        match runtime with
        | Legacy mainType ->
            if mainType.GetMethod("__defaults__", BindingFlags.IgnoreCase ||| BindingFlags.Public ||| BindingFlags.Static) |> isNull |> not then
                Some "__defaults__"
            else
                None
        | FScript(_, _, _, defaultMethod) ->
            defaultMethod

    member _.TryGetFunctionFlags(name: string) =
        match runtime with
        | Legacy mainType ->
            match mainType.GetMethod(name, BindingFlags.IgnoreCase ||| BindingFlags.Public ||| BindingFlags.Static) with
            | Null -> None
            | NonNull methodInfo ->
                let flags =
                    [ match methodInfo.GetCustomAttribute(typeof<BatchableAttribute>) with
                      | :? BatchableAttribute -> ExportFlag.Batchable
                      | _ -> ()
                      match methodInfo.GetCustomAttribute(typeof<CacheableAttribute>) with
                      | :? CacheableAttribute as cacheable -> ExportFlag.Cache cacheable.Cacheability
                      | _ -> () ]
                Some flags
        | FScript(loaded, descriptor, _, _) ->
            if loaded.ExportedFunctionNames |> List.contains name then
                descriptor |> Map.tryFind name |> Option.defaultValue [] |> Some
            else
                None

    member _.GetAttribute<'a when 'a :> Attribute>(name: string) =
        match runtime with
        | Legacy mainType ->
            match mainType.GetMethod(name, BindingFlags.IgnoreCase ||| BindingFlags.Public ||| BindingFlags.Static) with
            | Null -> None
            | NonNull methodInfo ->
                match methodInfo.GetCustomAttribute(typeof<'a>) with
                | NonNull attributeValue -> attributeValue :?> 'a |> Some
                | _ -> None
        | FScript _ ->
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
            | "batchable", None -> Some ExportFlag.Batchable
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
    static member private toFScriptCoreValue(value: Terrabuild.Expressions.Value) =
        let rec convert value =
            match value with
            | Terrabuild.Expressions.Value.Nothing -> FScript.Language.VOption None
            | Terrabuild.Expressions.Value.Bool boolValue -> FScript.Language.VBool boolValue
            | Terrabuild.Expressions.Value.String stringValue -> FScript.Language.VString stringValue
            | Terrabuild.Expressions.Value.Number numberValue -> FScript.Language.VInt (int64 numberValue)
            | Terrabuild.Expressions.Value.Enum enumValue -> FScript.Language.VString enumValue
            | Terrabuild.Expressions.Value.Map mapValue ->
                mapValue
                |> Map.toList
                |> List.map (fun (key, itemValue) -> FScript.Language.MKString key, convert itemValue)
                |> Map.ofList
                |> FScript.Language.VMap
            | Terrabuild.Expressions.Value.List listValue -> listValue |> List.map convert |> FScript.Language.VList
            | Terrabuild.Expressions.Value.Object objectValue -> Conversions.toFScriptValueFromObject objectValue
        convert value

    static member private toFScriptValueFromObject (value: objnull) =
        let rec convertObject (objValue: objnull) =
            if isNull objValue then
                FScript.Language.VOption None
            else
                let valueType = objValue.GetType()
                if valueType = typeof<string> then
                    match objValue with
                    | :? string as str -> FScript.Language.VString str
                    | _ -> raiseTypeError "Expected string object value"
                elif valueType = typeof<bool> then FScript.Language.VBool (objValue :?> bool)
                elif valueType = typeof<int> then FScript.Language.VInt (int64 (objValue :?> int))
                elif valueType = typeof<int64> then FScript.Language.VInt (objValue :?> int64)
                elif valueType = typeof<float> then FScript.Language.VFloat (objValue :?> float)
                elif valueType = typeof<double> then FScript.Language.VFloat (objValue :?> double)
                elif FSharpType.IsRecord(valueType, true) then
                    let fields = FSharpType.GetRecordFields(valueType)
                    let values = FSharpValue.GetRecordFields(nonNull objValue)
                    let mapped =
                        Array.zip fields values
                        |> Array.map (fun (fieldInfo, fieldValue) -> fieldInfo.Name, convertObject fieldValue)
                        |> Map.ofArray
                    FScript.Language.VRecord mapped
                elif FSharpType.IsUnion(valueType, true) && valueType.IsGenericType && valueType.GetGenericTypeDefinition() = typedefof<option<_>> then
                    let unionCase, fields = FSharpValue.GetUnionFields(nonNull objValue, valueType)
                    match unionCase.Name, fields with
                    | "None", _ -> FScript.Language.VOption None
                    | "Some", [| item |] -> FScript.Language.VOption (Some (convertObject item))
                    | _ -> raiseTypeError $"Unsupported option value '{unionCase.Name}'"
                elif valueType.IsGenericType && valueType.GetGenericTypeDefinition() = typedefof<list<_>> then
                    let items =
                        match objValue with
                        | :? System.Collections.IEnumerable as enumerable -> enumerable
                        | _ -> raiseTypeError $"Unsupported list source type '{valueType.FullName}'"
                        |> Seq.cast<obj>
                        |> Seq.map convertObject
                        |> Seq.toList
                    FScript.Language.VList items
                else
                    raiseTypeError $"Unsupported object parameter type '{valueType.FullName}'"
        convertObject value

    static member ToFScriptValue(value: Terrabuild.Expressions.Value) =
        Conversions.toFScriptCoreValue value

    static member FromFScriptValue(targetType: System.Type, value: FScript.Language.Value) =
        let rec decode (target: System.Type) (value: FScript.Language.Value) : objnull =
            if target = typeof<string> then
                match value with
                | FScript.Language.VString stringValue -> box stringValue
                | _ -> raiseTypeError $"Expected string return type, got {value}"
            elif target = typeof<int> then
                match value with
                | FScript.Language.VInt intValue -> box (int intValue)
                | _ -> raiseTypeError $"Expected int return type, got {value}"
            elif target = typeof<bool> then
                match value with
                | FScript.Language.VBool boolValue -> box boolValue
                | _ -> raiseTypeError $"Expected bool return type, got {value}"
            elif target = typeof<float> || target = typeof<double> then
                match value with
                | FScript.Language.VFloat floatValue -> box floatValue
                | _ -> raiseTypeError $"Expected float return type, got {value}"
            elif target.IsGenericType && target.GetGenericTypeDefinition() = typedefof<option<_>> then
                let innerType = target.GetGenericArguments()[0]
                let unionCases = FSharpType.GetUnionCases(target)
                let noneCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "None")
                let someCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "Some")
                match value with
                | FScript.Language.VOption None -> FSharpValue.MakeUnion(noneCase, [| |])
                | FScript.Language.VOption (Some item) -> FSharpValue.MakeUnion(someCase, [| decode innerType item |])
                | _ -> raiseTypeError $"Expected option return type, got {value}"
            elif target.IsGenericType && target.GetGenericTypeDefinition() = typedefof<list<_>> then
                let innerType = target.GetGenericArguments()[0]
                let unionCases = FSharpType.GetUnionCases(target)
                let nilCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "Empty")
                let consCase = unionCases |> Array.find (fun unionCase -> unionCase.Name = "Cons")
                let empty = FSharpValue.MakeUnion(nilCase, [| |])
                match value with
                | FScript.Language.VList items ->
                    items
                    |> List.map (decode innerType)
                    |> List.rev
                    |> List.fold (fun state item -> FSharpValue.MakeUnion(consCase, [| item; state |])) empty
                | _ -> raiseTypeError $"Expected list return type, got {value}"
            elif target.IsGenericType && target.GetGenericTypeDefinition() = typedefof<Set<_>> then
                let innerType = target.GetGenericArguments()[0]
                let setModuleType =
                    match typeof<Set<string>>.Assembly.GetType("Microsoft.FSharp.Collections.SetModule") with
                    | null -> raiseBugError "Cannot resolve FSharp SetModule type"
                    | value -> value
                let ofSeqMethod = setModuleType.GetMethods() |> Array.find (fun methodInfo -> methodInfo.Name = "OfSeq")
                let genericOfSeq = ofSeqMethod.MakeGenericMethod([| innerType |])
                let values: obj =
                    match value with
                    | FScript.Language.VList items ->
                        let array = System.Array.CreateInstance(innerType, items.Length)
                        items
                        |> List.iteri (fun index item -> array.SetValue(decode innerType item, index))
                        array
                    | _ -> raiseTypeError $"Expected list return type to decode set, got {value}"
                genericOfSeq.Invoke(null, [| values |])
            elif FSharpType.IsRecord(target, true) then
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

                let fields = FSharpType.GetRecordFields(target)
                let fieldValues =
                    fields
                    |> Array.map (fun fieldInfo ->
                        match sourceMap |> Map.tryFind fieldInfo.Name with
                        | Some fieldValue -> decode fieldInfo.PropertyType fieldValue
                        | None -> raiseTypeError $"Missing field '{fieldInfo.Name}' in script result")
                FSharpValue.MakeRecord(target, fieldValues)
            elif target = typeof<Terrabuild.Expressions.Value> then
                let rec toTerrabuildValue (value: FScript.Language.Value) =
                    match value with
                    | FScript.Language.VUnit -> Terrabuild.Expressions.Value.Nothing
                    | FScript.Language.VBool boolValue -> Terrabuild.Expressions.Value.Bool boolValue
                    | FScript.Language.VInt intValue -> Terrabuild.Expressions.Value.Number (int intValue)
                    | FScript.Language.VString stringValue -> Terrabuild.Expressions.Value.String stringValue
                    | FScript.Language.VList listValue -> listValue |> List.map toTerrabuildValue |> Terrabuild.Expressions.Value.List
                    | FScript.Language.VRecord mapValue -> mapValue |> Map.map (fun _ itemValue -> toTerrabuildValue itemValue) |> Terrabuild.Expressions.Value.Map
                    | FScript.Language.VMap mapValue ->
                        mapValue
                        |> Map.toList
                        |> List.map (fun (key, itemValue) ->
                            match key with
                            | FScript.Language.MKString name -> name, toTerrabuildValue itemValue
                            | FScript.Language.MKInt _ -> raiseTypeError "Terrabuild map values expect string keys")
                        |> Map.ofList
                        |> Terrabuild.Expressions.Value.Map
                    | FScript.Language.VOption None -> Terrabuild.Expressions.Value.Nothing
                    | FScript.Language.VOption (Some optionValue) -> toTerrabuildValue optionValue
                    | _ -> raiseTypeError $"Unsupported FScript value '{value}' for Terrabuild.Expressions.Value"
                box (toTerrabuildValue value)
            else
                raiseTypeError $"Unsupported script return type '{target.FullName}'"
        decode targetType value

let private checker = FSharpChecker.Create()
let mutable private cache = Map.empty<string, Script>
let private loadLock = obj ()

let private loadLegacyScript (references: string list) (scriptFile: string) =
    let fullScriptPath = Path.GetFullPath(scriptFile)
    let outputDllName = $"{Path.GetTempFileName()}.dll"

    let compilerArgs =
        [| "-a"
           fullScriptPath
           "--targetprofile:netcore"
           "--target:library"
           $"--out:{outputDllName}"
           "--define:TERRABUILD_SCRIPT"
           for reference in references do
               $"--reference:{reference}" |]

    let errors, _ = checker.Compile(compilerArgs) |> Async.RunSynchronously
    let firstError = errors |> Array.tryFind (fun diagnostic -> diagnostic.Severity = FSharpDiagnosticSeverity.Error)
    if firstError.IsSome then
        raiseExternalError $"Error while compiling script {fullScriptPath}: {firstError.Value}"

    let assembly = Assembly.LoadFile(outputDllName)
    let expectedMainTypeName = Path.GetFileNameWithoutExtension(fullScriptPath)
    let mainType =
        match assembly.GetTypes() |> Seq.tryFind (fun assemblyType -> String.Compare(assemblyType.Name, expectedMainTypeName, true) = 0) with
        | Some assemblyType -> assemblyType
        | _ -> raiseInvalidArg $"Failed to identify function scope (either module or root class '{expectedMainTypeName}')"

    Script(mainType)

let private toFScriptScript (loaded: FScript.Runtime.ScriptHost.LoadedScript) =
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
    Script(FScript(loaded, descriptor, dispatchMethod, defaultMethod))

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

let private loadFScript (rootDirectory: string) (scriptFile: string) =

    let fullPath = Path.GetFullPath(scriptFile)
    let externs = FScript.Runtime.Registry.all { FScript.Runtime.HostContext.RootDirectory = rootDirectory }
    let scriptName = Path.GetFileName(fullPath) |> Option.ofObj
    let entrySource = File.ReadAllText(fullPath) |> prependEnvironmentBinding scriptName []
    let loaded =
        FScript.Runtime.ScriptHost.loadSourceWithIncludes
            externs
            rootDirectory
            fullPath
            entrySource
            (fun resolvedPath -> File.ReadAllText(resolvedPath) |> Some)
    toFScriptScript loaded

let private loadFScriptFromSourceWithIncludes
    (hostRootDirectory: string)
    (includeRootDirectory: string)
    (entryFile: string)
    (entrySource: string)
    (resolveImportedSource: string -> string option) =
    let externs = FScript.Runtime.Registry.all { FScript.Runtime.HostContext.RootDirectory = hostRootDirectory }
    let scriptName = Path.GetFileName(entryFile) |> Option.ofObj
    let entrySource = entrySource |> prependEnvironmentBinding scriptName []
    let loaded =
        FScript.Runtime.ScriptHost.loadSourceWithIncludes
            externs
            includeRootDirectory
            entryFile
            entrySource
            resolveImportedSource
    toFScriptScript loaded

let loadScript (rootDirectory: string) (references: string list) (scriptFile: string) =
    let fullScriptPath = Path.GetFullPath(scriptFile)
    let fullRootDirectory = Path.GetFullPath(rootDirectory)
    let cacheKey = $"{fullRootDirectory}::{fullScriptPath}"
    lock loadLock (fun () ->
        match cache |> Map.tryFind cacheKey with
        | Some script -> script
        | None ->
            let extension =
                match Path.GetExtension(fullScriptPath) with
                | null
                | "" -> ""
                | value -> value.ToLowerInvariant()
            let script =
                match extension with
                | ".fss" -> loadFScript fullRootDirectory fullScriptPath
                | _ -> loadLegacyScript references fullScriptPath
            cache <- cache |> Map.add cacheKey script
            script)

let loadScriptFromSourceWithIncludes
    (hostRootDirectory: string)
    (includeRootDirectory: string)
    (entryFile: string)
    (entrySource: string)
    (resolveImportedSource: string -> string option) =
    let fullHostRootDirectory = Path.GetFullPath(hostRootDirectory)
    let fullIncludeRootDirectory = Path.GetFullPath(includeRootDirectory)
    let fullEntryFile = Path.GetFullPath(entryFile)
    let cacheKey = $"{fullHostRootDirectory}::embedded::{fullIncludeRootDirectory}::{fullEntryFile}"

    lock loadLock (fun () ->
        match cache |> Map.tryFind cacheKey with
        | Some script -> script
        | None ->
            let script =
                loadFScriptFromSourceWithIncludes
                    fullHostRootDirectory
                    fullIncludeRootDirectory
                    fullEntryFile
                    entrySource
                    resolveImportedSource
            cache <- cache |> Map.add cacheKey script
            script)
