module pscomplete.Library

open System.Management.Automation
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


[<CLIMutable>]
type CompleteOutput =
    {
        ArgumentType: string
        CompletionText: string
        ResultType: CompletionResultType
        ExitKey: ExitKey
    }

[<OutputType(typeof<CompleteOutput>)>]
[<Cmdlet(VerbsLifecycle.Invoke, "PsComplete")>]
type ConfCmdlet() =
    inherit PSCmdlet()

    [<Parameter(Position = 0, ValueFromPipelineByPropertyName = true)>]
    member val Content = ResizeArray<CompletionResult>() with get, set

    [<Parameter>]
    member val CommandParameter = "" with get, set

    //
    member val FrameH = 0 with get, set
    member val FrameW = 0 with get, set
    member val FrameTopLeft = Unchecked.defaultof<Coordinates> with get, set
    member val Buffer = Unchecked.defaultof<BufferCell [,]> with get, set
    member val EmptyBuffer = Unchecked.defaultof<BufferCell [,]> with get, set

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member x.WriteBufferLine (y: int) (buffer: BufferCell [,]) (current: string) =
        let xmax = buffer.GetLength(1) - 1
        let current = current + "                    " // to clear previous text cells
        for i = 0 to min (current.Length - 1) xmax do
            buffer[y, i].Character <- current[i]

    member x.DrawEmptyFilter (state: DisplayState) (buffer: BufferCell [,]) =
        let top =
            Graphics.boxTop 5 $"{x.CommandParameter}[{state.RawFilterText}]"
        x.WriteBufferLine 0 buffer top

    member x.UpdateState(state: DisplayState) =
        let state' = state |> DisplayState.filterInPlace
        state'
        
    member x.UpdateStateAdded(state: DisplayState) =
        let state' = state |> DisplayState.filterCacheInPlace
        state'
        
    member x.RenderCachedState(state: DisplayState) =
        x.ClearScreenQuick()
        if state.FilteredCache.Count = 0 then
            x.DrawEmptyFilter state x.Buffer
            x.Host.UI.RawUI.SetBufferContents(x.FrameTopLeft, x.Buffer)
        else
            let pageLength = x.FrameH - 2 // frames
            let pageIndex = state.SelectedIndex / pageLength
            let pageSelIndex = state.SelectedIndex % pageLength

            let completions =
                state.FilteredCache
                |> Seq.skip (pageIndex * pageLength)
                |> Seq.truncate pageLength
                |> Seq.map PsCompletion.toText
                |> Seq.toArray

            let longest: string =
                completions |> Array.maxBy (fun f -> f.Length)

            let currSelectedText = completions[pageSelIndex]

            let topLine =
                Graphics.boxTop longest.Length $"{x.CommandParameter}[{state.RawFilterText}]"

            let bottomLine =
                Graphics.boxBottom longest.Length $"{state.SelectedIndex + 1} of {state.FilteredCache.Count}"

            let content =
                [|
                    yield topLine
                    for n in completions do
                        yield Graphics.boxCenter longest.Length n
                    yield bottomLine
                |]

            for i = 0 to content.Length - 1 do
                x.WriteBufferLine (i) x.Buffer content[i]
            // content
            // |> Array.iteri (fun i -> x.WriteBufferLine (i) x.Buffer)

            x.Host.UI.RawUI.SetBufferContents(x.FrameTopLeft, x.Buffer)

            // color selected line

            match Platform with
            | Unix ->
                x.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Blue
                let newarr =
                    x.Host.UI.RawUI.NewBufferCellArray(
                        Size(Width = currSelectedText.Length, Height = 1),
                        bufferCell ' '
                    )
                let txtcontent = $"{currSelectedText}"
                x.WriteBufferLine 0 newarr txtcontent
                x.Host.UI.RawUI.SetBufferContents(
                    Coordinates(1, x.FrameTopLeft.Y + 1 + pageSelIndex),
                    newarr
                )
                x.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Black
            | Win ->
                let selectedLine = x.FrameTopLeft.Y + 1 + pageSelIndex

                let linebuffer =
                    x.Host.UI.RawUI.GetBufferContents(
                        Rectangle(
                            left = 0,
                            top = selectedLine,
                            right = currSelectedText.Length,
                            bottom = selectedLine
                        )
                    )

                for x = 1 to currSelectedText.Length do
                    linebuffer[0, x].BackgroundColor <- ConsoleColor.Blue

                x.Host.UI.RawUI.SetBufferContents(Coordinates(0, selectedLine), linebuffer)

    member x.ClearScreenQuick() =
        let defaultColor =
            match Platform with
            | Win -> 0 |> enum<ConsoleColor>
            | Unix -> -1 |> enum<ConsoleColor>
        x.Host.UI.RawUI.BackgroundColor <- defaultColor
        x.Buffer <- x.Host.UI.RawUI.NewBufferCellArray(Size(x.FrameW, x.FrameH), bufferCell ' ')
    
    member x.ClearScreenFull(buffer: BufferCell [,]) =
        let defaultColor =
            match Platform with
            | Win -> 0 |> enum<ConsoleColor>
            | Unix -> -1 |> enum<ConsoleColor>
        x.Host.UI.RawUI.BackgroundColor <- defaultColor

        buffer
        |> Array2D.iteri (fun x y _ -> buffer[x, y].Character <- ' ')
        x.Host.UI.RawUI.SetBufferContents(x.FrameTopLeft, buffer)
    override this.BeginProcessing() =

        let ui = this.Host.UI.RawUI
        this.FrameTopLeft <- Coordinates(0, ui.CursorPosition.Y + 1 - ui.WindowPosition.Y)
        this.FrameH <- ui.WindowSize.Height - ui.CursorPosition.Y - 1
        this.FrameW <- ui.WindowSize.Width

        if this.ShouldExitEarly() then
            ()
        else
            this.Buffer <- ui.NewBufferCellArray(Size(this.FrameW, this.FrameH), bufferCell ' ')
            this.EmptyBuffer <- this.Buffer |> Array2D.copy

    member this.ExitWithWarning(message: string) =
        this.WriteWarning("\n" + message)

    member this.ShouldExitEarly() =
        if this.FrameH < 3 || this.FrameW < 1 then
            this.ExitWithWarning(
                "Window too small to draw completion list, please clear the buffer"
            )
            true
        elif this.Content.Count = 0 then
            true
        elif this.Content.Count = 1 then
            true
        else
            false


    member this.GetCompletionAndExit (state: DisplayState) (exitKey: ExitKey) =
        this.ClearScreenFull this.Buffer
        let filtered = state |> DisplayState.filterInPlace

        if filtered.FilteredCache.Count = 0 then
            ()
        else
            let completion = filtered.FilteredCache[state.SelectedIndex]
            {
                CompletionText = completion.CompletionText
                ArgumentType =
                    completion
                    |> PsCompletion.toText
                    |> PsArgument.getText
                ResultType = completion.ResultType
                ExitKey = exitKey
            }
            |> this.WriteObject

    override this.ProcessRecord() =
        try
            if this.ShouldExitEarly() then
                if this.Content.Count = 1 then
                    this.WriteObject
                        {
                            CompletionText = this.Content[0].CompletionText
                            ArgumentType = "" // argument type was never queried
                            ResultType = this.Content[0].ResultType
                            ExitKey = ExitKey.None
                        }
            else
                let ui = this.Host.UI.RawUI
                
                let initState =
                    {
                        CommandString = this.CommandParameter
                        RawFilterText = ""
                        SelectedIndex = 0
                        Content = this.Content
                        FilteredCache = ResizeArray(this.Content)
                        PageLength = this.FrameH - 2
                    }
                
                let loopArgs =
                    {
                        InitState = initState
                        Ui = ui
                        ExitCommand = this.GetCompletionAndExit
                    }
                
                Render.startLoop loopArgs (fun (state,ctx) ->
                    match ctx with
                    | InputAdded ->
                        this.UpdateStateAdded(state)
                        |> this.RenderCachedState
                    | Input ->
                        this.UpdateState(state)
                        |> this.RenderCachedState
                    | Arrow ->
                        this.RenderCachedState(state)
                )


        with
        | e ->
            {
                CompletionText = e.Message + "\n" + e.StackTrace
                ArgumentType = ""
                ExitKey = ExitKey.None
                ResultType = CompletionResultType.Text
            }
            |> this.WriteObject

        ()

    override this.EndProcessing() = ()
