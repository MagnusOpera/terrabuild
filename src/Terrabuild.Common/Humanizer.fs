module Humanizer

open System

[<AutoOpen>]
module TimeSpanExtensions =

    type TimeSpan with
        member ts.HumanizeAbbreviated() =
            let parts =
                [ if ts.Days > 0 then yield $"{ts.Days}d"
                  if ts.Hours > 0 then yield $"{ts.Hours}h"
                  if ts.Minutes > 0 then yield $"{ts.Minutes}m"
                  if ts.Seconds > 0 || (ts.Days = 0 && ts.Hours = 0 && ts.Minutes = 0)
                      then yield $"{ts.Seconds}s" ]
            String.Join(" ", parts)
