open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

type Parameter = {
    Name: string
    Required: bool
    Summary: string
    Example: string
}

type Command = {
    Name: string
    Weight: int option
    mutable Title: string option
    Cacheability: string option
    Batchability: bool
    mutable Summary: string
    mutable Parameters: Parameter list
}

type Extension = {
    Name: string
    mutable Summary: string
    mutable Commands: Command list
}

type private ScriptArgDoc = {
    Name: string
    Required: bool
    Summary: string
    Example: string
}

type private ScriptCommandDoc = {
    Name: string
    mutable Summary: string option
    mutable Title: string option
    mutable Args: ScriptArgDoc list
}

type private ScriptDocs = {
    Name: string
    mutable Summary: string option
    mutable Commands: Map<string, ScriptCommandDoc>
}

type private FunctionMeta = {
    Name: string
    Parameters: string list
}

let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None

let private collapseText (value: string) =
    let parts =
        value.Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun x -> x.Trim())
        |> Array.filter (fun x -> x <> "")
    match parts with
    | [||] -> ""
    | [| single |] -> single
    | multiple ->
        multiple
        |> Array.mapi (fun idx line -> if idx = 0 then line else " " + line)
        |> String.concat "\n"

let private normalizeParamNameForCompare (name: string) =
    name.ToLowerInvariant().Replace("-", "").Replace("_", "")

let private parseRequiredAttribute (raw: string option) =
    match raw |> Option.map (fun v -> v.Trim().ToLowerInvariant()) with
    | Some "true"
    | Some "required" -> true
    | _ -> false

let private parseXmlDocBlock (scriptPath: string) (docLines: string list) =
    if List.isEmpty docLines then
        None
    else
        try
            let xml = "<root>\n" + (String.concat "\n" docLines) + "\n</root>"
            let root =
                let document = XDocument.Parse(xml)
                match document.Root with
                | null -> failwith $"Invalid XML doc comment in {scriptPath}: missing root node"
                | value -> value
            let summary =
                root.Elements(XName.Get "summary")
                |> Seq.tryHead
                |> Option.map (fun e -> collapseText e.Value)
                |> Option.filter (fun x -> x <> "")
            let title =
                root.Elements(XName.Get "title")
                |> Seq.tryHead
                |> Option.map (fun e -> collapseText e.Value)
                |> Option.filter (fun x -> x <> "")
            let args =
                root.Elements(XName.Get "param")
                |> Seq.map (fun e ->
                    let name =
                        e.Attribute(XName.Get "name")
                        |> Option.ofObj
                        |> Option.map (fun x -> x.Value.Trim())
                        |> Option.defaultWith (fun () -> failwith $"Missing 'name' attribute on <param> in {scriptPath}")
                    let example =
                        e.Attribute(XName.Get "example")
                        |> Option.ofObj
                        |> Option.map (fun x -> x.Value.Trim())
                        |> Option.defaultValue ""
                    let required =
                        e.Attribute(XName.Get "required")
                        |> Option.ofObj
                        |> Option.map (fun x -> x.Value)
                        |> parseRequiredAttribute
                    let summary = collapseText e.Value
                    { Name = name; Required = required; Summary = summary; Example = example })
                |> List.ofSeq
            Some(summary, title, args)
        with ex ->
            failwith $"Invalid XML doc comment in {scriptPath}: {ex.Message}"

