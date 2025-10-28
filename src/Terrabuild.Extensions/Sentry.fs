namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Provides support for running sentry-cli commands.
/// </summary>
type Sentry() =

    /// <summary>
    /// Inject and upload sourcemaps using Debug IDs.
    /// </summary>
    /// <param name="project" example="&quot;insights&quot;">Project slug.</param> 
    /// <param name="path" example="&quot;dist&quot;">Sourcemaps path. Default value is dist.</param> 
    [<ExternalCacheAttribute>]
    static member sourcemaps (project: string option)
                             (path: string option) =
        let project = project |> map_value (fun project -> $"--project {project}")
        let path = path |> or_default "dist"

        let ops = [
            shellOp( "sentry-cli", $"sourcemaps inject {path}")
            shellOp( "sentry-cli", $"sourcemaps upload {project} {path}")
        ]
        ops
