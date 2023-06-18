module pscomplete.Library

open System.IO
open System.Management.Automation
open System.Management.Automation.Language
open System.Runtime.CompilerServices
open System.Text.Json
open Helpers
open System.Management.Automation.Host
open System
open System.Text.RegularExpressions
open System.Linq
open pscomplete.Helpers
open System.Collections.Generic
open pscomplete.Render
open Complete


[<Cmdlet(VerbsLifecycle.Invoke, "PsComplete")>]
type NewPsCompleteCmdlet() =
    inherit PSCmdlet()

    [<Parameter(Position = 0, ValueFromPipelineByPropertyName = true)>]
    member val CommandCompletion : CommandCompletion = null with get, set
    [<Parameter>]
    member val BufferString = String.Empty with get, set
    [<Parameter>]
    member val BufferCursorPosition = 0 with get, set
    member val CachedSettings = Unchecked.defaultof<_> with get, set
    member val CachedHost : UI.Host = Unchecked.defaultof<UI.Host> with get, set
    member val CachedRenderer : UI.Renderer = Unchecked.defaultof<UI.Renderer> with get, set
    
    override this.BeginProcessing() =
        Microsoft.PowerShell.PSConsoleReadLine.ClearScreen()
        match this.BufferString with
        | "./" when OperatingSystem.IsLinux() ->
            let executables =
                this.CommandCompletion.CompletionMatches
                |> Seq.where (fun x ->
                    let fileinfo = FileInfo(x.CompletionText)
                    fileinfo.Exists && fileinfo.UnixFileMode.HasFlag(UnixFileMode.UserExecute)
                )
                |> Seq.toArray
            this.CommandCompletion.CompletionMatches <- Collections.ObjectModel.Collection(executables)
        | _ -> ()
            
        // logDebug({|str=this.BufferString|})
        this.CachedSettings <- PsCompleteSettings(this)
        this.CachedHost <- UI.Host(this, this.BufferString, this.CachedSettings)
        this.CachedRenderer <- UI.Renderer(this.CachedHost)

    member this.UpdateState(state: DisplayState) =
        let state' = state |> DisplayState.filterInPlace
        state'
        
    member this.UpdateStateAdded(state: DisplayState) =
        let state' = state |> DisplayState.filterCacheInPlace
        state'

    member this.DoReplacement(newValue:string) = 
        Microsoft.PowerShell.PSConsoleReadLine.Replace(
            start=this.CommandCompletion.ReplacementIndex,
            length=this.CommandCompletion.ReplacementLength,
            replacement=newValue
        )

    member this.TryExitEarly() =
        if this.CachedHost.FrameHeight < 3 || this.CachedHost.FrameWidth < 1 then true
        elif this.CommandCompletion.CompletionMatches.Count = 0 then true
        elif this.CommandCompletion.CompletionMatches.Count = 1 then 
            let initState =
                {
                    BufferString = this.BufferString
                    RawFilterText = ""
                    SelectedIndex = 0
                    Content = this.CommandCompletion.CompletionMatches.ToArray()
                    FilteredCache = ResizeArray(this.CommandCompletion.CompletionMatches)
                    PageLength = this.CachedHost.FrameHeight - 2
                }
            let exitContext : ExitContext = 
                { 
                    completion = this.CommandCompletion
                    cmdlet = this
                    host = this.CachedHost
                    displayState = initState
                    exitKey = ExitKey.None
                } 
            handleExit(exitContext)
            true
        else false
        
    

    override this.ProcessRecord() =
        try
            if this.TryExitEarly() then () else
        
            let ui = this.Host.UI.RawUI
            
            let initState =
                {
                    BufferString = this.BufferString
                    RawFilterText = ""
                    SelectedIndex = 0
                    Content = this.CommandCompletion.CompletionMatches.ToArray()
                    FilteredCache = ResizeArray(this.CommandCompletion.CompletionMatches)
                    PageLength = this.CachedHost.FrameHeight - 2
                }
            
            let loopArgs =
                {
                    InitState = initState
                    Ui = ui
                    ExitCommand = (fun state key -> 
                        let exitContext : ExitContext = 
                            { 
                                completion = this.CommandCompletion
                                cmdlet = this
                                host = this.CachedHost
                                displayState = state
                                exitKey = key
                            } 
                        handleExit(exitContext)
                    )
                }
            
            Render.startLoop loopArgs (fun (state,ctx) ->
                match ctx with
                | InputAdded ->
                    let upd = this.UpdateStateAdded(state)
                    this.CachedRenderer.RenderCachedState(upd)
                | Input ->
                    let upd = this.UpdateState(state)
                    this.CachedRenderer.RenderCachedState(upd)
                | Arrow ->
                    this.CachedRenderer.RenderCachedState(state)
            )


        with
        | e -> this.WriteWarning(e.Message + "\n" + e.StackTrace)

    override this.EndProcessing() = ()
