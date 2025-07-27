namespace Terrabuild.Extensions

open Terrabuild.Extensibility


/// <summary>
/// `terraform` extension provides commands to handle a Terraform project.
///
/// {{&lt; callout type="warning" &gt;}}
/// This extension relies on external Terraform state.
/// {{&lt; /callout &gt;}}
/// </summary>
type Terraform() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="ignores" example="[ &quot;.terraform/&quot; &quot;*.tfstate/&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;*.planfile&quot; ]">Default values.</param>
    static member __defaults__() =
        let projectInfo = 
            { ProjectInfo.Default
              with Ignores = Set [ ".terraform/"; "*.tfstate/" ]
                   Outputs = Set [ "*.planfile" ] }
        projectInfo


    /// <summary>
    /// Run a terraform `command`.
    /// </summary>
    /// <param name="__dispatch__" example="fmt">Example.</param>
    /// <param name="arguments" example="&quot;-write=false&quot;">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        let arguments = $"{context.Command} {arguments}"

        let ops = [ shellOp("terraform", arguments) ]
        execRequest(Cacheability.Always, ops)


    /// <summary weight="1">
    /// Init Terraform.
    /// </summary>
    /// <param name="config" example="&quot;backend.prod.config&quot;">Set configuration for init.</param>
    static member init (context: ActionContext) (config: string option) =
        let config =
            match config with
            | Some config -> $" -backend-config={config}"
            | _ -> ""
        let ops = [ shellOp("terraform", $"init -reconfigure{config}") ]
        execRequest(Cacheability.Local, ops)


    /// <summary weight="2" title="Generate plan file.">
    /// This command validates the project:
    /// * initialize Terraform
    /// * select workspace
    /// * run validate
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    static member validate (context: ActionContext) =
        let ops = [
            shellOp("terraform", "init")
            shellOp("terraform", "validate")
        ]
        execRequest(Cacheability.Always, ops)


    /// <summary weight="3" title="Generate plan file.">
    /// This command generates the planfile:
    /// * initialize Terraform
    /// * select workspace
    /// * run plan
    ///
    /// **WARNING: This command generate an ephemeral artifact.**
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    /// <param name="config" example="&quot;backend.prod.config&quot;">Set configuration for init.</param>
    /// <param name="create" example="true">Create workspace if it does not exist.</param>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    static member plan (context: ActionContext) (config: string option) (workspace: string option) (create: bool option) (variables: Map<string, string>) =
        let vars = variables |> Seq.fold (fun acc (KeyValue(key, value)) -> acc + $" -var=\"{key}={value}\"") ""
        let config =
            match config with
            | Some config -> $" -backend-config={config}"
            | _ -> ""

        let create =
            match create with
            | Some true -> "-or-create "
            | _ -> ""

        let ops = [
            shellOp("terraform", $"init -reconfigure{config}")

            match workspace with
            | Some workspace -> shellOp("terraform", $"workspace select {create}{workspace}")
            | _ -> ()

            shellOp("terraform", $"plan -out=terrabuild.planfile{vars}")
        ]
        execRequest(Cacheability.Always ||| Cacheability.Ephemeral, ops)
  

    /// <summary weight="4" title="Apply plan file.">
    /// Apply the plan file:
    /// * initialize Terraform
    /// * select workspace
    /// * apply plan
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    /// <param name="config" example="&quot;backend.prod.config&quot;">Set configuration for init.</param>
    /// <param name="no_plan" example="true">Apply without plan file.</param>
    static member apply (context: ActionContext) (config: string option) (workspace: string option) (no_plan: bool option) =
        let config =
            match config with
            | Some config -> $" -backend-config={config}"
            | _ -> ""

        let planfile =
            match no_plan with
            | Some true -> ""
            | _ -> " terrabuild.planfile"

        let ops = [
            shellOp("terraform", $"init -reconfigure{config}")
            
            match workspace with
            | Some workspace -> shellOp("terraform", $"workspace select {workspace}")
            | _ -> ()

            shellOp("terraform", $"apply -input=false{planfile}")
        ]
        execRequest(Cacheability.Always, ops)

    /// <summary weight="4" title="Destroy the deployment.">
    /// Destroy the deployment:
    /// * initialize Terraform
    /// * select workspace
    /// * destroy
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    /// <param name="config" example="&quot;backend.prod.config&quot;">Set configuration for init.</param>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    static member destroy (context: ActionContext) (config: string option) (workspace: string option) (variables: Map<string, string>) =
        let vars = variables |> Seq.fold (fun acc (KeyValue(key, value)) -> acc + $" -var=\"{key}={value}\"") ""
        let config =
            match config with
            | Some config -> $" -backend-config={config}"
            | _ -> ""

        let ops = [
            shellOp("terraform", $"init -reconfigure{config}")
            
            match workspace with
            | Some workspace -> shellOp("terraform", $"workspace select {workspace}")
            | _ -> ()

            shellOp("terraform", $"destroy -input=false{vars}")
        ]
        execRequest(Cacheability.Always, ops)
