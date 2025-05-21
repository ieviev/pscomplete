module pscomplete.UI

open System.Management.Automation
open System
open Helpers
open System.Management.Automation.Language

[<Sealed>]
type AnsiRenderer(host: Host) =
    let host = host

    member this.GetContextInfo(result: CompletionResult) =
        let mutable contextLines = new SharedResizeArray<string>(1)

        match result.ResultType with
        | _ when String.IsNullOrWhiteSpace result.ToolTip -> ()
        | _ when result.ToolTip = "λ" ->
            match PsCompleteSettings.Callbacks.TryGetValue result.CompletionText with
            | true, lambda ->
                let result = lambda.Invoke result.CompletionText
                use ls = new IO.StringReader(result)
                let mutable curr = null

                while (curr <- ls.ReadLine()
                       curr <> null) do
                    contextLines.Add curr
            | _ -> ()
        | CompletionResultType.ProviderItem ->
            let fileInfo = IO.FileInfo(IO.Path.Combine(pwd (), result.CompletionText))

            match fileInfo.Exists with
            | true when OperatingSystem.IsLinux() ->
                try
                    let mimeOutput =
                        System.Diagnostics.Process
                            .runAsync("file", $"--mime-type \"{fileInfo.FullName}\"")
                            .Result

                    let fileOutput =
                        System.Diagnostics.Process
                            .runAsync("file", $"\"{fileInfo.FullName}\"")
                            .Result

                    contextLines.Add("File")
                    contextLines.Add(mimeOutput.Substring(fileInfo.FullName.Length + 2))
                    contextLines.Add(File.getHumanReadableFileSize (fileInfo.Length))
                    contextLines.Add(fileOutput.Substring(fileInfo.FullName.Length + 2))
                with e ->
                    contextLines.Add(e.Message)
            | true ->
                contextLines.Add("File")
                contextLines.Add(File.getHumanReadableFileSize (fileInfo.Length))
            | _ -> ()
        | CompletionResultType.ProviderContainer ->
            let dirInfo = IO.DirectoryInfo(IO.Path.Combine(pwd (), result.CompletionText))

            match dirInfo.Exists with
            | true ->
                try
                    let numOfFiles = dirInfo.EnumerateFiles() |> Seq.length
                    contextLines.Add "Directory"
                    contextLines.Add $"%i{numOfFiles} files"
                with _ ->
                    contextLines.Add "Directory"
                    contextLines.Add $"access denied"
            | _ -> ()
        | CompletionResultType.Variable -> contextLines.Add("Variable")
        | CompletionResultType.Method ->
            contextLines.Add $"Method"
            let ast = host.PipelineAst.Value.Value
            let pe = ast.PipelineElements |> Seq.last

            match pe with
            | :? CommandExpressionAst as ce ->
                // contextLines.Add($"ce:{ce}")
                match ce.Expression with
                | :? MemberExpressionAst as me ->
                    // hudLines.Add($"me:{me.Expression.GetType().Name}")
                    match me.Expression with
                    | :? TypeExpressionAst as te ->
                        let rt = te.TypeName.GetReflectionType()
                        // hudLines.Add($"type {rt.Name}")
                        let mems =
                            rt.GetMembers()
                            |> Seq.where (fun v -> v.Name = result.ListItemText)
                            |> Seq.toArray
                        // rtmethodinfo
                        for m in mems do
                            match m with
                            | :? System.Reflection.MethodInfo as v ->
                                Ast.printMethodSignature v |> Seq.iter contextLines.Add
                            | _ -> ()
                    | _ -> ()
                | _ -> ()
            | _ -> ()
        | CompletionResultType.Command ->
            let ci =
                match
                    host.Cmdlet.InvokeCommand.GetCommand(
                        result.CompletionText,
                        CommandTypes.All
                    )
                    |> Option.ofObj
                with
                | Some n when n.CommandType = CommandTypes.Alias ->
                    Some(host.Cmdlet.InvokeCommand.GetCommand(n.Definition, CommandTypes.All))
                | v -> v


            match ci with
            | None -> contextLines.Add "no info"
            | Some ci ->

                let output =
                    ci.OutputType
                    |> Seq.collect (fun v -> Ast.printTypeInfo (v.Type))
                    |> String.concat "|"

                contextLines.Add $"{ci.CommandType} : [{output}]"
                contextLines.Add ""

                try
                    let nonCommon =
                        ci.Parameters
                        |> Seq.where (fun v ->
                            not (isNull v.Value)
                            && PSCmdlet.CommonParameters.Contains(v.Key) |> not
                        )
                        |> Seq.map (fun v -> v.Value)

                    for p in nonCommon do
                        let aliases =
                            if p.Aliases.Count = 0 then
                                ""
                            else
                                (p.Aliases |> Seq.map (fun v -> $"-{v}") |> String.concat "|")
                                + ", "

                        if not (isNull p.ParameterType) then
                            let pts = Ast.printTypeInfo p.ParameterType
                            contextLines.Add($"{aliases}{p.Name} : {pts}")
                with e ->
                    ()


        | CompletionResultType.ParameterValue -> ()
        | CompletionResultType.ParameterName ->
            host.CommandInfo.Value
            |> Option.bind (fun v ->
                try
                    match v.Parameters.TryGetValue(result.ListItemText) with
                    | true, v -> Some v
                    | _ -> None
                with e ->
                    None
            )
            |> Option.iter (fun p ->
                let aliases =
                    if p.Aliases.Count = 0 then
                        ""
                    else
                        (p.Aliases |> Seq.map (fun v -> $"-{v}") |> String.concat "|") + ", "

                contextLines.Add($"{aliases}{p.Name}")

                for pts in Ast.printTypeInfo p.ParameterType do
                    contextLines.Add(pts)

                for attr in Ast.printAttributesInfo p do
                    contextLines.Add(attr)

            )
        | CompletionResultType.Property ->

            let availOpt = host.AvailableProperties.Value

            availOpt
            |> Result.map (fun avail ->
                avail
                |> Seq.tryPick (fun (n, t) ->
                    match n = result.CompletionText with
                    | true -> Some t
                    | _ -> None
                )
                |> Option.iter (fun p ->
                    contextLines.Add $"Property {result.CompletionText}"

                    for pts in Ast.printTypeInfo p do
                        contextLines.Add(pts)
                )
            )
            |> Result.defaultWith (fun _ -> contextLines.Add "no info")
        | _ -> contextLines.Add(result.ResultType.ToString())

        contextLines

    member this.RenderCachedState(state: DisplayState) =
        let sout = state.Sout
        let maxwidth = host.FrameWidth - 2
        let pageLength = host.FrameHeight - 2
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

        let tempstream = state.Membuf
        let mutable e = currPage.GetEnumerator()
        let mutable i = 0
        Ansi.writeln tempstream (utf8 topLine)

        while e.MoveNext() do
            let mutable span = System.Text.Encoding.UTF8.GetBytes(e.Current).AsSpan()

            if span.Length > maxwidth then
                span <- span.Slice(0, maxwidth)

            Ansi.writeVert tempstream

            if i = pageSelIndex then
                Ansi.writeGreenBg tempstream span
            else
                Ansi.write tempstream (span)

            Ansi.writeWs tempstream (longestLen - span.Length)
            Ansi.writeVert tempstream
            Ansi.ln tempstream
            i <- i + 1

        let bottomLine =
            Graphics.boxBottom longestLen $"{vis_idx} of {state.FilteredCache.Count}"

        Ansi.writeln tempstream (utf8 bottomLine)

        let inline whud (ln: int) (str: string) =
            let bytes = utf8 str
            let line = utf8 $"%i{ln}"
            let cstart = host.FrameWidth - bytes.Length
            Ansi.writePos tempstream line (utf8 $"%i{cstart}") bytes

        // optional top-right hud
        if
            host.Settings.IsTopRightHudEnabled.Value
            && state.FilteredCache.Count > state.SelectedIndex
        then
            let freespace = host.FrameWidth - longestLen - 2

            let contextWidth =
                if freespace > 45 then 45
                elif freespace > 35 then 35
                else 25

            let currentCommand = state.FilteredCache[state.SelectedIndex]

            let contextInfoArray = this.GetContextInfo(currentCommand)

            if contextInfoArray.Count = 0 then
                ()
            else

                let mutable i = 1

                for contextLine in contextInfoArray do
                    let str =
                        (contextLine.Substring(0, min contextWidth contextLine.Length).Trim())

                    whud i str
                    i <- i + 1

        System.Console.Clear()
        tempstream.Seek(0L, IO.SeekOrigin.Begin) |> ignore
        tempstream.CopyTo(sout)
        Console.CursorVisible <- false
        sout.Flush()
        MemoryStream.reset tempstream
