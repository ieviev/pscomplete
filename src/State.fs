namespace pscomplete

open System.Management.Automation
open System.Linq
open System.Runtime.CompilerServices
open System.Text.RegularExpressions

type DisplayState =
    {
        CommandString: string
        mutable RawFilterText: string
        mutable SelectedIndex: int
        Content: CompletionResult ResizeArray
        FilteredCache: CompletionResult ResizeArray
        PageLength: int
    }
    with
        member this.GetFilters() = 
            this.RawFilterText.TrimStart('^').Split(' ')

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member this.TryGoUpBy (x:int) =
            if this.SelectedIndex - x >= 0 then
                this.SelectedIndex <- this.SelectedIndex - x
                
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member this.TryGoDownBy (x:int) =
            if this.SelectedIndex + x < this.FilteredCache.Count then
                this.SelectedIndex <- this.SelectedIndex + x
                


type StateResult = 
    | DoNothing of DisplayState
    | InputChanged of DisplayState
    | Exit of DisplayState 

module DisplayState =
    open System
    open System.Collections.Generic

    let inline filterResizeArrayInline (state:DisplayState) (source:ICollection<CompletionResult>) : unit =
        let temp = source
        state.FilteredCache.Clear()
        let filters = state.GetFilters()
        match state.RawFilterText with
        | v when v.StartsWith '^' -> 
            for v in temp do
                if 
                    Array.forall 
                        (fun filter -> 
                            v.ListItemText.StartsWith(
                                filter,StringComparison.OrdinalIgnoreCase)) filters
                then 
                    state.FilteredCache.Add(v)
                    
        | _ -> 
            for v in temp do
                if 
                    Array.forall 
                        (fun (filter:string) -> 
                            v.ListItemText.Contains(
                                filter,StringComparison.OrdinalIgnoreCase)) filters
                then 
                    state.FilteredCache.Add(v)
    let filterCacheInPlace (state: DisplayState) =
        let source = state.FilteredCache.ToArray()
        state.FilteredCache.Clear()
        filterResizeArrayInline state source
        state
    let filterInPlace (state: DisplayState) =
        let source = state.Content 
        state.FilteredCache.Clear()
        filterResizeArrayInline state source
        state
     
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let updateWithFilterText (newFilter:string) (state:DisplayState) =
        state.SelectedIndex <- 0
        state.RawFilterText <- $"%s{newFilter}"
        state

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let arrowRightInplace (state:DisplayState) =
        state.TryGoDownBy(state.PageLength)
        state
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let arrowDownInplace (state:DisplayState) =
        state.TryGoDownBy(1)
        state
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let arrowLeftInplace (state:DisplayState) =
        state.TryGoUpBy(state.PageLength)
        state

    /// returns none if should exit
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let tabPressed (state:DisplayState) : StateResult =
        let invalidChars = [|'\\';'/';'.';'-';'$';'~'|]
        match state.FilteredCache.Count with 
        | 0 | 1 -> StateResult.Exit state 
        // | x when x > 100 -> DoNothing state // heuristic. don't want to turn 100000 results to a linked list
        | _ -> 
            let filterContent = state.RawFilterText.TrimStart('^')
            let shouldExpand = state.FilteredCache.TrueForAll(fun v -> v.CompletionText.StartsWith(filterContent) )
            if shouldExpand then
                let longestCommonPrefix = 
                    state.FilteredCache 
                    |> Seq.map (fun v -> v.CompletionText) 
                    |> Helpers.Seq.longestCommonPrefix
                    |> (fun v -> 
                        v.TrimStart(invalidChars)
                    )
                
                match longestCommonPrefix.Length = filterContent.Length with
                | true -> StateResult.DoNothing state
                | _ -> 

                match state.RawFilterText.StartsWith("^", StringComparison.InvariantCultureIgnoreCase) with
                | true -> updateWithFilterText ($"^{longestCommonPrefix}") state |> InputChanged
                | false -> updateWithFilterText longestCommonPrefix state |> InputChanged
            else
                //do nothing
                StateResult.Exit state

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let arrowUpInplace (state:DisplayState) =
        state.TryGoUpBy(1)
        state
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let backspaceInplace (state:DisplayState) =
        state.SelectedIndex <- 0
        state.RawFilterText <- state.RawFilterText[.. state.RawFilterText.Length - 2]
        state
        
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let addFilterCharInplace (c:char) (state:DisplayState) =
        state.SelectedIndex <- 0
        state.RawFilterText <- $"%s{state.RawFilterText}%c{c}"
        state

    
        