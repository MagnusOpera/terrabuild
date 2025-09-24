module DotnetHelpers
    #nowarn "0077" // op_Explicit

    open System.Xml.Linq
    open Errors
    open System.IO

    let private NsNone = XNamespace.None

    let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

    let private ext2projType = Map [ (".csproj",  "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC")
                                     (".fsproj",  "F2A71F9B-5D33-465A-A702-920D77279786")
                                     (".vbproj",  "F184B08F-C81C-45F6-A57F-5ABD9991F28F") 
                                     (".pssproj", "F5034706-568F-408A-B7B3-4D38C6DB8A32")
                                     (".sqlproj", "00D1A9C2-B5F0-4AF3-8072-F6C62B433612")
                                     (".dcproj",  "E53339B2-1760-4266-BCC7-CA923CBCF16C")]



    let ext2ProjectType ext = ext2projType |> Map.tryFind ext


    let findProjectFile (directory: string) =
        let projects =
            ext2projType.Keys
            |> Seq.map (fun k -> $"*{k}")
            |> Seq.collect (fun ext -> System.IO.Directory.EnumerateFiles(directory, ext))
            |> List.ofSeq
        match projects with
        | [ project ] -> project
        | [] -> raiseInvalidArg "No project found"
        | _ -> raiseInvalidArg "Multiple projects found"

    let findDependencies (projectFile: string) =
        let xdoc = XDocument.Load (projectFile)
        let refs =
            xdoc.Descendants() 
            |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
            |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string | null)
            |> Seq.choose Option.ofObj
            |> Seq.map (fun x -> x.Replace("\\", "/"))
            |> Seq.map (Option.get << FS.parentDirectory)
            |> Seq.distinct
            |> List.ofSeq
        Set refs 

    [<Literal>]
    let defaultConfiguration = "Debug"




    let generateGuidFromString (input : string) =
        use md5 = System.Security.Cryptography.MD5.Create()
        let inputBytes = System.Text.Encoding.GetEncoding(0).GetBytes(input)
        let hashBytes = md5.ComputeHash(inputBytes)
        let hashGuid = System.Guid(hashBytes)
        hashGuid

    let toVSGuid (guid : System.Guid) =
        guid.ToString("D").ToUpperInvariant()


    let generateSolutionContent (projectDirs : string list) (configuration: string) =
        let string2guid s =
            s
            |> generateGuidFromString 
            |> toVSGuid

        let projects = projectDirs |> List.map findProjectFile

        let guids =
            projects
            |> Seq.map (fun x -> x, string2guid x)
            |> Map

        seq {
            yield "Microsoft Visual Studio Solution File, Format Version 12.00"
            yield "# Visual Studio 17"

            for project in projects do
                let fileName = project
                let projectType = fileName |> Path.GetExtension |> nonNull |> ext2ProjectType
                match projectType with
                | Some prjType -> yield sprintf @"Project(""{%s}"") = ""%s"", ""%s"", ""{%s}"""
                                    prjType
                                    (fileName |> Path.GetFileNameWithoutExtension)
                                    fileName
                                    (guids[fileName])
                                  yield "EndProject"
                | None -> failwith $"Unsupported project {fileName}"

            yield "Global"

            yield "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution"
            yield $"\t\t{configuration}|Any CPU = {configuration}|Any CPU"
            yield "\tEndGlobalSection"

            yield "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution"
            for project in projects do
                let guid = guids[project]
                yield $"\t\t{{{guid}}}.{configuration}|Any CPU.ActiveCfg = {configuration}|Any CPU"
                yield $"\t\t{{{guid}}}.{configuration}|Any CPU.Build.0 = {configuration}|Any CPU"
            yield "\tEndGlobalSection"

            yield "EndGlobal"
        }


    let writeSolutionFile (projectDirs : string list) (configuration: string) (slnFile: string) =
        let content = generateSolutionContent projectDirs configuration
        IO.writeLines  slnFile content

