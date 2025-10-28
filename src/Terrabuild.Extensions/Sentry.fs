namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides support for running sentry cli commands.
/// </summary>
type Sentry() =

    /// <summary>
    /// Publish source map as new release.
    /// </summary>
    /// <param name="org" example="&quot;magnusopera&quot;">Organization slug.</param> 
    /// <param name="project" example="&quot;terrabuild&quot;">Project slug.</param> 
    /// <param name="version" example="&quot;24ab0640&quot;">Release identifier - default value is project hash.</param> 
    /// <param name="args" example="&quot;&quot;">Arguments to pass to sentry cli.</param> 
    [<ExternalCacheAttribute>]
    static member release (context: ActionContext)
                          (org: string option)
                          (project: string option)
                          (version: string option) =
        let org = org |> map_value (fun org -> $"--org '{org}'")
        let project = project |> map_value (fun project -> $"--project '{project}'")
        let version = version |> or_default context.Hash

        let ops = [
            shellOp( "npx", $"sentry-cli {org} {project} releases new '{version}'")
            shellOp( "npx", $"sentry-cli {org} {project} releases files '{version}' upload-sourcemaps dist --rewrite")
            shellOp( "npx", $"sentry-cli {org} {project} releases finalize '{version}'")
        ]
        ops
