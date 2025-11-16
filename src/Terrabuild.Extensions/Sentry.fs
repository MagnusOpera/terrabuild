namespace Terrabuild.Extensions
open Terrabuild.Extensibility
open Converters


/// <summary>
/// Uploads source maps and assets with **sentry-cli**, using Debug IDs for mapping.
/// </summary>
type Sentry() =

    /// <summary>
    /// Injects and uploads sourcemaps for JavaScript bundles using Debug IDs.
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
