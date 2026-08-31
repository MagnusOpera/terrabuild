module String

open System
open System.Text.RegularExpressions
open System.Text


let toLower (s : string) =
    s.ToLowerInvariant()

let toUpper (s : string) =
    s.ToUpperInvariant()

let join (separator : string) (strings : string seq) =
    String.Join(separator, strings)

let firstLine (input: string) =
    input.Split([| "\r\n"; "\n" |], StringSplitOptions.None)[0]

let getLines (input: string) =
    input.Split([| "\r\n"; "\n" |], StringSplitOptions.RemoveEmptyEntries)

let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None

let cut m (s: string) =
    if s.Length > m then s.Substring(0, m) + "..."
    else s

let startsWith (start: string) (s: string) =
    s.StartsWith(start)

let trim (s: string) =
    s.Trim()

let replace (substring: string) (value: string) (s: string) =
    s.Replace(substring, value)

let normalizeShellArgs (input: string) : string =
    let sb = StringBuilder()
    let mutable inQuotes = false
    let mutable lastWasSpace = false

    for ch in input do
        match ch with
        | '"' ->
            inQuotes <- not inQuotes
            sb.Append(ch) |> ignore
            lastWasSpace <- false
        | ' ' when not inQuotes ->
            if not lastWasSpace then
                sb.Append(' ') |> ignore
                lastWasSpace <- true
        | _ ->
            sb.Append(ch) |> ignore
            lastWasSpace <- false

    sb.ToString().Trim()

let splitShellArgs (input: string) =
    let args = ResizeArray<string>()
    let current = StringBuilder()
    let mutable quote: char option = None
    let mutable started = false

    let complete () =
        if started then
            args.Add(current.ToString())
            current.Clear() |> ignore
            started <- false

    let mutable index = 0
    while index < input.Length do
        let ch = input[index]
        match quote, ch with
            | Some '\'', '\''
            | Some '"', '"' ->
                quote <- None
                started <- true
            | Some '\'', _ ->
                current.Append(ch) |> ignore
                started <- true
            | Some '"', '\\' ->
                if index + 1 < input.Length && (input[index + 1] = '\\' || input[index + 1] = '"') then
                    index <- index + 1
                    current.Append(input[index]) |> ignore
                else
                    current.Append(ch) |> ignore
                started <- true
            | Some '"', _ ->
                current.Append(ch) |> ignore
                started <- true
            | None, ('\'' | '"') ->
                quote <- Some ch
                started <- true
            | None, '\\' ->
                if index + 1 < input.Length then
                    index <- index + 1
                    current.Append(input[index]) |> ignore
                else
                    current.Append(ch) |> ignore
                started <- true
            | None, ch when Char.IsWhiteSpace(ch) -> complete ()
            | None, _ ->
                current.Append(ch) |> ignore
                started <- true
            | Some _, _ -> failwith "Unsupported command-line quote"
        index <- index + 1

    if quote.IsSome then invalidArg (nameof input) "Unterminated quote in command arguments"
    complete ()
    args |> List.ofSeq

let slugify (s: string) =
    let replace (m: string) (r: string) (s: string) = Regex.Replace(s, m, r)
    let s =
        s|> replace @"([a-z0-9])([A-Z])" "$1-$2"
        |> replace @"[^a-zA-Z0-9-]" "-"
    s.Trim('-').ToLowerInvariant()
