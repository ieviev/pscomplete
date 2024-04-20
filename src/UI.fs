module pscomplete.UI

open System.Management.Automation
open System.Management.Automation.Host
open System
open Helpers
open System.Management.Automation.Language

// deprecated
// [<Sealed>]
// type Renderer(host:Host) =
//     let host = host
//     let mutable _cachedBuffer : BufferCell [,] = unbox (host.BlankBuffer.Clone())
//     member this.DrawEmptyFilter(state:DisplayState,buffer: byref<BufferCell [,]>) =
//         let top = (Graphics.boxTop 5 $"%s{state.SanitizedBufferString.Value}[%s{state.RawFilterText}]").AsSpan()
//         Buffer.writeLine(&buffer,0,top)

//     member this.GetCompletionHudInfoAsync(result:CompletionResult, maxLength : int) =
//         task {
//             match result.ResultType with
//             | CompletionResultType.ProviderItem ->
//                 let fileInfo = IO.FileInfo( IO.Path.Combine(pwd(),result.CompletionText))
//                 match fileInfo.Exists with
//                 | true ->
//                     let! mimeOutput = System.Diagnostics.Process.runAsync("file",$"--mime-type \"{fileInfo.FullName}\"")
//                     let! fileOutput = System.Diagnostics.Process.runAsync("file",$"\"{fileInfo.FullName}\"")
//                     return [|
//                         "File"
//                         String.trimForHud(mimeOutput.Substring(fileInfo.FullName.Length + 2), maxLength)
//                         File.getHumanReadableFileSize(fileInfo.Length)
//                         String.trimForHud (fileOutput.Substring(fileInfo.FullName.Length + 2), maxLength)
//                     |]
//                 | _ -> return [| "File" |]
//             | CompletionResultType.ProviderContainer ->
//                 let dirInfo = IO.DirectoryInfo( IO.Path.Combine(pwd(),result.CompletionText))
//                 match dirInfo.Exists with
//                 | true ->
//                     let numOfFiles = dirInfo.EnumerateFiles() |> Seq.length
//                     return [|
//                         $"Directory"
//                         $"%i{numOfFiles} files"
//                     |]
//                 | _ -> return [| "Folder" |]
//             | CompletionResultType.Variable -> return [| "Variable" |]
//             | _ when result.ToolTip.StartsWith("{") ->
//                 try
//                     let data =
//                         System.Text.Json.JsonSerializer.Deserialize<HudInfo>(result.ToolTip)
//                     return (data.Lines |> Array.map (fun v -> String.trimForHud(v, maxLength)))
//                 with e ->
//                     return ([||])
//             | CompletionResultType.ParameterValue -> return [||]
//             | _ -> return [|$"%s{result.ResultType.ToString()}"|]
//         }

//     member this.RenderSelectedLine(currSelectedText:string, pageSelectedIndex:int) =
//         match OperatingSystem.IsWindows() with
//         | false ->
//             host.RawUI.BackgroundColor <- ConsoleColor.Green
//             host.RawUI.ForegroundColor <- ConsoleColor.Black
//             let mutable newarr =
//                 host.RawUI.NewBufferCellArray(
//                     Size(Width = currSelectedText.Length, Height = 1),
//                     bufferCell ' '
//                 )
//             Buffer.writeLine(&newarr,0,currSelectedText.AsSpan())
//             host.RawUI.SetBufferContents(
//                 Coordinates(1, host.FrameTopLeft.Y + 1 + pageSelectedIndex),
//                 newarr
//             )
//         | true ->
//             let selectedLine = host.FrameTopLeft.Y + 1 + pageSelectedIndex

//             let linebuffer =
//                 host.RawUI.GetBufferContents(
//                     Rectangle(
//                         left = 0,
//                         top = selectedLine,
//                         right = currSelectedText.Length,
//                         bottom = selectedLine
//                     )
//                 )

//             for x = 1 to currSelectedText.Length do
//                 linebuffer[0, x].BackgroundColor <- ConsoleColor.Blue

//             host.RawUI.SetBufferContents(Coordinates(0, selectedLine), linebuffer)

//         host.RawUI.ForegroundColor <- ConsoleColor.Default
//         host.RawUI.BackgroundColor <- ConsoleColor.Black


