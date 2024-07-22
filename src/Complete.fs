module pscomplete.Complete
open Render
open System.Management.Automation
open System
open Helpers
open System.Management.Automation.Runspaces
open System.Management.Automation.Host

type ExitContext = {
    completion:CommandCompletion
    cmdlet:PSCmdlet
    host:Host
    displayState:DisplayState
    exitKey:ExitKey
}

let replaceRaw (ctx:ExitContext, completion:CompletionResult) =

    let start' = ctx.completion.ReplacementIndex
    let end' = ctx.completion.ReplacementLength
    let inline replace(s,e,t) = Microsoft.PowerShell.PSConsoleReadLine.Replace(s,e,t)
    replace( start', end', completion.CompletionText )

let replaceDefault (ctx:ExitContext, completion:CompletionResult) =

    let start' = ctx.completion.ReplacementIndex
    let end' = ctx.completion.ReplacementLength
    let inline replace(s,e,t) = Microsoft.PowerShell.PSConsoleReadLine.Replace(s,e,t)
    match completion.ResultType with
    | CompletionResultType.ProviderContainer -> replace( start', end', completion.CompletionText + "/" )
    | _ -> replace( start', end', completion.CompletionText + " " )

let handleExit (ctx:ExitContext) =
    Microsoft.PowerShell.PSConsoleReadLine.ClearScreen()
    // Console.Clear()
    // Console.Out.Write("\x1b[m")

    match ctx.exitKey with
    | ExitKey.Escape -> ()
    | _ ->

    if ctx.displayState.SelectedIndex >= ctx.displayState.FilteredCache.Count then () else


    let completion: CompletionResult =
        ctx.displayState.FilteredCache[ctx.displayState.SelectedIndex]

    let completionText = completion.CompletionText
    // let firstWord = ctx.displayState.FirstWordOfBufferString
    // logDebug({|
    //     lastWord = ctx.displayState.GetLastWordOfCommand()
    //     replacementIndex = ctx.completion.ReplacementIndex
    //     replacementLength = ctx.completion.ReplacementLength
    //     resultType = completion.ResultType
    //     completionText = completionText
    //     bufferString = ctx.displayState.BufferString
    // |})

    match ctx.displayState.GetLastWordOfCommand() with
    // $env:___
    | (Matches @"^\$\w+:$") as v -> replaceDefault(ctx,completion)
    | (EndsWith ":") as v when ctx.displayState.FirstWordOfBufferString.Value = "scp" ->
        Microsoft.PowerShell.PSConsoleReadLine.Replace(
            ctx.completion.ReplacementIndex,
            ctx.completion.ReplacementLength,
            v + completionText
        )
    | (Contains ":") as v when
        ctx.displayState.FirstWordOfBufferString.Value = "scp"
        && ctx.displayState.BufferString.Contains('/') ->
        let correctindex = ctx.displayState.BufferString.IndexOf('/')
        Microsoft.PowerShell.PSConsoleReadLine.Replace(
            correctindex,
            ctx.completion.ReplacementLength - (correctindex - ctx.completion.ReplacementIndex),
            completionText
        )
    | (UnfinishedDotnetCommand) as v ->
        match ctx.exitKey with
        | ExitKey.Enter -> replaceRaw(ctx,completion)
        | _ ->
        let start' = ctx.completion.ReplacementIndex
        let end' = ctx.completion.ReplacementLength
        let inline replace(s,e,t) = Microsoft.PowerShell.PSConsoleReadLine.Replace(s,e,t)
        replace( start', end', completion.CompletionText + "]::" )

    | (EndsWith ":") as v when completionText.EndsWith("(") ->
        let updated =
            CompletionResult(
                completion.CompletionText + ")",
                completion.ListItemText,
                completion.ResultType,
                completion.ToolTip)
        replaceRaw(ctx,updated)
        Microsoft.PowerShell.PSConsoleReadLine.SetCursorPosition(
            ctx.completion.ReplacementIndex + completionText.Length
        )
    | _ -> replaceDefault(ctx,completion)




