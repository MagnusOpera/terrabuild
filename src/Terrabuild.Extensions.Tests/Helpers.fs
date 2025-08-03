
module TestHelpers
open Terrabuild.Extensibility

let someArgs = Some "--opt1 --opt2"
let noneArgs = None

let normalize (request: Terrabuild.Extensibility.ActionExecutionRequest) =
    { request with
        Operations = request.Operations |> List.map (fun op -> 
            { op with Arguments = op.Arguments |> String.normalizeShellArgs }) }

let ciContext =
    { ActionContext.Debug = true
      ActionContext.CI = true
      ActionContext.Command = "ci-command"
      ActionContext.Hash = "ABCDEF123456789" }

let localContext =
    { ActionContext.Debug = false
      ActionContext.CI = false
      ActionContext.Command = "local-command"
      ActionContext.Hash = "123456789ABCDEF" }

let someMap = [ "prm1", "val1"
                "prm2", "val2" ] |> Map |> Some

let noneMap = None

