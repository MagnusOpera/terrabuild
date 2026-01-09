module Auth
open System


type SpaceAuth = {
    Id: string
    Token: string
    MasterKey: string
}

[<RequireQualifiedAccess>]
type Configuration = {
    SpaceAuths: SpaceAuth list
}


let private removeAuthToken (workspaceId: string) =
    let configFile = FS.combinePath (Cache.createTerrabuildProfile()) "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.SpaceAuths = List.empty }

    let config = { config with SpaceAuths = config.SpaceAuths |> List.filter (fun sa -> sa.Id = workspaceId )}

    config
    |> Json.Serialize
    |> IO.writeTextFile configFile


let private addAuthToken (workspaceId: string) (token: string) (masterKey: string) =
    let configFile = FS.combinePath (Cache.createTerrabuildProfile()) "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { SpaceAuths = [] }

    // remove previous config
    let config =
        { config with SpaceAuths = config.SpaceAuths |> List.filter (fun x -> x.Id <> workspaceId ) }

    let config =
        { config with SpaceAuths = { Id = workspaceId; Token = token; MasterKey = masterKey } :: config.SpaceAuths }

    config
    |> Json.Serialize
    |> IO.writeTextFile configFile


let readAuth (workspaceId: string) =
    let configFile = FS.combinePath (Cache.createTerrabuildProfile()) "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.SpaceAuths = List.empty }

    match config.SpaceAuths |> List.tryFind (fun sa -> sa.Id = workspaceId) with
    | Some spaceAuth -> Some spaceAuth
    | _ -> None



let login workspaceId token masterKey =
    Api.Factory.create (Some workspaceId) (Some token) |> ignore
    addAuthToken workspaceId token masterKey

let logout space =
    removeAuthToken space
