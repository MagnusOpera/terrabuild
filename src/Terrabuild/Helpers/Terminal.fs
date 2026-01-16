module Terminal
open System
open System.Threading

let private terms = [
    "^xterm" // xterm, PuTTY, Mintty
    "^rxvt" // RXVT
    "^eterm" // Eterm
    "^screen" // GNU screen, tmux
    "tmux" // tmux
    "^vt100" // DEC VT series
    "^vt102" // DEC VT series
    "^vt220" // DEC VT series
    "^vt320" // DEC VT series
    "ansi" // ANSI
    "scoansi" // SCO ANSI
    "cygwin" // Cygwin, MinGW
    "linux" // Linux console
    "konsole" // Konsole
    "bvterm" // Bitvise SSH Client
    "^st-256color" // Suckless Simple Terminal, st
    "alacritty" // Alacritty
]

let private forceAnsi =
    match System.Environment.GetEnvironmentVariable("TB_FORCE_ANSI") |> Option.ofObj with
    | Some "1" -> true
    | Some "true" -> true
    | Some "TRUE" -> true
    | _ -> false

let private silent =
    match System.Environment.GetEnvironmentVariable("TB_SILENT") |> Option.ofObj with
    | Some "1"
    | Some "true"
    | Some "TRUE" -> true
    | _ -> false

let mutable private runtimeMuted = false

let private isMuted () =
    silent || Volatile.Read(&runtimeMuted)

let mute () =
    Volatile.Write(&runtimeMuted, true)

let unmute () =
    Volatile.Write(&runtimeMuted, false)

let supportAnsi =
    (forceAnsi || not Console.IsOutputRedirected)
    &&
    match System.Environment.GetEnvironmentVariable("TERM") |> Option.ofObj with
    | Some currTerm ->
        terms |> List.exists (fun term -> 
            match currTerm with
            | String.Regex term _ -> true
            | _ -> false)
    | _ -> false


let flush () =
    if not (isMuted ()) then
        Console.Out.Flush()

let write (str: string) =
    if not (isMuted ()) then
        Console.Out.Write(str)

let writeLine (str: string) =
    if not (isMuted ()) then
        Console.Out.WriteLine(str)

let hideCursor() =
    if supportAnsi && not (isMuted ()) then Ansi.Styles.cursorHide |> write

let showCursor() =
    if supportAnsi then Ansi.Styles.cursorShow |> write

let autoflush() =
    if supportAnsi && not (isMuted ()) then
        new IO.StreamWriter(Console.OpenStandardOutput(), AutoFlush = true)
        |> Console.SetOut
