module SemVer

open System
open System.Text.RegularExpressions

/// Parses a version string like "0.174.0" or "0.174.0-next" into (Version, prerelease option)
let parseSemver (input: string) =
    let m = Regex.Match(input, @"^(?<core>\d+\.\d+\.\d+)(?:-(?<pre>[0-9A-Za-z\-\.]+))?$")
    if m.Success then
        let core = Version(m.Groups.["core"].Value)
        let pre = if m.Groups.["pre"].Success then Some m.Groups.["pre"].Value else None
        (core, pre)
    else
        Errors.raiseInvalidArg $"Invalid version specification '{input}'"

/// Returns true if actual >= required (semver-style comparison).
let isAtLeast (required: string) (actual: string) =
    let (reqCore, reqPre) = parseSemver required
    let (actCore, actPre) = parseSemver actual
    let cmp = actCore.CompareTo(reqCore)
    if cmp > 0 then true
    elif cmp < 0 then false
    else
        match reqPre, actPre with
        | None, None -> true                  // both stable, equal
        | Some _, None -> true                // actual is stable, required was prerelease
        | None, Some _ -> false               // required stable, actual prerelease
        | Some r, Some a -> String.Compare(a, r, StringComparison.Ordinal) >= 0