//     member this.DisplayHudInfo(hudWidth:int, lines:string seq) =
//         host.RawUI.BackgroundColor <- ConsoleColor.Black
//         host.RawUI.ForegroundColor <- ConsoleColor.Green
//         let mutable hudBuffer =
//             host.RawUI.NewBufferCellArray(
//                 Size(Width = hudWidth , Height = 1),
//                 bufferCell ' '
//             )
//         let mutable finalIdx = 0
//         for idx, line in lines |> Seq.indexed do
//             let line = line[0..(min (hudWidth - 1) line.Length)]
//             Buffer.writeLine(&hudBuffer, 0, line)
//             host.RawUI.SetBufferContents(
//                 Coordinates(
//                     x= host.FrameWidth - hudWidth + (hudWidth - line.Length),
//                     y= host.FrameTopLeft.Y + idx),
//                 hudBuffer[*,0..line.Length - 1]
//             )
//             finalIdx <- idx
//         host.RawUI.ForegroundColor <- enum -1
//         host.RawUI.BackgroundColor <- enum -1
//         host.RawUI.SetBufferContents(
//             Coordinates(host.FrameWidth - 1, host.FrameTopLeft.Y + finalIdx + 1),
//             Array2D.create 1 1 (bufferCell '█')
//         )



//     member this.RenderTopRightHUD(state:DisplayState) =
//         //display top right hud info, experimental
//             if
//                 host.Settings.IsTopRightHudEnabled.Value
//                 && state.FilteredCache.Count > state.SelectedIndex
//                 && OperatingSystem.IsLinux()
//             then
//                 let hudWidth = 25
//                 let currentCommand = state.FilteredCache[state.SelectedIndex]
//                 let hudInfoArray = this.GetCompletionHudInfoAsync(currentCommand, hudWidth).GetAwaiter().GetResult()
//                 state.PrevHudArray <- hudInfoArray
//                 this.DisplayHudInfo(hudWidth=hudWidth, lines=hudInfoArray)

//             host.RawUI.ForegroundColor <- ConsoleColor.Default
//             host.RawUI.BackgroundColor <- ConsoleColor.Black

//     member this.MoveCursorAway(state:DisplayState) =
//         host.RawUI.SetBufferContents(
//             Coordinates(host.FrameWidth - 1 , host.FrameHeight - 1),
//             Array2D.create 1 1 (bufferCell ' ')
//         )
//     member this.RenderContent(completions,state:DisplayState) =
//         let longest: string =
//             completions
//             |> Array.maxBy (fun f -> f.Length)
//         let topLine =
//             Graphics.boxTop longest.Length $"%s{state.SanitizedBufferString.Value}[{state.RawFilterText}]"
//         let bottomLine =
//             Graphics.boxBottom longest.Length $"{state.SelectedIndex + 1} of {state.FilteredCache.Count}"
//         let mutable i = 0
//         Buffer.writeLine(&_cachedBuffer, i, topLine)
//         i <- i + 1
//         for n in completions do
//             Buffer.writeLine (&_cachedBuffer, i,Graphics.boxCenter longest.Length n)
//             i <- i + 1
//         Buffer.writeLine(&_cachedBuffer, i, bottomLine)
//         host.RawUI.SetBufferContents(host.FrameTopLeft, _cachedBuffer)
//     member this.RenderCachedState(state:DisplayState) =
//         Buffer.clearInPlace(host,&_cachedBuffer)
//         if state.FilteredCache.Count = 0 then
//             this.DrawEmptyFilter(state,&_cachedBuffer)
//             host.RawUI.SetBufferContents(host.FrameTopLeft, _cachedBuffer)
//         else
//             let pageLength = _cachedBuffer.GetLength(0)  - 2 // frames
//             let pageIndex = state.SelectedIndex / pageLength
//             let pageSelIndex = state.SelectedIndex % pageLength

//             let completions =
//                 state.FilteredCache
//                 |> Seq.skip (pageIndex * pageLength)
//                 |> Seq.truncate pageLength
//                 |> Seq.map PsCompletion.toText
//                 |> Seq.toArray

//             this.RenderContent(completions,state)
//             let currSelectedText = completions[pageSelIndex]
//             // color selected line
//             this.RenderSelectedLine(currSelectedText,pageSelIndex)
//             this.RenderTopRightHUD(state)