let private parseScriptDocs (scriptPath: string) (extensionName: string) : ScriptDocs =
    let lines = File.ReadAllLines(scriptPath)
    let commands = System.Collections.Generic.Dictionary<string, ScriptCommandDoc>()
    let mutable extSummary: string option = None
    let mutable pendingDoc: string list = []
    let mutable seenCode = false

    let addPendingDocLine (line: string) =
        pendingDoc <- pendingDoc @ [ line.Trim() ]

    let clearPending () =
        pendingDoc <- []

    let applyAsExtensionDoc () =
        match parseXmlDocBlock scriptPath pendingDoc with
        | Some(summary, _, _) ->
            extSummary <- summary
        | None -> ()
        clearPending ()

    let applyAsCommandDoc (commandName: string) =
        let name =
            match commandName.Trim().ToLowerInvariant() with
            | "dispatch" -> "__dispatch__"
            | "defaults" -> "__defaults__"
            | other -> other
        let summary, title, args =
            match parseXmlDocBlock scriptPath pendingDoc with
            | Some(summary, title, args) -> summary, title, args
            | None -> None, None, []
        clearPending ()
        let value = { Name = name; Summary = summary; Title = title; Args = args }
        commands.[name] <- value

    for rawLine in lines do
        let line = rawLine.Trim()
        if line.StartsWith("///") then
            addPendingDocLine (line.Substring(3))
        elif line = "" then
            ()
        else
            match line with
            | Regex "^(?:\\[\\s*<\\s*export\\s*>\\s*\\]\\s*)?let\\s+([A-Za-z_][A-Za-z0-9_]*)\\b.*$" [commandName] ->
                applyAsCommandDoc commandName
                seenCode <- true
            | _ ->
                if not seenCode && not (List.isEmpty pendingDoc) then
                    applyAsExtensionDoc ()
                else
                    clearPending ()
                seenCode <- true

    if not seenCode && not (List.isEmpty pendingDoc) then
        applyAsExtensionDoc ()

    { Name = extensionName
      Summary = extSummary
      Commands = commands |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq }

let private parseExportedFunctionParameters (scriptContent: string) =
    let matches =
        Regex.Matches(
            scriptContent,
            @"(?:\[\s*<\s*export\s*>\s*\]\s*)?let\s+([A-Za-z_][A-Za-z0-9_]*)\s+((?:\([^\)]*\)\s*)+)=",
            RegexOptions.Multiline)
    [ for m in matches do
        let name = m.Groups[1].Value
        let argsGroup = m.Groups[2].Value
        let argsMatches = Regex.Matches(argsGroup, @"\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*:")
        let parameters = [ for a in argsMatches -> a.Groups[1].Value ]
        yield name, { Name = name; Parameters = parameters } ]
    |> Map.ofList

