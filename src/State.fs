namespace pscomplete

open System.Management.Automation
open System.Linq
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open System.IO

type DisplayState = {
    BufferString: string
    mutable RawFilterText: string
    mutable SelectedIndex: int
    mutable PrevHudArray: string array
    Sout : System.IO.Stream
    Membuf : System.IO.MemoryStream
    Content: CompletionResult[]
    FilteredCache: CompletionResult ResizeArray
    PageLength: int
} with

    member this.GetFilters() = this.RawFilterText.TrimStart('^').Split(' ')

    member this.GetLastWordOfCommand() =
        match this.BufferString.LastIndexOf(' ') with
        | -1 -> this.BufferString
        | n -> this.BufferString.Substring(n + 1)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.TryGoUpBy(x: int) =
        if this.SelectedIndex - x >= 0 then
            this.SelectedIndex <- this.SelectedIndex - x

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    member this.TryGoDownBy(x: int) =
        if this.SelectedIndex + x < this.FilteredCache.Count then
            this.SelectedIndex <- this.SelectedIndex + x

    member this.SanitizedBufferString =
        lazy
            match this.BufferString.IndexOf("\n") with
            | -1 -> this.BufferString
            | n -> ""

    member this.FirstWordOfBufferString =
        lazy
            match this.BufferString.IndexOf(' ') with
            | -1 -> this.BufferString
            | n -> this.BufferString[.. n - 1]

type StateResult =
    | DoNothing of DisplayState
    | InputChanged of DisplayState
    | Exit of DisplayState

module DisplayState =
    open System
    open System.Collections.Generic

    let inline filterResizeArrayInline
        (state: DisplayState)
        (source: ICollection<CompletionResult>)
        : unit
        =
        let temp = source
        state.FilteredCache.Clear()
        let filters = state.GetFilters()

        match state.RawFilterText with
        | v when v.StartsWith '^' ->
            for v in temp do
                if
                    Array.forall
                        (fun filter ->
                            v.ListItemText.StartsWith(filter, StringComparison.OrdinalIgnoreCase)
                        )
                        filters
                then
                    state.FilteredCache.Add(v)

        | _ ->
            for v in temp do
                if
                    Array.forall
                        (fun (filter: string) ->
                            v.ListItemText.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        )
                        filters
                then
                    state.FilteredCache.Add(v)

    let inline filterCacheInPlace(state: DisplayState) =
        let source = state.FilteredCache.ToArray()
        state.FilteredCache.Clear()
        filterResizeArrayInline state source
        state

    let inline filterInPlace(state: DisplayState) =
        let source = state.Content
        state.FilteredCache.Clear()
        filterResizeArrayInline state source
        state

    let inline updateWithFilterText (newFilter: string) (state: DisplayState) =
        state.SelectedIndex <- 0
        state.RawFilterText <- $"%s{newFilter}"
        state

    let inline arrowRightInplace(state: DisplayState) =
        state.TryGoDownBy(state.PageLength)
        state

    let inline arrowDownInplace(state: DisplayState) =
        state.TryGoDownBy(1)
        state


    let pageStart(state: DisplayState) =
        state.SelectedIndex <-
            min
                (state.FilteredCache.Count - 1)
                ((state.SelectedIndex / state.PageLength) * state.PageLength)

        state

    let pageEnd(state: DisplayState) =
        state.SelectedIndex <-
            min
                (state.FilteredCache.Count - 1)
                ((state.SelectedIndex / state.PageLength) * state.PageLength + state.PageLength - 1)

        state

    let arrowLeftInplace(state: DisplayState) =
        state.TryGoUpBy(state.PageLength)
        state

    /// returns none if should exit
    let tabPressed(state: DisplayState) : StateResult =
        let invalidChars = [| '\\'; '/'; '.'; '-'; '$'; '~' |]
        match state.FilteredCache.Count with
        | 0
        | 1 -> StateResult.Exit state
        | _ -> StateResult.Exit state

    let arrowUpInplace(state: DisplayState) =
        state.TryGoUpBy(1)
        state

    let backspaceInplace(state: DisplayState) =
        state.SelectedIndex <- 0
        state.RawFilterText <- state.RawFilterText[.. state.RawFilterText.Length - 2]
        state

    let addFilterCharInplace (c: char) (state: DisplayState) =
        state.SelectedIndex <- 0
        state.RawFilterText <- $"%s{state.RawFilterText}%c{c}"
        state
