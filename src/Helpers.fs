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
        if not (res.ToolTip.StartsWith("[")) then res.ListItemText else
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
    // ||| ReadKeyOptions.AllowCtrlC
    ||| ReadKeyOptions.IncludeKeyDown



open System.Text
open System
open System.Runtime.InteropServices

type String() =
    
    static member longestCommonPrefix s1 s2 =
        let rec loop (acc:StringBuilder) s1 s2 =
            match s1, s2 with
            | "", _ -> acc
            | _, "" -> acc
            | _ ->
                if Char.ToLowerInvariant s1.[0] = Char.ToLowerInvariant s2.[0] then
                    loop (acc.Append s1.[0]) (s1.Substring(1)) (s2.Substring(1))
                else
                    acc
        (loop (StringBuilder()) s1 s2).ToString()
        
    static member trimForHud(str:string, [<Optional; DefaultParameterValue(20)>] maxLength: int) =
        let sb = StringBuilder()
        for i = 0 to (min (str.Length - 1) (maxLength - 1)) do 
            match str[i] with 
            | '\n' -> ()
            | '\r' -> ()
            | c -> sb.Append(c)  |> ignore
        String.Format($"{{0,{maxLength}}}",sb.ToString())

    static member trimForBuffer(str:string, [<Optional; DefaultParameterValue(20)>] maxLength: int) =
        if String.IsNullOrEmpty(str) then StringBuilder(String.replicate maxLength " ") else
        let sb = StringBuilder()
        for i = 0 to (min (str.Length - 1) (maxLength - 1)) do 
            match str[i] with 
            | '\n' -> ()
            | '\r' -> ()
            | c -> sb.Append(c)  |> ignore
        
        let mutable pos = sb.Length - 1
        let mutable count = 0
        if Char.IsWhiteSpace(sb[pos]) then
            while pos > 0 && Char.IsWhiteSpace(sb[pos - 1]) do
                count <- count + 1
                pos <- pos - 1
            sb.Remove(pos,count) |> ignore
        while sb.Length < maxLength do
            sb.Append ' ' |> ignore
        sb


module File =

    let getHumanReadableFileSize (i:int64) : string =
        let mutable suffix = String.Empty
        let mutable readable = 0L
        if i < 0x400L then $"%i{i} B" else
        match i with 
        | _ when i >= 0x1000000000000000L -> suffix <- "EB"; readable <- i >>> 50
        | _ when i >= 0x4000000000000L -> suffix <- "PB"; readable <- i >>> 40
        | _ when i >= 0x10000000000L -> suffix <- "TB"; readable <- i >>> 30
        | _ when i >= 0x40000000L -> suffix <- "GB"; readable <- i >>> 20
        | _ when i >= 0x100000L -> suffix <- "MB"; readable <- i >>> 10
        | _ when i >= 0x400L -> suffix <- "KB"; readable <- i 
        | _ -> suffix <- "B"; readable <- 0 
        readable <- (readable / 1024L)
        readable.ToString("0.### ") + suffix
        

module Seq =
    let longestCommonPrefix (seq: seq<string>) =
        seq |> Seq.reduce (fun acc v -> 
            String.longestCommonPrefix acc v
        )
        


type PsCompleteSettings(cmdlet:PSCmdlet) =
    let _settings : System.Management.Automation.PSObject = 
        cmdlet.GetVariableValue("PsCompleteSettings") |> unbox
    
    member this.IsTopRightHudEnabled = 
        lazy (unbox<bool>(_settings.Properties["TopRightHUDEnabled"].Value))

    member this.PromptText = 
        lazy (unbox<string>(_settings.Properties["PromptText"].Value))


open System.Management.Automation
open System.Reflection

module Runspace =
    let private _runspace =
        Runspaces.Runspace.DefaultRunspace

    let private _runspace_doPath =
        lazy
            _runspace
                .GetType()
                .GetProperty("DoPath", BindingFlags.NonPublic ||| BindingFlags.Instance)

    let getCurrentPath() =
        let asd = _runspace_doPath.Value.GetValue(_runspace) :?> PathIntrinsics
        asd.CurrentLocation.Path
        

[<AutoOpen>]
module Globals =
    let inline cwd() = Runspace.getCurrentPath()

open System.Diagnostics
type Process with
    static member runAsync(pname:string,?pargs:string,?cwd:string) =
        task {
            use p = new Process(
                StartInfo=ProcessStartInfo(fileName=pname,arguments=defaultArg pargs "",WorkingDirectory=defaultArg cwd "", RedirectStandardOutput=true))
            p.Start() |> ignore
            do! p.WaitForExitAsync()
            return! p.StandardOutput.ReadToEndAsync()
        }
        
let logDebug(item) = 
    IO.File.WriteAllText("/tmp/log.json",JsonSerializer.Serialize(item,JsonSerializerOptions(WriteIndented = true)) ) 

type ConsoleColor with 
    static member Default = 
        match System.OperatingSystem.IsWindows() with
        | true -> 0 |> enum<ConsoleColor>
        | false -> -1 |> enum<ConsoleColor>



[<AutoOpen>]
module Patterns =
    // EndsWith ":" 
    let (|EndsWith|_|) (p:string) (s:string) =  
        if s.EndsWith(p, StringComparison.Ordinal)  
        then Some()  
        else None
        
    let (|StartsWith|_|) (p:string) (s:string) =  
        if s.StartsWith(p, StringComparison.Ordinal)  
        then Some()  
        else None

    let (|UnfinishedDotnetCommand|_|) (s:string) =  
        if Regex.IsMatch(s,@"^\[[^\]]*$")
        then Some()  
        else None
        
module Ast =
    open System.Management.Automation
    open System.Management.Automation.Language
    let tryGetPipelineCommand(cmdlet:PSCmdlet, bufferString:string) =
        let mutable tokens : Token[] = [||]
        let mutable errors : ParseError[] = [||]
        let scriptBlockAst = System.Management.Automation.Language.Parser.ParseInput(bufferString, &tokens, &errors)
        let lastStatement = scriptBlockAst.EndBlock.Statements |> Seq.last
        match lastStatement with
        | :? PipelineAst as pipelineast ->
            let lastPipelineElement = pipelineast.PipelineElements |> Seq.last
            match lastPipelineElement with
            | :? CommandAst as commandast ->
                let commandName = commandast.GetCommandName()
                let commandInfo = cmdlet.InvokeCommand.GetCommand(commandName, CommandTypes.All)
                Some(commandInfo, commandast)
            | _ -> None
        | _ -> None
    