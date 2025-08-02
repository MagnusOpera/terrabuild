
module Converters



let concat_quote args =
    args |> Option.map (String.join " " << Seq.map (fun x -> $"\"{x}\"")) |> Option.defaultValue ""

let concat_space args =
    args |> Option.map (String.join " ") |> Option.defaultValue ""

let concat_comma args =
    args |> Option.map (String.join ",") |> Option.defaultValue ""

let or_default defaultValue arg =
    arg |> Option.defaultValue defaultValue

let format_space f args =
    args |> Option.map (String.join " " << Seq.map f) |> or_default ""

let format_comma f args =
    args |> Option.map (String.join "," << Seq.map f) |> or_default ""

let map_true value arg =
    match arg with
    | Some true -> value
    | _ -> ""

let map_false value arg =
    match arg with
    | Some true -> ""
    | _ -> value

let map_value f arg =
    match arg with
    | Some arg -> f arg
    | _ -> ""

let map_non_empty f arg =
    match arg with
    | "" -> ""
    | arg -> f arg
