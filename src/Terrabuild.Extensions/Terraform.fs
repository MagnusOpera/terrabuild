namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


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
    /// <param name="args" example="[ &quot;-write=false&quot; ]">Arguments for command.</param>
    static member __dispatch__ (context: ActionContext)
                               (args: string list option) =
        let args = args |> concat_quote

        let ops = [ shellOp("terraform", $"{context.Command} {args}") ]
        ops |> execRequest Cacheability.Always


    /// <summary weight="1">
    /// Init Terraform.
    /// </summary>
    /// <param name="config" example="&quot;backend.prod.config&quot;">Set configuration for init.</param>
    /// <param name="args" example="[ &quot;-upgrade&quot; ]">Arguments for command.</param>
    static member init (config: string option)
                       (args: string list option) =
        let config = config |> map_value (fun config -> $"-backend-config={config}")
        let args = args |> concat_quote
    
        let ops = [
            shellOp("terraform", $"init -reconfigure {config} {args}")
        ]
        ops |> execRequest Cacheability.Local


    /// <summary weight="2" title="Generate plan file.">
    /// Validate project.
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    /// <param name="args" example="[ &quot;-no-color&quot; ]">Arguments for command.</param>
    static member validate (args: string list option) =
        let args = args |> concat_quote

        let ops = [
            shellOp("terraform", $"validate {args}")
        ]
        ops |> execRequest Cacheability.Always




    /// <summary weight="2" title="Select workspace.">
    /// Select a workspace.
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    /// <param name="create" example="true">Create workspace if it does not exist.</param>
    /// <param name="args" example="[ &quot;-no-color&quot; ]">Arguments for command.</param>
    static member select (workspace: string option)
                         (create: bool option)
                         (args: string list option) =
        let create = create |> map_true "-or-create"
        let args = args |> concat_quote

        let ops = [
            match workspace with
            | Some workspace -> shellOp("terraform", $"workspace select {create} {workspace} {args}")
            | _ -> ()
        ]
        ops |> execRequest Cacheability.Local
  

    /// <summary weight="3" title="Generate plan file.">
    /// Generate the planfile.
    /// 
    /// {{&lt; callout type="warning" &gt;}}
    /// This extension generate an [ephemeral artifact](/docs/getting-started/artifacts/#ephemeral-artifacts).
    /// {{&lt; /callout &gt;}}
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    /// <param name="args" example="[ &quot;-no-color&quot; ]">Arguments for command.</param>
    static member plan (variables: Map<string, string> option)
                       (args: string list option) =
        let vars = variables |> format_space (fun kvp -> $"-var=\"{kvp.Key}={kvp.Value}\"")
        let args = args |> concat_quote

        let ops = [
            shellOp("terraform", $"plan -out=terrabuild.planfile {vars} {args}")
        ]
        ops |> execRequest (Cacheability.Always ||| Cacheability.Ephemeral)
  

    /// <summary weight="4" title="Apply plan file.">
    /// Apply the plan file.
    /// </summary>
    /// <param name="no_plan" example="true">Apply without plan file.</param>
    /// <param name="args" example="[ &quot;-no-color&quot; ]">Arguments for command.</param>
    static member apply (no_plan: bool option)
                        (args: string list option) =
        let planfile = no_plan |> map_false "terrabuild.planfile"
        let args = args |> concat_quote

        let ops = [
            shellOp("terraform", $"apply -input=false {planfile} {args}")
        ]
        ops |> execRequest Cacheability.Always

    /// <summary weight="4" title="Destroy the deployment.">
    /// Destroy the deployment.
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    /// <param name="args" example="[  ]">Arguments for command.</param>
    static member destroy (variables: Map<string, string> option)
                          (args: string list option) =
        let vars = variables |> format_space (fun kvp -> $"-var=\"{kvp.Key}={kvp.Value}\"")
        let args = args |> concat_quote

        let ops = [
            shellOp("terraform", $"apply -destroy -input=false {vars} {args}")
        ]
        ops |> execRequest Cacheability.Always
