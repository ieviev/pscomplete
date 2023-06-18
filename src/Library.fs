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

    member this.CommandParameterSanitized =
        lazy 
            match this.CommandParameter.IndexOf("\n") with
            | -1 -> this.CommandParameter
            | n -> ""

    member this.Settings = PsCompleteSettings(this)
        
    override this.BeginProcessing() =

        let ui = this.Host.UI.RawUI
        this.FrameTopLeft <- Coordinates(0, ui.CursorPosition.Y + 1 - ui.WindowPosition.Y)
        let numOfNewlines = 
            this.CommandParameter |> String.filter ((=) '\n') |> String.length
        this.FrameH <- ui.WindowSize.Height - ui.CursorPosition.Y - 1 - numOfNewlines
        this.FrameW <- ui.WindowSize.Width

        if this.ShouldExitEarly() then
            ()
        else
            this.Buffer <- ui.NewBufferCellArray(Size(this.FrameW, this.FrameH), bufferCell ' ')
            this.EmptyBuffer <- this.Buffer |> Array2D.copy

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member x.WriteBufferLine (y: int) (buffer: BufferCell [,]) (current: string) =
        let xmax = buffer.GetLength(1) - 1
        let current = current + "                    " // to clear previous text cells
        for i = 0 to min (current.Length - 1) xmax do
            buffer[y, i].Character <- current[i]

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member x.WriteBufferBlock (buffer: BufferCell [,],current: string) =
        let xmax = buffer.GetLength(1) - 1
        for i = 0 to xmax do
            let char = if i < current.Length then current[i] else ' '
            buffer[0, i].Character <- char

    member this.DrawEmptyFilter (state: DisplayState) (buffer: BufferCell [,]) =
        let top =
            Graphics.boxTop 5 $"%s{this.CommandParameterSanitized.Value}[{state.RawFilterText}]"
        this.WriteBufferLine 0 buffer top

    member this.UpdateState(state: DisplayState) =
        let state' = state |> DisplayState.filterInPlace
        state'
        
    member this.UpdateStateAdded(state: DisplayState) =
        let state' = state |> DisplayState.filterCacheInPlace
        state'

    member this.GetCompletionHudInfoAsync(result:CompletionResult, maxLength : int) =
        task {
            match result.ResultType with
            | CompletionResultType.ProviderItem -> 
                let fileInfo = IO.FileInfo( IO.Path.Combine(cwd(),result.CompletionText))
                match fileInfo.Exists with 
                | true -> 
                    let! mimeOutput = System.Diagnostics.Process.runAsync("file",$"--mime-type \"{fileInfo.FullName}\"")
                    let! fileOutput = System.Diagnostics.Process.runAsync("file",$"\"{fileInfo.FullName}\"")
                    return [|
                        "File"
                        String.trimForHud(mimeOutput.Substring(fileInfo.FullName.Length + 2), maxLength)
                        File.getHumanReadableFileSize(fileInfo.Length)
                        String.trimForHud (fileOutput.Substring(fileInfo.FullName.Length + 2), maxLength)
                    |]
                | _ -> return [||]
            | CompletionResultType.ProviderContainer -> 
                let dirInfo = IO.DirectoryInfo( IO.Path.Combine(cwd(),result.CompletionText))
                match dirInfo.Exists with 
                | true -> 
                    let numOfFiles = dirInfo.EnumerateFiles() |> Seq.length
                    return [|
                        $"Directory"
                        $"%i{numOfFiles} files"
                    |]
                | _ -> 
                    return [|
                        "Folder"
                    |]
            | _ -> return [|$"%s{result.ResultType.ToString()}"|]
        }

        

        
    member this.DisplayHudInfo(hudWidth:int, lines:string[]) =
        this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Black
        this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.Green
        let hudBuffer =
            this.Host.UI.RawUI.NewBufferCellArray(
                Size(Width = hudWidth , Height = 1),
                bufferCell ' '
            )
        for i = 0 to lines.Length - 1 do
            let line = lines[i]
            this.WriteBufferBlock(hudBuffer,line)
            this.Host.UI.RawUI.SetBufferContents(
                Coordinates(this.FrameW - hudWidth, this.FrameTopLeft.Y + i),
                hudBuffer
            )
        this.Host.UI.RawUI.SetBufferContents(
            Coordinates(this.FrameW - hudWidth, this.FrameTopLeft.Y + lines.Length + 1),
            Array2D.create 1 1 (bufferCell ' ')
        )
        // this.Host.UI.RawUI.FlushInputBuffer()
        this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.White
        this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Black
        
        
    member this.RenderCachedState(state: DisplayState) =
        this.ClearScreenQuick()
        if state.FilteredCache.Count = 0 then
            this.DrawEmptyFilter state this.Buffer
            this.Host.UI.RawUI.SetBufferContents(this.FrameTopLeft, this.Buffer)
        else
            let pageLength = this.FrameH - 2 // frames
            let pageIndex = state.SelectedIndex / pageLength
            let pageSelIndex = state.SelectedIndex % pageLength

            let completions =
                state.FilteredCache
                |> Seq.skip (pageIndex * pageLength)
                |> Seq.truncate pageLength
                |> Seq.map PsCompletion.toText
                |> Seq.toArray

            let longest: string =
                completions 
                |> Array.maxBy (fun f -> f.Length)

            let currSelectedText = completions[pageSelIndex]

            let topLine =
                Graphics.boxTop longest.Length $"%s{this.CommandParameterSanitized.Value}[{state.RawFilterText}]"

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
                this.WriteBufferLine (i) this.Buffer content[i] 
    
            this.Host.UI.RawUI.SetBufferContents(this.FrameTopLeft, this.Buffer)

            // color selected line

            match Platform with
            | Unix ->
                this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Green
                this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.Black
                let newarr =
                    this.Host.UI.RawUI.NewBufferCellArray(
                        Size(Width = currSelectedText.Length, Height = 1),
                        bufferCell ' '
                    )
                let txtcontent = $"{currSelectedText}"
                this.WriteBufferLine 0 newarr txtcontent
                this.Host.UI.RawUI.SetBufferContents(
                    Coordinates(1, this.FrameTopLeft.Y + 1 + pageSelIndex),
                    newarr
                )
                // display top right hud info, experimental
                if 
                    state.FilteredCache.Count > state.SelectedIndex 
                    && OperatingSystem.IsLinux() 
                    && this.Settings.IsTopRightHudEnabled.Value
                
                then
                    let hudWidth = 25
                    let currentCommand = state.FilteredCache[state.SelectedIndex]
                    let hudInfoArray = this.GetCompletionHudInfoAsync(currentCommand, hudWidth).GetAwaiter().GetResult()
                    this.DisplayHudInfo(hudWidth=hudWidth, lines=hudInfoArray)

                this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.White
                this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Black
            | Win ->
                let selectedLine = this.FrameTopLeft.Y + 1 + pageSelIndex

                let linebuffer =
                    this.Host.UI.RawUI.GetBufferContents(
                        Rectangle(
                            left = 0,
                            top = selectedLine,
                            right = currSelectedText.Length,
                            bottom = selectedLine
                        )
                    )

                for x = 1 to currSelectedText.Length do
                    linebuffer[0, x].BackgroundColor <- ConsoleColor.Blue

                this.Host.UI.RawUI.SetBufferContents(Coordinates(0, selectedLine), linebuffer)

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

    member this.CleanCompletionText(state:DisplayState ,completion:CompletionResult) =
        let lastWord = state.GetLastWordOfCommand()
        if lastWord.StartsWith('$') && lastWord.Contains(':') then
            completion.CompletionText.Substring(completion.CompletionText.IndexOf(':') + 1)
        else completion.CompletionText
        
    member this.GetCompletionAndExit (state: DisplayState) (exitKey: ExitKey) =
        this.ClearScreenFull this.Buffer
        
        let filtered = state |> DisplayState.filterInPlace
        if filtered.FilteredCache.Count = 0 then
            ()
        else
            let completion = filtered.FilteredCache[state.SelectedIndex]
            // {| cmdstr = state.CommandString; rawfilter = state.RawFilterText; completion = completion.CompletionText |}
            // |> System.Text.Json.JsonSerializer.Serialize
            // |> (fun v -> System.IO.File.WriteAllText("/mnt/ramdisk/temp.json",v))
            {
                CompletionText = this.CleanCompletionText(state,completion)
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


[<OutputType(typeof<CompleteOutput>)>]
[<Cmdlet(VerbsLifecycle.Invoke, "NewPsComplete")>]
type NewPsCompleteCmdlet() =
    inherit PSCmdlet()

    [<Parameter(Position = 0, ValueFromPipelineByPropertyName = true)>]
    member val CommandCompletion = ResizeArray<System.Management.Automation.CommandCompletion>() with get, set

    [<Parameter>]
    member val CommandParameter = "" with get, set

    //
    member val FrameH = 0 with get, set
    member val FrameW = 0 with get, set
    member val FrameTopLeft = Unchecked.defaultof<Coordinates> with get, set
    member val Buffer = Unchecked.defaultof<BufferCell [,]> with get, set
    member val EmptyBuffer = Unchecked.defaultof<BufferCell [,]> with get, set

    member this.CommandParameterSanitized =
        lazy 
            match this.CommandParameter.IndexOf("\n") with
            | -1 -> this.CommandParameter
            | n -> ""

    member this.Settings = PsCompleteSettings(this)
        
    override this.BeginProcessing() =

        let ui = this.Host.UI.RawUI
        this.FrameTopLeft <- Coordinates(0, ui.CursorPosition.Y + 1 - ui.WindowPosition.Y)
        let numOfNewlines = 
            this.CommandParameter |> String.filter ((=) '\n') |> String.length
        this.FrameH <- ui.WindowSize.Height - ui.CursorPosition.Y - 1 - numOfNewlines
        this.FrameW <- ui.WindowSize.Width

        if this.ShouldExitEarly() then
            ()
        else
            this.Buffer <- ui.NewBufferCellArray(Size(this.FrameW, this.FrameH), bufferCell ' ')
            this.EmptyBuffer <- this.Buffer |> Array2D.copy

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member x.WriteBufferLine (y: int) (buffer: BufferCell [,]) (current: string) =
        let xmax = buffer.GetLength(1) - 1
        let current = current + "                    " // to clear previous text cells
        for i = 0 to min (current.Length - 1) xmax do
            buffer[y, i].Character <- current[i]

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member x.WriteBufferBlock (buffer: BufferCell [,],current: string) =
        let xmax = buffer.GetLength(1) - 1
        for i = 0 to xmax do
            let char = if i < current.Length then current[i] else ' '
            buffer[0, i].Character <- char

    member this.DrawEmptyFilter (state: DisplayState) (buffer: BufferCell [,]) =
        let top =
            Graphics.boxTop 5 $"%s{this.CommandParameterSanitized.Value}[{state.RawFilterText}]"
        this.WriteBufferLine 0 buffer top

    member this.UpdateState(state: DisplayState) =
        let state' = state |> DisplayState.filterInPlace
        state'
        
    member this.UpdateStateAdded(state: DisplayState) =
        let state' = state |> DisplayState.filterCacheInPlace
        state'

    member this.GetCompletionHudInfoAsync(result:CompletionResult, maxLength : int) =
        task {
            match result.ResultType with
            | CompletionResultType.ProviderItem -> 
                let fileInfo = IO.FileInfo( IO.Path.Combine(cwd(),result.CompletionText))
                match fileInfo.Exists with 
                | true -> 
                    let! mimeOutput = System.Diagnostics.Process.runAsync("file",$"--mime-type \"{fileInfo.FullName}\"")
                    let! fileOutput = System.Diagnostics.Process.runAsync("file",$"\"{fileInfo.FullName}\"")
                    return [|
                        "File"
                        String.trimForHud(mimeOutput.Substring(fileInfo.FullName.Length + 2), maxLength)
                        File.getHumanReadableFileSize(fileInfo.Length)
                        String.trimForHud (fileOutput.Substring(fileInfo.FullName.Length + 2), maxLength)
                    |]
                | _ -> return [||]
            | CompletionResultType.ProviderContainer -> 
                let dirInfo = IO.DirectoryInfo( IO.Path.Combine(cwd(),result.CompletionText))
                match dirInfo.Exists with 
                | true -> 
                    let numOfFiles = dirInfo.EnumerateFiles() |> Seq.length
                    return [|
                        $"Directory"
                        $"%i{numOfFiles} files"
                    |]
                | _ -> 
                    return [|
                        "Folder"
                    |]
            | _ -> return [|$"%s{result.ResultType.ToString()}"|]
        }

        

        
    member this.DisplayHudInfo(hudWidth:int, lines:string[]) =
        this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Black
        this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.Green
        let hudBuffer =
            this.Host.UI.RawUI.NewBufferCellArray(
                Size(Width = hudWidth , Height = 1),
                bufferCell ' '
            )
        for i = 0 to lines.Length - 1 do
            let line = lines[i]
            this.WriteBufferBlock(hudBuffer,line)
            this.Host.UI.RawUI.SetBufferContents(
                Coordinates(this.FrameW - hudWidth, this.FrameTopLeft.Y + i),
                hudBuffer
            )
        this.Host.UI.RawUI.SetBufferContents(
            Coordinates(this.FrameW - hudWidth, this.FrameTopLeft.Y + lines.Length + 1),
            Array2D.create 1 1 (bufferCell ' ')
        )
        // this.Host.UI.RawUI.FlushInputBuffer()
        this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.White
        this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Black
        
        
    member this.RenderCachedState(state: DisplayState) =
        this.ClearScreenQuick()
        if state.FilteredCache.Count = 0 then
            this.DrawEmptyFilter state this.Buffer
            this.Host.UI.RawUI.SetBufferContents(this.FrameTopLeft, this.Buffer)
        else
            let pageLength = this.FrameH - 2 // frames
            let pageIndex = state.SelectedIndex / pageLength
            let pageSelIndex = state.SelectedIndex % pageLength

            let completions =
                state.FilteredCache
                |> Seq.skip (pageIndex * pageLength)
                |> Seq.truncate pageLength
                |> Seq.map PsCompletion.toText
                |> Seq.toArray

            let longest: string =
                completions 
                |> Array.maxBy (fun f -> f.Length)

            let currSelectedText = completions[pageSelIndex]

            let topLine =
                Graphics.boxTop longest.Length $"%s{this.CommandParameterSanitized.Value}[{state.RawFilterText}]"

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
                this.WriteBufferLine (i) this.Buffer content[i] 
    
            this.Host.UI.RawUI.SetBufferContents(this.FrameTopLeft, this.Buffer)

            // color selected line

            match Platform with
            | Unix ->
                this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Green
                this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.Black
                let newarr =
                    this.Host.UI.RawUI.NewBufferCellArray(
                        Size(Width = currSelectedText.Length, Height = 1),
                        bufferCell ' '
                    )
                let txtcontent = $"{currSelectedText}"
                this.WriteBufferLine 0 newarr txtcontent
                this.Host.UI.RawUI.SetBufferContents(
                    Coordinates(1, this.FrameTopLeft.Y + 1 + pageSelIndex),
                    newarr
                )
                // display top right hud info, experimental
                if 
                    state.FilteredCache.Count > state.SelectedIndex 
                    && OperatingSystem.IsLinux() 
                    && this.Settings.IsTopRightHudEnabled.Value
                
                then
                    let hudWidth = 25
                    let currentCommand = state.FilteredCache[state.SelectedIndex]
                    let hudInfoArray = this.GetCompletionHudInfoAsync(currentCommand, hudWidth).GetAwaiter().GetResult()
                    this.DisplayHudInfo(hudWidth=hudWidth, lines=hudInfoArray)

                this.Host.UI.RawUI.ForegroundColor <- ConsoleColor.White
                this.Host.UI.RawUI.BackgroundColor <- ConsoleColor.Black
            | Win ->
                let selectedLine = this.FrameTopLeft.Y + 1 + pageSelIndex

                let linebuffer =
                    this.Host.UI.RawUI.GetBufferContents(
                        Rectangle(
                            left = 0,
                            top = selectedLine,
                            right = currSelectedText.Length,
                            bottom = selectedLine
                        )
                    )

                for x = 1 to currSelectedText.Length do
                    linebuffer[0, x].BackgroundColor <- ConsoleColor.Blue

                this.Host.UI.RawUI.SetBufferContents(Coordinates(0, selectedLine), linebuffer)

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

    member this.CleanCompletionText(state:DisplayState ,completion:CompletionResult) =
        let lastWord = state.GetLastWordOfCommand()
        if lastWord.StartsWith('$') && lastWord.Contains(':') then
            completion.CompletionText.Substring(completion.CompletionText.IndexOf(':') + 1)
        else completion.CompletionText
        
    member this.GetCompletionAndExit (state: DisplayState) (exitKey: ExitKey) =
        this.ClearScreenFull this.Buffer
        
        let filtered = state |> DisplayState.filterInPlace
        if filtered.FilteredCache.Count = 0 then
            ()
        else
            let completion = filtered.FilteredCache[state.SelectedIndex]
            // {| cmdstr = state.CommandString; rawfilter = state.RawFilterText; completion = completion.CompletionText |}
            // |> System.Text.Json.JsonSerializer.Serialize
            // |> (fun v -> System.IO.File.WriteAllText("/mnt/ramdisk/temp.json",v))
            {
                CompletionText = this.CleanCompletionText(state,completion)
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