[<Sealed>]
type AnsiRenderer(host: Host) =
    let host = host
    member this.GetCompletionHudInfo(result: CompletionResult) =
        task {
            let mutable hudLines = new SharedResizeArray<string>(1)

            match result.ResultType with
            | CompletionResultType.ProviderItem ->
                let fileInfo = IO.FileInfo(IO.Path.Combine(pwd (), result.CompletionText))
                match fileInfo.Exists with
                | true when OperatingSystem.IsLinux() ->
                    let! mimeOutput =
                        System.Diagnostics.Process.runAsync (
                            "file",
                            $"--mime-type \"{fileInfo.FullName}\""
                        )
                    let! fileOutput =
                        System.Diagnostics.Process.runAsync ("file", $"\"{fileInfo.FullName}\"")
                    hudLines.Add("File")
                    hudLines.Add(mimeOutput.Substring(fileInfo.FullName.Length + 2))
                    hudLines.Add(File.getHumanReadableFileSize (fileInfo.Length))
                    hudLines.Add(fileOutput.Substring(fileInfo.FullName.Length + 2))
                | true ->
                    hudLines.Add("File")
                    hudLines.Add(File.getHumanReadableFileSize (fileInfo.Length))
                | _ -> ()
            | CompletionResultType.ProviderContainer ->
                let dirInfo = IO.DirectoryInfo(IO.Path.Combine(pwd (), result.CompletionText))

                match dirInfo.Exists with
                | true ->
                    let numOfFiles = dirInfo.EnumerateFiles() |> Seq.length
                    hudLines.Add("Directory")
                    hudLines.Add($"%i{numOfFiles} files")
                | _ -> ()
            | CompletionResultType.Variable -> hudLines.Add("Variable")
            | _ when result.ToolTip.StartsWith("{") ->
                try
                    let data = System.Text.Json.JsonSerializer.Deserialize<HudInfo>(result.ToolTip)
                    data.Lines |> Seq.iter hudLines.Add
                with e ->
                    ()
            | CompletionResultType.ParameterValue -> ()
            | CompletionResultType.ParameterName ->
                host.CommandInfo.Value
                |> Option.bind (fun v ->
                    match v.Parameters.TryGetValue(result.ListItemText) with
                    | true, v -> Some v
                    | _ -> None
                )
                |> Option.iter (fun p ->
                    let aliases = 
                        if p.Aliases.Count = 0 then 
                            "" 
                        else
                            " (" + (p.Aliases |> String.concat "|") + ")"
                    hudLines.Add($"Parameter {p.Name}{aliases}")
                    for pts in Ast.printTypeInfo p.ParameterType do
                        hudLines.Add(pts)
                    for attr in Ast.printAttributesInfo p do
                        hudLines.Add(attr)

                )
            | CompletionResultType.Property ->
                
                let availOpt = host.AvailableProperties.Value
                availOpt
                |> Result.map (fun avail -> 
                    avail 
                    |> Seq.tryPick (fun (n,t) -> 
                        match n = result.CompletionText with 
                        | true -> Some t
                        | _ -> None
                    )
                    |> Option.iter (fun p -> 
                        hudLines.Add($"Property {result.CompletionText}")
                        for pts in Ast.printTypeInfo p do
                            hudLines.Add(pts)
                    )
                )
                |> Result.defaultWith (fun v -> 
                    hudLines.Add("no info")
                    // hudLines.Add(v)
                )
                
                // host.PipelineAst.Value 
                // |> Option.iter (fun v -> 
                //     let ce = v.PipelineElements[0] :?> CommandExpressionAst
                //     let ea = ce.Expression
                //     match ce.Expression with 
                //     | :? MemberExpressionAst as me -> 
                 
                //         match me.Expression with 
                //         | :? TypeExpressionAst as te -> 
                //             let rt = te.TypeName.GetReflectionType()
                //             let p = rt.GetProperties() |> Seq.head
                //             hudLines.Add(p.Name)
                //             ()
                //             // hudLines.Add (string te.TypeName)
                //             // // hudLines.Add (string te.StaticType)
                //             // // hudLines.Add (string te.StaticType.Name)
                //             // hudLines.Add (string te.TypeName.Name)
                //             // // hudLines.Add ($"{te.TypeName.AssemblyName}")
                //             // hudLines.Add ($"{te.TypeName.FullName}")
                //             // hudLines.Add ($"{te.TypeName.GetReflectionType()}")
                            
                //             // hudLines.Add (string (te.TypeName.GetReflectionType()))
                //             // hudLines.Add (te.TypeName.FullName)
                //             // hudLines.Add (te.TypeName.AssemblyName)
                //         | _ -> 
                //             hudLines.Add("no info")
                        
                        

                //         ()
                        
                //     | _ -> 
                //         hudLines.Add ($"no info")
                        
                //     // let e2 = ea.GetType().v
                    
                //     // hudLines.Add ($"{v.}")
                //     // // let gv = ce.SafeGetValue()
                //     // hudLines.Add ($"{ea.GetType()}")
                //     // // hudLines.Add ($"{ea.SafeGetValue().GetType().Name}")
                //     // hudLines.Add ($"{ce.Redirections.Count}")
                    
                //     // for s in v.PipelineElements do

                //     //     hudLines.Add $"{s.GetType().Name}"
                // )
            | _ -> hudLines.Add($"%s{result.ResultType.ToString()}")

            return hudLines
        }

    member this.RenderCachedState(state: DisplayState) =
        let sout = state.Sout
        let maxwidth = host.FrameWidth - 2
        let pageLength = host.BlankBuffer.GetLength(0) - 2
        // let pageLength = _cachedBuffer.GetLength(0)  - 2 // frames
        let pageIndex = state.SelectedIndex / pageLength
        let pageSelIndex = state.SelectedIndex % pageLength
        use mutable currPage = new SharedResizeArray<string>(pageLength)

        do
            state.FilteredCache
            |> Seq.skip (pageIndex * pageLength)
            |> Seq.truncate pageLength
            |> Seq.iter (PsCompletion.toText >> currPage.Add)

        let longestLen =
            let mutable l = 0
            for n in currPage do
                if n.Length > l then
                    l <- n.Length
            min l maxwidth

        let topLine =
            Graphics.boxTop
                longestLen
                $"%s{state.SanitizedBufferString.Value}[{state.RawFilterText}]"

        let vis_idx =
            if state.FilteredCache.Count = 0 then
                0
            else
                state.SelectedIndex + 1

        let bottomLine =
            Graphics.boxBottom longestLen $"{vis_idx} of {state.FilteredCache.Count}"

        Ansi.clearScreen state.Sout
        let tout = state.Membuf
        let mutable e = currPage.GetEnumerator()
        let mutable i = 0
        Ansi.writeln tout (utf8 topLine)

        while e.MoveNext() do
            let mutable span = System.Text.Encoding.UTF8.GetBytes(e.Current).AsSpan()
            if span.Length > maxwidth then
                span <- span.Slice(0,maxwidth)

            Ansi.writeVert tout

            if i = pageSelIndex then
                Ansi.writeGreenBg tout span
            else
                Ansi.write tout (span)

            Ansi.writeWs tout (longestLen - span.Length)
            Ansi.writeVert tout
            Ansi.ln tout
            i <- i + 1

        Ansi.writeln tout (utf8 bottomLine)

        let inline whud (ln: int) (str: string) =
            let bytes = utf8 str
            let line = utf8 $"%i{ln}"
            let cstart = host.FrameWidth - bytes.Length
            Ansi.writePos tout line (utf8 $"%i{cstart}") bytes

        // top-right hud
        if
            host.Settings.IsTopRightHudEnabled.Value
            && state.FilteredCache.Count > state.SelectedIndex
            && OperatingSystem.IsLinux()
        then
            let freespace = host.FrameWidth - longestLen - 2
            let hudWidth = if freespace > 35 then 35 else 25
            let currentCommand = state.FilteredCache[state.SelectedIndex]

            let hudInfoArray =
                this.GetCompletionHudInfo(currentCommand).GetAwaiter().GetResult()

            let mutable i = 1

            for hudLine in hudInfoArray do
                let str = (hudLine.Substring(0, min hudWidth hudLine.Length).Trim())
                whud i str
                i <- i + 1

        tout |> MemoryStream.copyTo sout
        MemoryStream.reset tout
