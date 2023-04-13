module pscomplete.Helpers

open System
open System.Management.Automation
open System.Management.Automation.Host
open System.Text.Json
open System.Linq
open System.Text.RegularExpressions

module Chars =
    [<Literal>]
    let topLeftDouble = '╔'

    [<Literal>]
    let verticalDouble = '║'

    [<Literal>]
    let horizontalDouble = '═'

    [<Literal>]
    let topRightDouble = '╗'

    [<Literal>]
    let bottomLeftDouble = '╚'

    [<Literal>]
    let bottomRightDouble = '╝'


module Graphics =
    let boxTop length content = $"╔══ {content} ".PadRight(length + 1, '═') + "╗"


    let boxCenter length content = $"║{content}".PadRight(length + 1, ' ') + "║"


    let boxBottom length content = $"╚══ {content} ".PadRight(length + 1, '═') + "╝"


module PsArgument =
    let getText (v: string) =
        let argtype = v
        let startidx = argtype.IndexOf(": [")

        if startidx = -1 then
            ""
        else
            argtype.Substring(startidx + ": [".Length)
            |> (fun f -> f[.. f.Length - 2])


let bufferCell char =
    BufferCell(
        Character = char,
        BufferCellType = BufferCellType.Complete,
        ForegroundColor = ConsoleColor.White
    )


type PlatformKind =
    | Win
    | Unix

let Platform =
    match Environment.OSVersion.Platform with
    | PlatformID.Win32NT -> Win
    | _ -> Unix



type PsCompletion() =

    /// tooltips enclosed in [] are displayed
    /// (usually contains argument type e.g [string array])
    static member toText(res: CompletionResult) =
        let typeinfo =
            res.ToolTip
            |> (fun f -> f.Replace("[]", " array"))
            |> (fun f ->
                if f.StartsWith "[" then
                    ": " + f[.. f.IndexOf("]")]
                elif f.StartsWith "\n" then
                    Regex.Matches(f, "\[-").Count
                    |> (fun f -> $": %i{f}")
                else
                    "")

        $"{res.ListItemText} {typeinfo}"

let readkeyopts =
    ReadKeyOptions.NoEcho
    ||| ReadKeyOptions.AllowCtrlC
    ||| ReadKeyOptions.IncludeKeyDown


// let saveDebugState state =
//    state |> JsonSerializer.Serialize
//    |> (fun f -> System.IO.File.WriteAllText(@"C:\Users\kast\dev\s.json",f))

module String =
    open System.Text
    let rec longestCommonPrefix s1 s2 =
        let rec loop (acc:StringBuilder) s1 s2 =
            match s1, s2 with
            | "", _ -> acc
            | _, "" -> acc
            | _ ->
                if s1.[0] = s2.[0] then
                    loop (acc.Append s1.[0]) (s1.Substring(1)) (s2.Substring(1))
                else
                    acc
        (loop (StringBuilder()) s1 s2).ToString()
        
module Seq =
    let longestCommonPrefix (seq: seq<string>) =
        seq |> Seq.reduce (fun acc v -> 
            String.longestCommonPrefix acc v
        )
        
