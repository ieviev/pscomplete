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
    let boxTop length content =
        $"╔══ {content} ".PadRight(length + 1, '═') + "╗"


    let boxCenter length content =
        $"║{content}".PadRight(length + 1, ' ') + "║"


    let boxBottom length content =
        $"╚══ {content} ".PadRight(length + 1, '═') + "╝"


let bufferCell char =
    BufferCell(
        Character = char,
        BufferCellType = BufferCellType.Complete,
        ForegroundColor = ConsoleColor.White
    )


let truncate n (str: string) =
    match str.Length > n with
    | false -> str
    | true -> str[.. n - 1]

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


        if not (res.ToolTip.StartsWith("[")) then
            res.ListItemText
        else


            match res.ResultType with
            | CompletionResultType.ParameterName ->
                let typeinfo =
                    res.ToolTip
                    |> (fun f -> f.Replace("[]", " array"))
                    |> (fun f ->
                        if f.StartsWith "[" then
                            ": " + f[.. f.IndexOf("]")]
                        elif f.StartsWith "\n" then
                            Regex.Matches(f, "\[-").Count |> (fun f -> $": %i{f}")
                        else
                            ""
                    )

                $"{truncate 30 res.ListItemText, -30}{truncate 25 typeinfo, 25}"
            | _ ->

            let typeinfo =
                res.ToolTip
                |> (fun f -> f.Replace("[]", " array"))
                |> (fun f ->
                    if f.StartsWith "[" then
                        ": " + f[.. f.IndexOf("]")]
                    elif f.StartsWith "\n" then
                        Regex.Matches(f, "\[-").Count |> (fun f -> $": %i{f}")
                    else
                        ""
                )

            $"{res.ListItemText} {typeinfo}"

let readkeyopts =
    ReadKeyOptions.NoEcho
    // ||| ReadKeyOptions.AllowCtrlC
    ||| ReadKeyOptions.IncludeKeyDown



open System.Text
open System
open System.Runtime.InteropServices

type String() =

    static member trimForHud(str: string, [<Optional; DefaultParameterValue(20)>] maxLength: int) =
        let sb = StringBuilder()

        let strSpan = str.AsSpan()

        for i = 0 to (min (str.Length - 1) (maxLength - 1)) do
            match strSpan[i] with
            | '\n' -> ()
            | '\r' -> ()
            | c -> sb.Append(c) |> ignore

        // must fill the whole space
        String.Format($"{{0,{maxLength}}}", sb.ToString())

    static member trimForBuffer
        (
            str: ReadOnlySpan<char>,
            [<Optional; DefaultParameterValue(20)>] maxLength: int
        ) =

        if str.IsEmpty then
            ValueNone
        else
            let span = str
            let arr = System.Buffers.ArrayPool<char>.Shared.Rent(maxLength)
            let arrspan = arr.AsSpan()

            for i = 0 to arr.Length - 1 do
                if i < span.Length then
                    match str[i] with
                    | '\n'
                    | '\r' -> arrspan[i] <- ' '
                    | c -> arrspan[i] <- c
                else
                    arrspan[i] <- ' '

            ValueSome(arr)


module File =

    let getHumanReadableFileSize(i: int64) : string =
        let mutable suffix = String.Empty
        let mutable readable = 0L

        if i < 0x400L then
            $"%i{i} B"
        else
            match i with
            | _ when i >= 0x1000000000000000L ->
                suffix <- "EB"
                readable <- i >>> 50
            | _ when i >= 0x4000000000000L ->
                suffix <- "PB"
                readable <- i >>> 40
            | _ when i >= 0x10000000000L ->
                suffix <- "TB"
                readable <- i >>> 30
            | _ when i >= 0x40000000L ->
                suffix <- "GB"
                readable <- i >>> 20
            | _ when i >= 0x100000L ->
                suffix <- "MB"
                readable <- i >>> 10
            | _ when i >= 0x400L ->
                suffix <- "KB"
                readable <- i
            | _ ->
                suffix <- "B"
                readable <- 0

            readable <- (readable / 1024L)
            readable.ToString("0.### ") + suffix







type PsCompleteSettings(cmdlet: PSCmdlet) =
    let _settings: System.Management.Automation.PSObject =
        cmdlet.GetVariableValue("PsCompleteSettings") |> unbox

    member this.IsTopRightHudEnabled =
        lazy (unbox<bool> (_settings.Properties["TopRightHUDEnabled"].Value))

    member this.PromptText = lazy (unbox<string> (_settings.Properties["PromptText"].Value))


