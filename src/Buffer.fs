module Buffer

open pscomplete
open System.Management.Automation.Host
open System
open Helpers

let defaultColor =
    match System.OperatingSystem.IsWindows() with
    | true -> 0 |> enum<ConsoleColor>
    | false -> -1 |> enum<ConsoleColor>

let inline clearInPlace(host: Host, buffer: byref<BufferCell[,]>) =
    host.RawUI.BackgroundColor <- defaultColor
    buffer <- unbox (host.BlankBuffer.Clone())

let inline writeLine(buffer: byref<BufferCell[,]>, lineNo: int, line: ReadOnlySpan<char>) =
    let xmax = buffer.GetLength(1) // - 1
    let trimmed = String.trimForBuffer (line, xmax)

    match trimmed with
    | ValueSome trimmed ->
        for i = 0 to xmax - 1 do
            buffer[lineNo, i].Character <- trimmed[i]

        System.Buffers.ArrayPool<char>.Shared.Return(trimmed)
    | _ ->
        for i = 0 to xmax - 1 do
            buffer[lineNo, i].Character <- ' '


