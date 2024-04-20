module pscomplete.Library

open System.Management.Automation
open Helpers
open System
open System.Linq
open pscomplete.Render
open Complete

[<Cmdlet(VerbsLifecycle.Invoke, "PsComplete")>]
type PsCompleteCmdlet() =
    inherit PSCmdlet()

    [<Parameter(Position = 0, ValueFromPipelineByPropertyName = true)>]
    member val CommandCompletion : CommandCompletion = null with get, set
    [<Parameter>]
    member val BufferString = String.Empty with get, set
    [<Parameter>]
    member val BufferCursorPosition = 0 with get, set
    member val CachedSettings = Unchecked.defaultof<_> with get, set
    member val CachedHost : Host = Unchecked.defaultof<Host> with get, set
    member val CachedRenderer : UI.AnsiRenderer = Unchecked.defaultof<UI.AnsiRenderer> with get, set

    override this.BeginProcessing() =
        Microsoft.PowerShell.PSConsoleReadLine.ClearScreen()
        match this.BufferString with
        | "./" when OperatingSystem.IsLinux() ->
            this.CommandCompletion.CompletionMatches 
                <- Collections.ObjectModel.Collection(Directory.getExecutables(pwd()))
        | _ -> ()

        // logDebug({|str=this.BufferString|})
        this.CachedSettings <- PsCompleteSettings(this)
        this.CachedHost <- Host(this, this.BufferString, this.CachedSettings)
        // this.CachedRenderer <- UI.Renderer(this.CachedHost)
        this.CachedRenderer <- UI.AnsiRenderer(this.CachedHost)

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
                    PrevHudArray = [||]                    
                    Sout = null                    
                    Membuf = null
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
            let pagelen = this.CachedHost.FrameHeight - 2
            let initState =
                {
                    BufferString = this.BufferString
                    RawFilterText = ""
                    SelectedIndex = 0
                    Content = this.CommandCompletion.CompletionMatches.ToArray()
                    FilteredCache = ResizeArray(this.CommandCompletion.CompletionMatches)
                    PageLength = this.CachedHost.FrameHeight - 2
                    PrevHudArray = [||]                    
                    Sout = System.Console.OpenStandardOutput()
                    Membuf = new System.IO.MemoryStream(pagelen * 10)
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