open System.Management.Automation
open System.Reflection

module Runspace =
    let private _runspace = Runspaces.Runspace.DefaultRunspace

    let private _runspace_doPath =
        lazy
            _runspace
                .GetType()
                .GetProperty("DoPath", BindingFlags.NonPublic ||| BindingFlags.Instance)

    let getCurrentPath() =
        let asd = _runspace_doPath.Value.GetValue(_runspace) :?> PathIntrinsics
        asd.CurrentLocation.Path


[<AutoOpen>]
module Ps =
    let inline pwd() = Runspace.getCurrentPath ()

open System.Diagnostics
open System.Collections.Generic

type Process with

    static member runAsync(pname: string, ?pargs: string, ?cwd: string) =
        task {
            use p =
                new Process(
                    StartInfo =
                        ProcessStartInfo(
                            fileName = pname,
                            arguments = defaultArg pargs "",
                            WorkingDirectory = defaultArg cwd "",
                            RedirectStandardOutput = true
                        )
                )

            p.Start() |> ignore
            do! p.WaitForExitAsync()
            return! p.StandardOutput.ReadToEndAsync()
        }

let logDebug(item) =
    IO.File.WriteAllText(
        "/tmp/log.json",
        JsonSerializer.Serialize(item, JsonSerializerOptions(WriteIndented = true))
    )

type ConsoleColor with

    static member Default =
        match System.OperatingSystem.IsWindows() with
        | true -> 0 |> enum<ConsoleColor>
        | false -> -1 |> enum<ConsoleColor>



[<AutoOpen>]
module Patterns =
    // EndsWith ":"
    let (|EndsWith|_|) (p: string) (s: string) =
        if s.EndsWith(p, StringComparison.Ordinal) then Some() else None

    let (|Contains|_|) (p: string) (s: string) =
        if s.Contains(p, StringComparison.Ordinal) then Some() else None

    let (|StartsWith|_|) (p: string) (s: string) =
        if s.StartsWith(p, StringComparison.Ordinal) then
            Some()
        else
            None

    let (|UnfinishedDotnetCommand|_|)(s: string) =
        if Regex.IsMatch(s, @"^\[[^\]]*$") then Some() else None

    let (|Matches|_|) (p: string) (s: string) = if Regex.IsMatch(s, p) then Some() else None





module Directory =
    open System.IO

    let getExecutables(directoryPath: string) =
        directoryPath
        |> Directory.EnumerateFiles
        |> Seq.where (fun x ->
            let isExecutable(fi: FileInfo) =
                int (fi.UnixFileMode &&& (UnixFileMode.UserExecute ||| UnixFileMode.GroupExecute))
                <> 0

            let fileinfo = FileInfo(x)
            fileinfo.Exists && isExecutable fileinfo
        )
        |> Seq.map (fun v ->
            let relpath = Path.GetRelativePath(directoryPath, v)
            CompletionResult($"./{relpath}")
        )
        |> Seq.toArray



open System.Runtime.CompilerServices
open System.Buffers

