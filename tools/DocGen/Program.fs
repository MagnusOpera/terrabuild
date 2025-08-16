open System.IO
open System.Text.RegularExpressions
open FSharp.Data
open System.Reflection
open Terrabuild.Extensibility


type Documentation = XmlProvider<"Examples/Terrabuild.Extensions.xml">

let load (filename: string) =
    let content = File.ReadAllText(filename)
    let result = Documentation.Parse(content)
    result


type Parameter = {
    Name: string
    Required: bool
    Summary: string
    Example: string
}

type Command = {
    Name: string
    Weight: int option
    Title: string option
    Cacheability: Cacheability option
    Summary: string
    mutable Parameters: Parameter list
}

type Extension = {
    Name: string
    Summary: string
    mutable Commands: Command list
}


let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None


let (|Extension|Command|) s =
    match s with
    // T:Terrabuild.Extensions.Docker
    | Regex "^T:Terrabuild\.Extensions\.(.+)$" [name] -> Extension (name.ToLowerInvariant())

    // M:Terrabuild.Extensions.Terraform.plan(Microsoft.FSharp.Core.FSharpOption{System.String})
    | Regex "^M:Terrabuild\.Extensions\.([^.]+)\.([^(]+)" [extension; name] -> Command (extension.ToLowerInvariant(), name.ToLowerInvariant())
    | _ -> failwith $"Unknown member kind: {s}"


let getCacheInfo (methodInfo: MethodInfo) =
    match methodInfo.GetCustomAttribute(typeof<CacheableAttribute>) with
    | :? CacheableAttribute as attr -> Some attr.Cacheability
    | _ -> None


let buildExtensions (assembly: Assembly) (members: Documentation.Member seq) =
    // first find extensions
    let extensions =
        members
        |> Seq.choose (fun m ->
            match m.Name with
            | Extension name when name <> "null" -> Some { Name = name; Summary = m.Summary.Value; Commands = List.empty }
            | _ -> None)
        |> Seq.map (fun ext -> ext.Name, ext)
        |> Map.ofSeq

    // add members
    members
    |> Seq.iter (fun m ->
        match m.Name with
        | Command (extension, name) ->
            let fullTypename = "terrabuild.extensions." + extension
            let extensionType = assembly.GetTypes() |> Seq.find (fun t -> t.FullName.ToLowerInvariant() = fullTypename)
            let methodInfo = extensionType.GetMethod(name, BindingFlags.Public ||| BindingFlags.Static)
            let methodArgs =
                methodInfo.GetParameters() |> Seq.map (fun p -> p.Name) |> Set.ofSeq
                |> Set.remove "context"

            let cacheability = getCacheInfo methodInfo

            match extensions |> Map.tryFind extension with
            | None -> if extension <> "null" then failwith $"Extension {extension} does not exist"
            | Some ext ->
                let prms =
                    m.Params
                    |> Option.ofObj
                    |> Option.defaultValue Array.empty
                    |> Seq.map (fun prm -> { Name = prm.Name
                                             Summary = prm.Value.Trim()
                                             Required = prm.Required |> Option.defaultValue false
                                             Example = prm.Example.Value })
                    |> List.ofSeq

                let prmNames =
                    prms |> List.map (fun prm -> prm.Name) |> Set.ofList
                    |> Set.remove "__dispatch__"
                    |> Set.remove "context"
                if name <> "__defaults__" && prmNames <> methodArgs then
                    failwith $"Undocumented members on {extension}.{name}"

                let cmd = { Name = name
                            Title = m.Summary.Title
                            Summary = m.Summary.Value.Trim()
                            Cacheability = cacheability
                            Parameters = prms
                            Weight = m.Summary.Weight }
                ext.Commands <- ext.Commands @ [cmd]
        | _ -> ())

    extensions
    |> Map.iter (fun _ ext -> ext.Commands <- ext.Commands |> List.sortBy (fun x -> x.Weight))

    extensions



let writeCommand extensionDir (command: Command) (batchCommand: Command option) (extension: Extension) =

    let cacheabilityInfo =
        match command.Cacheability with
        | None -> []
        | Some Cacheability.Never ->
            [
                ""
                "{{< callout type=\"exclamation\" >}}"
                "This command is **not cacheable**."
                "{{< /callout >}}"
                ""
            ]
        | Some Cacheability.Local ->
            [
                ""
                "{{< callout type=\"info\" >}}"
                "This command is **locally cacheable** only."
                "{{< /callout >}}"
                ""
            ]
        | Some Cacheability.Remote ->
            [
                ""
                "{{< callout type=\"info\" >}}"
                "This command is **fully cacheable**."
                "{{< /callout >}}"
                ""
            ]

    match command.Name with
    | "__defaults__" -> ()
    | _ ->
        let commandFile = Path.Combine(extensionDir, $"{command.Name}.md")
        let commandContent = [
            "---"
            match command.Name with
            | "__dispatch__" -> $"title: \"<command>\""
            | _ -> $"title: \"{command.Name}\""
            if command.Weight |> Option.isSome then $"weight: {command.Weight.Value}"
            "---"
            ""
            yield! cacheabilityInfo
            command.Summary

            let name =
                match command.Parameters |> List.tryFind (fun x -> x.Name = command.Name) with
                | Some nameOverride -> nameOverride.Example
                | _ -> command.Name

            match command.Name with
            | "__dispatch__" -> $"Example for command `{name}`:"
            | _ -> ()

            "```"
            match command.Parameters with
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

            $"## Argument Reference"
            match command.Parameters with
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
    if args.Length <> 2 then failwith "Usage: DocGen <xml-doc-file> <output-dir>"
    let doc = load args[0]
    let outputDir = args[1]
    if doc.Assembly.Name <> "Terrabuild.Extensions" then failwith "Expecting documentation for Terrabuild.Extensions"

    let members = doc.Members |> Option.ofObj |> Option.defaultValue Array.empty
    let assemblyFile = Path.ChangeExtension(args[0], "dll")
    let assembly = System.Reflection.Assembly.LoadFrom assemblyFile
    let extensions = buildExtensions assembly members

    // generate files
    printfn "Generating docs"
    for (KeyValue(_, extension)) in extensions do
        let extensionDir = Path.Combine(outputDir, extension.Name)
        if Directory.Exists extensionDir |> not then Directory.CreateDirectory extensionDir |> ignore

        printfn $"  {extension.Name}"
        writeExtension extensionDir extension

        // generate extension commands
        for cmd in extension.Commands do
            let batchCmd = extension.Commands |> List.tryFind (fun x -> x.Name = $"__{cmd.Name}__")
            writeCommand extensionDir cmd batchCmd extension

    // cleanup
    printfn "Cleaning output"
    let genExtensions = extensions.Keys |> Set.ofSeq
    let folders =
        Directory.EnumerateDirectories(outputDir)
        |> Seq.map Path.GetFileName
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
            |> Seq.map Path.GetFileName
            |> Set.ofSeq
        let removeCommands = commands - genCommands
        for command in removeCommands do
            let file = Path.Combine(extensionDir, command)
            printfn $"  Removing {file}"
            File.Delete(file)

    printfn "Done"
    0
