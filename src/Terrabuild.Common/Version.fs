module Version

open System
open System.Text.RegularExpressions

type Semver =
    { Major: int
      Minor: int
      Patch: int
      Pre: string option }

let parseSemver(input: string) : Semver =
    let m = Regex.Match(input, @"^(?<nums>\d+(?:\.\d+){0,2})(?:-(?<pre>[0-9A-Za-z\-\.]+))?$")
    if m.Success then
        let parts = m.Groups.["nums"].Value.Split('.')
        let major = int parts.[0]
        let minor = if parts.Length > 1 then int parts.[1] else 0
        let patch = if parts.Length > 2 then int parts.[2] else 0
        let pre = if m.Groups.["pre"].Success then Some m.Groups.["pre"].Value else None
        { Major = major; Minor = minor; Patch = patch; Pre = pre }
    else
        Errors.raiseInvalidArg $"Invalid version specification '{input}'"

let compareSemver (a: Semver) (b: Semver) =
    let coreCmp =
        compare (a.Major, a.Minor, a.Patch) (b.Major, b.Minor, b.Patch)
    if coreCmp <> 0 then coreCmp
    else
        match a.Pre, b.Pre with
        | None, None -> 0
        | None, Some _ -> 1   // stable > prerelease
        | Some _, None -> -1  // prerelease < stable
        | Some ap, Some bp -> String.Compare(ap, bp, StringComparison.Ordinal)

let isAtLeast (required: string) (actual: string) =
    let req = parseSemver required
    let act = parseSemver actual
    compareSemver act req >= 0
