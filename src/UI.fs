module pscomplete.UI

open System.Management.Automation
open System.Management.Automation.Host
open System
open Helpers

type Host(cmdlet:PSCmdlet,bufferString:string, settings:PsCompleteSettings) = 
    let numOfNewlines = bufferString |> String.filter ((=) '\n') |> String.length
    let rawUI = cmdlet.Host.UI.RawUI
    let _initialWindowY =  rawUI.WindowPosition.Y
    let _initialCursorY =  rawUI.CursorPosition.Y
    let _initialWindowHeight =  cmdlet.Host.UI.RawUI.WindowSize.Height 
    let _frameHeight = 
        _initialWindowHeight
        - _initialCursorY
        - numOfNewlines 
        - 1 
    let _frameWidth = cmdlet.Host.UI.RawUI.WindowSize.Width
    let _frameTopLeft = Coordinates(0, _initialCursorY + 1 - _initialCursorY)
    let _blankbuffer = rawUI.NewBufferCellArray(Size(_frameWidth, _frameHeight), bufferCell ' ')

    member this.FrameHeight = _frameHeight
    member this.FrameWidth = _frameWidth
    member val FrameTopLeft = _frameTopLeft
    member val RawUI = rawUI
    member val BlankBuffer = _blankbuffer
    member val Settings = settings
    
    
module Buffer =
    let inline clearInPlace (host:Host, buffer: byref<BufferCell [,]>) =
        let defaultColor =
            match System.OperatingSystem.IsWindows() with
            | true -> 0 |> enum<ConsoleColor>
            | false -> -1 |> enum<ConsoleColor>
        host.RawUI.BackgroundColor <- defaultColor
        buffer <- unbox (host.BlankBuffer.Clone())

    

    let inline writeLine (buffer: byref<BufferCell [,]>, lineNo:int, line:string) =
        let xmax = buffer.GetLength(1) // - 1
        let trimmed = String.trimForBuffer(line, xmax)
        
        for i = 0 to trimmed.Length - 1 do
            buffer[lineNo, i].Character <- trimmed[i]


