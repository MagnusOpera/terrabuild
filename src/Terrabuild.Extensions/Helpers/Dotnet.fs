module DotnetHelpers
    #nowarn "0077" // op_Explicit

    open System.Xml.Linq
    open Errors

    let private NsNone = XNamespace.None

    let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

    let private ext2projType = Map [ (".csproj",  "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC")
                                     (".fsproj",  "F2A71F9B-5D33-465A-A702-920D77279786")
                                     (".vbproj",  "F184B08F-C81C-45F6-A57F-5ABD9991F28F") 
                                     (".pssproj", "F5034706-568F-408A-B7B3-4D38C6DB8A32")
                                     (".sqlproj", "00D1A9C2-B5F0-4AF3-8072-F6C62B433612")
                                     (".dcproj",  "E53339B2-1760-4266-BCC7-CA923CBCF16C")]



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

