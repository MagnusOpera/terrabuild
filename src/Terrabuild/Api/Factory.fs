module Api.Factory
open System.Net
open Errors

let checkAuthError msg f  =
    try
        f()
    with
    | exn ->
        let errorCode =
            match exn.InnerException with
            | :? WebException as innerEx ->
                match innerEx.Response with
                | :? HttpWebResponse as hwr -> hwr.StatusCode.ToString()
                | _ -> exn.Message
            | _ -> exn.Message

        match errorCode with
        | "401" -> forwardAuthError($"Unauthorized access.", exn)
        | "403" -> forwardAuthError($"Forbidden access.", exn)
        | "500" -> forwardExternalError($"Internal server error.", exn)
        | _ -> forwardExternalError($"Api failed with error {errorCode}.", exn)


let create workspaceId token options =
    match workspaceId, token with
    | Some workspaceId , Some token ->
        checkAuthError
            $"please check permissions with your administrator to access workspace {workspaceId}"
            (fun() -> Client(workspaceId, token, options) :> Contracts.IApiClient |> Some)
    | _ ->
        None