let private parseDescriptorFlags (scriptContent: string) =
    let normalizeFlag (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "dispatch" -> Some "dispatch"
        | "default" -> Some "default"
        | "batchable" -> Some "batchable"
        | "never" -> Some "never"
        | "local" -> Some "local"
        | "external" -> Some "external"
        | "remote" -> Some "remote"
        | _ -> None

    let parseFlags (flagsRaw: string) =
        let fromStrings =
            Regex.Matches(flagsRaw, "\"([^\r\n\"]+)\"")
            |> Seq.cast<Match>
            |> Seq.choose (fun x -> normalizeFlag x.Groups[1].Value)
            |> List.ofSeq

        let fromUnionCases =
            Regex.Matches(flagsRaw, @"\b([A-Za-z_][A-Za-z0-9_]*)\b")
            |> Seq.cast<Match>
            |> Seq.choose (fun x -> normalizeFlag x.Groups[1].Value)
            |> List.ofSeq

        if not (List.isEmpty fromStrings) then fromStrings else fromUnionCases

    let entries =
        Regex.Matches(scriptContent, @"\[\s*nameof\s+([A-Za-z_][A-Za-z0-9_]*)\s*\]\s*=\s*\[(.*?)\]", RegexOptions.Singleline)
        |> Seq.cast<Match>
        |> Seq.map (fun m ->
            let fnName = m.Groups[1].Value
            let flagsRaw = m.Groups[2].Value
            let flags = parseFlags flagsRaw
            fnName, flags)
        |> List.ofSeq

    if List.isEmpty entries then
        failwith "Missing exported descriptor map. Expected entries like: [nameof fn] = [\"local\"; \"dispatch\"]"

    entries

let private toCacheability flags =
    flags
    |> List.tryPick (fun flag ->
        match flag with
        | "never" -> Some "never"
        | "local" -> Some "local"
        | "remote" -> Some "remote"
        | "external" -> Some "external"
        | _ -> None)

let private buildScriptExtension (scriptPath: string) : Extension =
    let extensionName =
        match Path.GetFileNameWithoutExtension(scriptPath) with
        | null -> failwith $"Unable to resolve extension name from script path '{scriptPath}'"
        | name -> name.ToLowerInvariant()
    let content = File.ReadAllText(scriptPath)

    let directives = parseScriptDocs scriptPath extensionName
    let functionParams = parseExportedFunctionParameters content
    let descriptor = parseDescriptorFlags content

    let commandDocs = directives.Commands

    let commands =
        descriptor
        |> List.map (fun (functionName, flags) ->
            let commandName =
                if flags |> List.contains "dispatch" then "__dispatch__"
                elif flags |> List.contains "default" then "__defaults__"
                else functionName

            let functionMeta =
                match functionParams |> Map.tryFind functionName with
                | Some meta -> meta
                | None -> failwith $"Missing '[<export>] let {functionName} ...' declaration in {scriptPath}"

            let commandDoc =
                match commandDocs |> Map.tryFind commandName with
                | Some value -> value
                | None -> failwith $"Missing XML doc comment block for exported function '{functionName}' in {scriptPath}"

            let functionArgs = functionMeta.Parameters |> List.filter (fun p -> p <> "context")

            let docArgs =
                commandDoc.Args
                |> List.filter (fun arg -> arg.Name <> "context")

            let mergedArgs: Map<string, ScriptArgDoc> =
                docArgs
                |> List.fold (fun acc arg -> acc |> Map.add arg.Name arg) Map.empty

            let fromDocOrder =
                docArgs
                |> List.filter (fun arg -> arg.Name <> "__dispatch__")
                |> List.map (fun arg -> ({ Name = arg.Name; Required = arg.Required; Summary = arg.Summary; Example = arg.Example } : Parameter))

            let fromFunctionOrder =
                functionArgs
                |> List.map (fun name ->
                    match mergedArgs |> Map.tryFind name with
                    | Some arg -> ({ Name = arg.Name; Required = arg.Required; Summary = arg.Summary; Example = arg.Example } : Parameter)
                    | None -> ({ Name = name; Required = false; Summary = ""; Example = "" } : Parameter))

            let commandParameters: Parameter list =
                if commandName = "__defaults__" then
                    fromDocOrder
                elif not (List.isEmpty fromDocOrder) then
                    let documented =
                        fromDocOrder
                        |> List.map (fun x -> normalizeParamNameForCompare x.Name)
                        |> Set.ofList
                    let missing =
                        fromFunctionOrder
                        |> List.filter (fun x -> not (documented.Contains (normalizeParamNameForCompare x.Name)))
                    fromDocOrder @ missing
                else
                    fromFunctionOrder

            { Name = commandName
              Weight = None
              Title = commandDoc.Title
              Cacheability = toCacheability flags
              Batchability = flags |> List.contains "batchable"
              Summary = commandDoc.Summary |> Option.defaultValue ""
              Parameters = commandParameters })

    let extension : Extension =
        { Name = extensionName
          Summary = directives.Summary |> Option.defaultValue ""
          Commands = commands }
    extension

let private buildExtensions () : Map<string, Extension> =
    let scriptsDir = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../../src/Terrabuild/Scripts"))
    Directory.EnumerateFiles(scriptsDir, "*.fss")
    |> Seq.filter (fun path -> Path.GetFileName(path) <> "null.fss")
    |> Seq.map buildScriptExtension
    |> Seq.map (fun ext -> ext.Name, ext)
    |> Map.ofSeq

let private tryReadExistingWeight (commandFile: string) =
    if not (File.Exists commandFile) then None
    else
        File.ReadAllLines(commandFile)
        |> Array.tryPick (fun line ->
            let trimmed = line.Trim()
            if trimmed.StartsWith("weight:") then
                let value = trimmed.Substring("weight:".Length).Trim()
                match Int32.TryParse(value) with
                | true, parsed -> Some parsed
                | _ -> None
            else None)

let private tryReadExistingArgOrder (commandFile: string) =
    if not (File.Exists commandFile) then []
    else
        let lines = File.ReadAllLines(commandFile)
        let mutable inArgs = false
        [
            for line in lines do
                let trimmed = line.Trim()
                if trimmed = "## Argument Reference" then
                    inArgs <- true
                elif inArgs && trimmed.StartsWith("## ") then
                    inArgs <- false
                elif inArgs then
                    match trimmed with
                    | Regex "^\\* `([^`]+)` - \\((Required|Optional)\\).*$" [name; _] ->
                        yield name
                    | _ -> ()
        ]

let private reorderParameters (command: Command) (commandFile: string) =
    let existingOrder =
        tryReadExistingArgOrder commandFile
        |> List.map (fun name -> if command.Name = "__dispatch__" && name = "command" then "__dispatch__" else name)
    if List.isEmpty existingOrder then
        command.Parameters
    else
        let indexed =
            existingOrder
            |> List.mapi (fun idx name -> normalizeParamNameForCompare name, idx)
            |> Map.ofList
        let withIdx, withoutIdx =
            command.Parameters
            |> List.partition (fun prm -> indexed |> Map.containsKey (normalizeParamNameForCompare prm.Name))
        let sorted =
            withIdx
            |> List.sortBy (fun prm -> indexed.[normalizeParamNameForCompare prm.Name])
        sorted @ withoutIdx

let writeCommand extensionDir (command: Command) (batchCommand: Command option) (extension: Extension) =

    let cacheInfo =
        match command.Cacheability with
        | None -> "never"
        | Some value -> value

    let batchInfo =
        if command.Batchability then "yes"
        else "no"

    match command.Name with
    | "__defaults__" -> ()
    | _ ->
        let commandFile = Path.Combine(extensionDir, $"{command.Name}.md")
        let existingWeight = tryReadExistingWeight commandFile
        let effectiveWeight = command.Weight |> Option.orElse existingWeight
        let orderedParameters = reorderParameters command commandFile
        let commandContent = [
            "---"
            match command.Name with
            | "__dispatch__" ->
                $"title: \"<command>\""
            | _ ->
                $"title: \"{command.Name}\""
            if effectiveWeight |> Option.isSome then $"weight: {effectiveWeight.Value}"
            "---"

            command.Summary

            let name =
                match orderedParameters |> List.tryFind (fun x -> x.Name = command.Name) with
                | Some nameOverride -> nameOverride.Example
                | _ -> command.Name

            match command.Name with
            | "__dispatch__" -> $"Example for command `{name}`:"
            | _ -> ()

            "```"
            match orderedParameters with
            | [] -> $"@{extension.Name} {name} {{ }}"
            | prms ->
                $"@{extension.Name} {name} {{"
                for prm in prms do
                    match prm.Name with
                    | "context" -> ()
                    | _ ->
                        if prm.Name <> "__dispatch__" then
                            $"    {prm.Name} = {prm.Example}"
                "}"
            "```"

            "### Capabilities"
            ""
            "| Capability | Info |"
            "|------------|------|"
            $"| Cache      | {cacheInfo}"
            $"| Bach      | {batchInfo}"
            ""


            $"## Argument Reference"
            match orderedParameters with
            | [] -> "This command does not accept arguments."
            | prms ->
                "The following arguments are supported:"
                for prm in prms do
                    match prm.Name with
                    | "context" -> ()
                    | _ ->
                        let prmName = if prm.Name = "__dispatch__" then "command" else prm.Name
                        let required = if prm.Required then "Required" else "Optional"
                        $"* `{prmName}` - ({required}) {prm.Summary}"

            match batchCommand with
            | Some batchCommand ->
                "## Batch Reference"
                batchCommand.Summary

                "```"
                match batchCommand.Parameters with
                | [] -> $"@{extension.Name} {name} {{ }}"
                | prms ->
                    $"@{extension.Name} {name} {{"
                    for prm in prms do
                        match prm.Name with
                        | "context" -> ()
                        | _ -> $"    {prm.Name} = {prm.Example}"
                    "}"
                "```"

                ""
                "The following arguments are supported:"
                match batchCommand.Parameters with
                | [] -> "This command does not accept arguments."
                | prms ->
                    for prm in prms do
                        match prm.Name with
                        | "context" -> ()
                        | _ ->
                            let required = if prm.Required then "Required" else "Optional"
                            $"* `{prm.Name}` - ({required}) {prm.Summary}"
            | _ -> ()
        ]
        File.WriteAllLines(commandFile, commandContent)



let writeExtension extensionDir (extension: Extension) =
    // generate extension index
    let extensionFile = Path.Combine(extensionDir, "_index.md")
    if File.Exists extensionFile then
        ()
    else
        let extensionContent = [
            "---"
            $"title: \"{extension.Name}\""
            "---"
            ""
            extension.Summary
            ""
            "## Available Commands"
            match extension.Commands with
            | [] -> "This extension has no commands."
            | _ ->
                "| Command | Description |"
                "|---------|-------------|"
                for cmd in extension.Commands do
                    match cmd.Name with
                    | "__defaults__" ->
                        ()
                    | "__dispatch__" ->
                        $"| [&lt;command&gt;](/docs/extensions/{extension.Name}/{cmd.Name}) | {cmd.Title |> Option.defaultValue cmd.Summary} |"
                    | name when name.StartsWith("__") -> ()
                    | _ ->
                        $"| [{cmd.Name}](/docs/extensions/{extension.Name}/{cmd.Name}) | {cmd.Title |> Option.defaultValue cmd.Summary} |"

                match extension.Commands |> List.tryFind (fun cmd -> cmd.Name = "__defaults__") with
                | Some init ->
                    ""
                    $"## Project Initializer"
                    init.Summary
                    "```"
                    $"project {{"
                    $"  @{extension.Name} {{ }}"
                    "}"
                    "```"
                    "Equivalent to:"
                    "```"
                    $"project {{"
                    for prm in init.Parameters do
                        $"    {prm.Name} = {prm.Example}"
                    "}"
                    "```"

                | _ -> ()
        ]
        File.WriteAllLines(extensionFile, extensionContent)


[<EntryPoint>]
let main args =
    let outputDir, write =
        match args with
        | [| outputDir |] -> outputDir, false
        | [| outputDir; "--write" |] -> outputDir, true
        | _ -> failwith "Usage: DocGen <output-dir> [<--write>]"

    let extensions = buildExtensions ()

    // generate files
    printfn "Generating docs"
    for (KeyValue(_, extension)) in extensions do
        let extensionDir = Path.Combine(outputDir, extension.Name)
        if write && Directory.Exists extensionDir |> not then Directory.CreateDirectory extensionDir |> ignore

        printfn $"  {extension.Name}"
        if write then writeExtension extensionDir extension

        // generate extension commands
        for cmd in extension.Commands do
            let batchCmd = extension.Commands |> List.tryFind (fun x -> x.Name = $"__{cmd.Name}__")
            if write then writeCommand extensionDir cmd batchCmd extension

    // cleanup
    if write then
        printfn "Cleaning output"
        let genExtensions = extensions.Keys |> Set.ofSeq
        let folders =
            Directory.EnumerateDirectories(outputDir)
            |> Seq.map (nonNull << Path.GetFileName)
            |> Set.ofSeq
        let removeFolders = folders - genExtensions
        for folder in removeFolders do
            let folder = Path.Combine(outputDir, folder)
            printfn $"  Removing {folder}"
            Directory.Delete(folder, true)

        for (KeyValue(_, extension)) in extensions do
            let extensionDir = Path.Combine(outputDir, extension.Name)
            let genCommands =
                extension.Commands
                |> List.map (fun cmd -> $"{cmd.Name}.md")
                |> Set.ofSeq
                |> Set.add "_index.md"
            let commands =
                Directory.EnumerateFiles(extensionDir)
                |> Seq.map (nonNull << Path.GetFileName)
                |> Set.ofSeq
            let removeCommands = commands - genCommands
            for command in removeCommands do
                let file = Path.Combine(extensionDir, command)
                printfn $"  Removing {file}"
                File.Delete(file)

    printfn "Done"
    0