[<Sealed>]
type ObjectPool<'t>(generate: unit -> 't, initialPoolCount: int) =
    let mutable pool = Queue<'t>()

    do
        for _ = 1 to initialPoolCount do
            pool.Enqueue(generate ())

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Rent() =
        match pool.TryDequeue() with
        | (true, v) -> v
        | _ -> generate ()

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Return(item: 't) = pool.Enqueue(item)



[<Sealed>]
type SharedResizeArray<'t when 't: equality>(initialSize: int) =
    let mutable size = 0
    let mutable limit = initialSize
    let mutable pool: 't array = ArrayPool.Shared.Rent(initialSize)

    member this.EnsureCapacity(n) =
        if limit < n then
            let newArray = ArrayPool.Shared.Rent(n)
            ArrayPool.Shared.Return(pool)
            pool <- newArray
            limit <- newArray.Length

    member this.Add(item) =
        if size = limit then
            let newLimit = limit * 2
            let newArray = ArrayPool.Shared.Rent(newLimit)
            Array.Copy(pool, newArray, size)
            ArrayPool.Shared.Return(pool)
            pool <- newArray
            limit <- newLimit

        pool[size] <- item
        size <- size + 1

    member this.OverwriteWith(items: Span<'t>) =
        this.Clear()
        this.EnsureCapacity(items.Length)
        size <- items.Length
        items.CopyTo(pool.AsSpan())

    member this.Item
        with get (i: int) = pool[i]
        and set (i: int) v = pool[i] <- v

    member this.Clear() = size <- 0

    member this.Contains(item) =
        let mutable e = pool.AsSpan(0, size).GetEnumerator()
        let mutable found = false

        while not found && e.MoveNext() do
            found <- obj.ReferenceEquals(e.Current, item)

        found

    member this.Remove(item: 't) =
        let mutable span: Span<'t> = pool.AsSpan(0, size)
        let mutable e = pool.AsSpan(0, size).GetEnumerator()
        let mutable idx = -1
        let mutable i = 0

        while idx = -1 && e.MoveNext() do
            if obj.ReferenceEquals(e.Current, item) then
                idx <- i

            i <- i + 1

        if idx = size - 1 then
            size <- size - 1
        else
            span[idx] <- span[size - 1]
            size <- size - 1

    member this.GetEnumerator() =
        let mutable e = pool.AsSpan(0, size).GetEnumerator()
        e

    member this.Length = size
    member this.Count = size

    member this.Exists(lambda) =
        let mutable e = pool.AsSpan(0, size).GetEnumerator()
        let mutable found = false

        while not found && e.MoveNext() do
            found <- lambda e.Current

        found

    member this.AsSpan() = pool.AsSpan(0, size)
    member this.AsMemory() = pool.AsMemory(0, size)
    member this.AsArray() = pool.AsSpan(0, size).ToArray()

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.Dispose() = ArrayPool.Shared.Return(pool, false)

    interface IDisposable with
        member this.Dispose() = this.Dispose()


let inline utf8(str: string) = System.Text.Encoding.UTF8.GetBytes(str)

let inline utf8c(str: char) =
    System.Text.Encoding.UTF8.GetBytes($"%c{str}")


module MemoryStream =
    let inline reset(ms: System.IO.MemoryStream) =
        let buf = ms.GetBuffer()
        Array.Clear(buf, 0, int buf.Length)
        ms.Seek(0L, IO.SeekOrigin.Begin) |> ignore
        ms.SetLength(0)

    let inline copyTo (targetStream: IO.Stream) (ms: System.IO.MemoryStream) =
        ms.Seek(0L, IO.SeekOrigin.Begin) |> ignore
        ms.CopyTo(targetStream)


module AnsiColor =
    let BG_GREEN_LIGHT = "\x1b[102m"B
    let FG_BLACK = "\x1b[30m"B
    let RESET = "\x1b[0m"B


module Ansi =

    open System.IO
    let inline clearScreen(sout: Stream) = sout.Write("\x1b[2J\x1b[H"B)

    let inline writePos
        (sout: Stream)
        (line: ReadOnlySpan<byte>)
        (col: ReadOnlySpan<byte>)
        (text: ReadOnlySpan<byte>)
        =
        sout.Write("\x1b["B)
        sout.Write(line)
        sout.Write(";"B)
        sout.Write(col)
        sout.Write("H"B)
        sout.Write(text)

    let inline writeVert(sout: Stream) =
        sout.Write("\x1b[0m"B)
        sout.Write(utf8c Chars.verticalDouble)

    let inline write (sout: Stream) (text: ReadOnlySpan<byte>) =
        sout.Write("\x1b[0m"B)
        sout.Write(text)

    let inline writeWs (sout: Stream) (len: int) =
        for _ = 1 to len do
            sout.Write(" "B)

    let inline writeln (sout: Stream) (text: ReadOnlySpan<byte>) =
        sout.Write("\x1b[0m"B)
        sout.Write(text)
        sout.Write("\n"B)

    let inline writeRed (sout: Stream) (text: ReadOnlySpan<byte>) =
        sout.Write("\x1b[31m"B)
        sout.Write(text)

    let inline writeGreenBg (sout: Stream) (text: ReadOnlySpan<byte>) =
        sout.Write(AnsiColor.FG_BLACK)
        sout.Write(AnsiColor.BG_GREEN_LIGHT)
        sout.Write(text)

    let inline ln(sout: Stream) = sout.Write("\n"B)





module Ast =
    open System.Management.Automation
    open System.Management.Automation.Language

    let tryGetPipelineCommand(cmdlet: PSCmdlet, bufferString: string) =
        let mutable tokens: Token[] = [||]
        let mutable errors: ParseError[] = [||]

        let scriptBlockAst =
            System.Management.Automation.Language.Parser.ParseInput(bufferString, &tokens, &errors)

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


    let tryGetParameterName(cmdlet: PSCmdlet, bufferString: string) =
        let mutable tokens: Token[] = [||]
        let mutable errors: ParseError[] = [||]

        let scriptBlockAst =
            System.Management.Automation.Language.Parser.ParseInput(bufferString, &tokens, &errors)

        let lastStatement = scriptBlockAst.EndBlock.Statements |> Seq.last
        lastStatement


    let rec printTypeInfo(ptype: Type) : string list =
        if isNull ptype then
            []
        else if ptype = typeof<int> then
            List.singleton "int"
        elif ptype = typeof<bool> then List.singleton "bool"
        elif ptype = typeof<byte> then List.singleton "byte"
        elif ptype = typeof<int64> then List.singleton "int64"
        elif ptype = typeof<uint32> then List.singleton "uint"
        elif ptype = typeof<obj> then
            List.singleton "obj"
        elif ptype = typeof<string> then
            List.singleton "string"
        elif ptype = typeof<PSCredential> then
            List.singleton "pscredential"
        elif ptype = typeof<SwitchParameter> then
            List.singleton "switch"
        elif ptype = typeof<ActionPreference> then
            List.singleton "preference"
        elif ptype.IsEnum then
            let evs = ptype.GetEnumNames()
            if evs.Length < 6 then
                evs |> String.concat "|" |> (fun v -> [ $"({v})" ])
            else
                let collection =
                    evs
                    |> Seq.fold
                        (fun (acc: StringBuilder list) v ->
                            match acc with
                            | [] -> acc // impossible
                            | head :: tail when head.Length = 0 ->
                                head.Append($"[{v}") |> ignore
                                acc
                            | head :: tail when (head.Length + v.Length) > 25 ->
                                head.Append(" ") |> ignore
                                let newb = StringBuilder()
                                newb.Append(v) :: acc
                            | head :: tail ->
                                head.Append(", ") |> ignore
                                head.Append(v) |> ignore
                                acc
                        )
                        [ StringBuilder() ]
                    |> Seq.toList

                collection |> Seq.head |> (fun v -> v.Append("]") |> ignore)
                (collection |> List.rev |> List.map (fun v -> v.ToString()))
        elif ptype.IsArray then
            let et = ptype.GetElementType()
            let inner = printTypeInfo et
            List.singleton $"{inner} array"
        elif ptype.IsGenericType then
            let genarg = ptype.GenericTypeArguments
            let gens =
                genarg
                |> Seq.collect (fun v ->
                    let vt = v
                    if vt.IsEnum then List.singleton vt.Name else printTypeInfo v
                )
                |> String.concat ","
                |> (fun v -> $"<{v}>")

            let tname = ptype.Name[.. ptype.Name.Length - 3]
            List.singleton $"{tname}{gens}"


        else
            List.singleton $"{ptype.Name}"


    let rec printAttributesInfo(parameters: ParameterMetadata) : string list =
        parameters.Attributes
        |> Seq.fold
            (fun acc (attr) ->
                match attr with
                | :? ParameterAttribute as a -> acc
                | :? ValidateNotNullOrEmptyAttribute as a -> acc
                | :? ValidateNotNullAttribute as a -> acc
                | :? ValidateRangeAttribute as a -> $"range: {a.MinRange} .. {a.MaxRange}" :: acc
                | :? ValidateSetAttribute as a ->
                    "(" + (a.ValidValues |> String.concat "|") + ")" :: acc
                | :? AliasAttribute as a -> acc
                | :? ArgumentTransformationAttribute as a -> acc
                | :? ArgumentCompleterAttribute as a -> acc
                | _ ->
                    let gt = attr.GetType()

                    match gt.Name with
                    | "ValidateVariableName" -> acc
                    | _ ->
                        let its =
                            gt.GetInterfaces() |> Seq.map (fun v -> v.Name) |> String.concat ""

                        gt.Name :: its :: acc
            // $"{gt.Name}, {its}" :: acc
            // acc
            )
            []
