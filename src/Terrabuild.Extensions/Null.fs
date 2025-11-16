namespace Terrabuild.Extensions
open Terrabuild.Extensibility

/// <summary>
/// A no-op extension used for testing or wiring minimal pipelines. Exposes fake init/dispatch to validate Terrabuild plumbing without side effects.
/// </summary>
type Null() =

    /// <summary>
    /// Returns default project metadata without touching the filesystem.
    /// </summary>
    static member __defaults__ (context: ExtensionContext) =
        ProjectInfo.Default

    /// <summary>
    /// Fake dispatch.
    /// </summary>
    static member __dispatch__ (context: ActionContext) =
        ()

    /// <summary>
    /// Fake action.
    /// </summary>
    static member fake (context: ActionContext) =
        ()