type Renderer(host:Host) =
    
    let host = host
    let mutable _cachedBuffer : BufferCell [,] = unbox (host.BlankBuffer.Clone())
    member this.DrawEmptyFilter(state:DisplayState,buffer: byref<BufferCell [,]>) = 
        let top =
            Graphics.boxTop 5 $"%s{state.SanitizedBufferString.Value}[%s{state.RawFilterText}]"
        Buffer.writeLine(&buffer,0,top)
    
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
                | _ -> return [| "File" |]
            | CompletionResultType.ProviderContainer -> 
                let dirInfo = IO.DirectoryInfo( IO.Path.Combine(cwd(),result.CompletionText))
                match dirInfo.Exists with 
                | true ->
                    let numOfFiles = dirInfo.EnumerateFiles() |> Seq.length
                    return [|
                        $"Directory"
                        $"%i{numOfFiles} files"
                    |]
                | _ -> return [| "Folder" |]
            | CompletionResultType.Variable -> return [| "Variable" |]
            | _ when result.ToolTip.StartsWith("$LIST") -> 
                let lines = result.ToolTip.Split("\n")
                // return (lines[1..] |> Array.map (fun v -> String.trimForHud(v, maxLength)))
                return (lines[1..])
            | _ -> return [|$"%s{result.ResultType.ToString()}"|]
        }

    member this.RenderSelectedLine(currSelectedText:string, pageSelectedIndex:int) =
        match OperatingSystem.IsWindows() with
        | false ->
            host.RawUI.BackgroundColor <- ConsoleColor.Green
            host.RawUI.ForegroundColor <- ConsoleColor.Black
            let mutable newarr =
                host.RawUI.NewBufferCellArray(
                    Size(Width = currSelectedText.Length, Height = 1),
                    bufferCell ' '
                )
            let txtcontent = currSelectedText //$"{currSelectedText}"
            
            Buffer.writeLine(&newarr,0,txtcontent)
            // let slice = newarr[0,*]
            // logDebug(slice)
            // this.WriteBufferLine 0 newarr txtcontent
            host.RawUI.SetBufferContents(
                Coordinates(1, host.FrameTopLeft.Y + 1 + pageSelectedIndex),
                newarr
            )
        | true ->
            let selectedLine = host.FrameTopLeft.Y + 1 + pageSelectedIndex

            let linebuffer =
                host.RawUI.GetBufferContents(
                    Rectangle(
                        left = 0,
                        top = selectedLine,
                        right = currSelectedText.Length,
                        bottom = selectedLine
                    )
                )

            for x = 1 to currSelectedText.Length do
                linebuffer[0, x].BackgroundColor <- ConsoleColor.Blue

            host.RawUI.SetBufferContents(Coordinates(0, selectedLine), linebuffer)

        host.RawUI.ForegroundColor <- ConsoleColor.Default
        host.RawUI.BackgroundColor <- ConsoleColor.Black
        

    member this.DisplayHudInfo(hudWidth:int, lines:string[]) =
        host.RawUI.BackgroundColor <- ConsoleColor.Black
        host.RawUI.ForegroundColor <- ConsoleColor.Green
        let mutable hudBuffer =
            host.RawUI.NewBufferCellArray(
                Size(Width = hudWidth , Height = 1),
                bufferCell ' '
            )
        for i = 0 to lines.Length - 1 do
            let line = lines[i]
            let line = line[0..(min (hudWidth - 1) line.Length)]
            Buffer.writeLine(&hudBuffer, 0, line)
            host.RawUI.SetBufferContents(
                Coordinates(
                    x= host.FrameWidth - hudWidth + (hudWidth - line.Length), 
                    y= host.FrameTopLeft.Y + i),
                hudBuffer[*,0..line.Length - 1]
            )
        host.RawUI.SetBufferContents(
            Coordinates(host.FrameWidth - 1, host.FrameTopLeft.Y + lines.Length + 1),
            Array2D.create 1 1 (bufferCell ' ')
        )
        // host.RawUI.FlushInputBuffer()
        host.RawUI.ForegroundColor <- ConsoleColor.Default
        host.RawUI.BackgroundColor <- ConsoleColor.Black

    member this.RenderTopRightHUD(state:DisplayState) =
        //display top right hud info, experimental
            if 
                host.Settings.IsTopRightHudEnabled.Value
                && state.FilteredCache.Count > state.SelectedIndex 
                && OperatingSystem.IsLinux() 
            then
                let hudWidth = 25
                let currentCommand = state.FilteredCache[state.SelectedIndex]
                let hudInfoArray = this.GetCompletionHudInfoAsync(currentCommand, hudWidth).GetAwaiter().GetResult()
                this.DisplayHudInfo(hudWidth=hudWidth, lines=hudInfoArray)

            host.RawUI.ForegroundColor <- ConsoleColor.Default
            host.RawUI.BackgroundColor <- ConsoleColor.Black

    member this.MoveCursorAway(state:DisplayState) =
        host.RawUI.SetBufferContents(
            Coordinates(host.FrameWidth - 1 , host.FrameHeight - 1),
            Array2D.create 1 1 (bufferCell ' ')
        )
    member this.RenderCachedState(state:DisplayState) =
        Buffer.clearInPlace(host,&_cachedBuffer)
        // Buffer.clearFull(host,&_cachedBuffer)
        if state.FilteredCache.Count = 0 then
            this.DrawEmptyFilter(state,&_cachedBuffer)
            host.RawUI.SetBufferContents(host.FrameTopLeft, _cachedBuffer)
        else
            // let pageLength = _cachedBuffer.Length - 2 // frames
            // frameh
            let pageLength = _cachedBuffer.GetLength(0)  - 2 // frames
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
                Graphics.boxTop longest.Length $"%s{state.SanitizedBufferString.Value}[{state.RawFilterText}]"

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
                Buffer.writeLine(&_cachedBuffer, i, content[i])
                
            
            host.RawUI.SetBufferContents(host.FrameTopLeft, _cachedBuffer)

            // color selected line
            this.RenderSelectedLine(currSelectedText,pageSelIndex)
            this.RenderTopRightHUD(state)
            
            
       

// singleton state
type State(cmdlet:PSCmdlet,commandCompletion:CommandCompletion, bufferString:string, bufferCursorPosition:int) = 
    do()


