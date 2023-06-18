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
    host:UI.Host
    displayState:DisplayState
    exitKey:ExitKey
}


let handleExit (ctx:ExitContext) =
    Microsoft.PowerShell.PSConsoleReadLine.ClearScreen()

    match ctx.exitKey with 
    | ExitKey.Escape -> ()
    | _ -> 
    
    if ctx.displayState.SelectedIndex >= ctx.displayState.FilteredCache.Count then () else
    
    
    let completionText = 
        ctx.displayState.FilteredCache[ctx.displayState.SelectedIndex].CompletionText 
    // logDebug({|
    //     lastWord = ctx.displayState.GetLastWordOfCommand()
    //     replacementIndex = ctx.completion.ReplacementIndex
    //     replacementLength = ctx.completion.ReplacementLength
    //     completionText = completionText
    //     bufferString = ctx.displayState.BufferString
    // |})

    match ctx.displayState.GetLastWordOfCommand() with 
    | (EndsWith ":") as v -> 
        Microsoft.PowerShell.PSConsoleReadLine.Replace(
            ctx.completion.ReplacementIndex, 
            ctx.completion.ReplacementLength, 
            v + completionText 
        )
    | _ -> 

    // default
    Microsoft.PowerShell.PSConsoleReadLine.Replace(
        ctx.completion.ReplacementIndex, 
        ctx.completion.ReplacementLength, 
        completionText + " "
    )
    
    
    