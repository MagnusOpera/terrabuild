namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides wrappers around the Terraform CLI (init/plan/apply/destroy) for projects that manage their own state.
///
/// {{&lt; callout type="warning" &gt;}}
/// This extension relies on external Terraform state.
/// {{&lt; /callout &gt;}}
/// </summary>
type Terraform() =

    /// <summary>
    /// Declares default outputs for Terraform runs.
    /// </summary>
    /// <param name="ignores" example="[ &quot;.terraform/&quot; &quot;*.tfstate/&quot; ]">Default ignore patterns (state and caches).</param>
    /// <param name="outputs" example="[ &quot;*.planfile&quot; ]">Default outputs (plan files).</param>
    static member __defaults__() =
        let projectInfo = 
            { ProjectInfo.Default
              with Outputs = Set [ "*.planfile" ] }
        projectInfo


    /// <summary>
    /// Runs an arbitrary Terraform command (action name is forwarded to `terraform`).
    /// </summary>
    /// <param name="args" example="&quot;fmt -write=false&quot;">Arguments appended after the Terraform subcommand.</param>
    [<NoCacheAttribute>]
    static member __dispatch__ (context: ActionContext)
                               (args: string option) =
        let args = args |> or_default ""

        let ops = [ shellOp("terraform", $"{context.Command} {args}") ]
        ops


    /// <summary weight="1">
    /// Initializes Terraform providers and backend.
    /// </summary>
    /// <param name="config" example="&quot;backend.prod.config&quot;">Backend config file passed to `terraform init -backend-config`.</param>
    /// <param name="args" example="&quot;-upgrade&quot;">Additional arguments for `terraform init`.</param>
    [<LocalCacheAttribute>]
    static member init (config: string option)
                       (args: string option) =
        let config = config |> map_value (fun config -> $"-backend-config={config}")
        let args = args |> or_default ""
    
        let ops = [
            shellOp("terraform", $"init -reconfigure {config} {args}")
        ]
        ops


    /// <summary weight="2" title="Generate plan file.">
    /// Validates configuration before planning/applying.
    /// </summary>
    /// <param name="args" example="&quot;-no-color&quot;">Additional arguments for `terraform validate`.</param>
    [<RemoteCacheAttribute>]
    static member validate (args: string option) =
        let args = args |> or_default ""

        let ops = [
            shellOp("terraform", $"validate {args}")
        ]
        ops




    /// <summary weight="2" title="Select workspace.">
    /// Selects or creates a Terraform workspace before planning/applying.
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    /// <param name="create" example="true">Create the workspace when it does not exist.</param>
    /// <param name="args" example="&quot;-no-color&quot;">Additional arguments for `terraform workspace select`.</param>
    [<NoCacheAttribute>]
    static member select (workspace: string option)
                         (create: bool option)
                         (args: string option) =
        let create = create |> map_true "-or-create"
        let args = args |> or_default ""

        let ops = [
            match workspace with
            | Some workspace -> shellOp("terraform", $"workspace select {create} {workspace} {args}")
            | _ -> ()
        ]
        ops
  

    /// <summary weight="3" title="Generate plan file.">
    /// Generates a plan file for review/apply.
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables passed as `-var` assignments.</param> 
    /// <param name="args" example="&quot;-no-color&quot;">Additional arguments for `terraform plan`.</param>
    [<RemoteCache>]
    static member plan (variables: Map<string, string> option)
                       (args: string option) =
        let vars = variables |> format_space (fun kvp -> $"-var=\"{kvp.Key}={kvp.Value}\"")
        let args = args |> or_default ""

        let ops = [
            shellOp("terraform", $"plan -out=terrabuild.planfile {vars} {args}")
        ]
        ops
  

    /// <summary weight="4" title="Apply plan file.">
    /// Applies the generated plan (or runs `apply` without a plan when requested).
    /// </summary>
    /// <param name="no_plan" example="true">Run `apply` without `-out` plan file.</param>
    /// <param name="args" example="&quot;-no-color&quot;">Additional arguments for `terraform apply`.</param>
    [<RemoteCacheAttribute>]
    static member apply (no_plan: bool option)
                        (args: string option) =
        let planfile = no_plan |> map_false "terrabuild.planfile"
        let args = args |> or_default ""

        let ops = [
            shellOp("terraform", $"apply -input=false {planfile} {args}")
        ]
        ops

    /// <summary weight="4" title="Destroy the deployment.">
    /// Destroys all managed resources for the current workspace.
    /// </summary>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables passed as `-var` assignments.</param> 
    /// <param name="args" example="&quot;&quot;">Additional arguments for `terraform destroy`.</param>
    [<RemoteCacheAttribute>]
    static member destroy (variables: Map<string, string> option)
                          (args: string option) =
        let vars = variables |> format_space (fun kvp -> $"-var=\"{kvp.Key}={kvp.Value}\"")
        let args = args |> or_default ""

        let ops = [
            shellOp("terraform", $"destroy -input=false {vars} {args}")
        ]
        ops
