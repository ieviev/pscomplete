namespace pscomplete

open System.Management.Automation
open System.Linq
open System.Runtime.CompilerServices
open System.Text.RegularExpressions

type DisplayState =
    {
        CommandString: string
        mutable FilterText: string
        mutable SelectedIndex: int
        Content: CompletionResult ResizeArray
        FilteredCache: CompletionResult ResizeArray
        PageLength: int
    }
    with
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
    let filterCacheInPlace (state: DisplayState) =
        let temp = state.FilteredCache.ToArray()
        state.FilteredCache.Clear()
        
        if state.FilterText.StartsWith("^",StringComparison.InvariantCultureIgnoreCase) then
            let tail = state.FilterText.Substring(1)
            for v in temp do
                if v.ListItemText.StartsWith(tail,StringComparison.OrdinalIgnoreCase) 
                then state.FilteredCache.Add(v)
        else
            for v in temp do
                if v.ListItemText.Contains(state.FilterText,StringComparison.OrdinalIgnoreCase) 
                then state.FilteredCache.Add(v)

        // temp
        // |> Seq.where (fun f -> f.ListItemText.Contains(state.FilterText,StringComparison.OrdinalIgnoreCase))
        // |> state.FilteredCache.AddRange

        state
    let filterInPlace (state: DisplayState) =
        state.FilteredCache.Clear()

        if state.FilterText.StartsWith("^",StringComparison.InvariantCultureIgnoreCase) then
            let tail = state.FilterText.Substring(1)
            for v in state.Content do
                if v.ListItemText.StartsWith(tail,StringComparison.OrdinalIgnoreCase) 
                then state.FilteredCache.Add(v)
        else
            for v in state.Content do
                if v.ListItemText.Contains(state.FilterText,StringComparison.OrdinalIgnoreCase) 
                then state.FilteredCache.Add(v)

        // state.Content
        // |> Seq.where (fun f -> f.ListItemText.Contains(state.FilterText,StringComparison.OrdinalIgnoreCase))
        // |> state.FilteredCache.AddRange
        state
     
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let updateWithFilterText (newFilter:string) (state:DisplayState) =
        state.SelectedIndex <- 0
        state.FilterText <- $"%s{newFilter}"
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
        match state.FilteredCache.Count with 
        | 0 | 1 -> StateResult.Exit state 
        | x when x > 100 -> DoNothing state // heuristic. don't want to turn 100000 results to a linked list
        | _ -> 
            let filterContent = state.FilterText.TrimStart('^')
            let shouldExpand = state.FilteredCache.TrueForAll(fun v -> v.CompletionText.StartsWith(filterContent) )
            if shouldExpand then
                let longestCommonPrefix = 
                    state.FilteredCache 
                    |> Seq.map (fun v -> v.CompletionText) 
                    |> Helpers.Seq.longestCommonPrefix
                    |> (fun v -> 
                        let invalidChars = [|'\\';'/';'.';'-';'$';'~'|]
                        v.TrimStart(invalidChars)
                    )
                
                match longestCommonPrefix.Length = filterContent.Length with
                | true -> StateResult.DoNothing state
                | _ -> 

                match state.FilterText.StartsWith("^", StringComparison.InvariantCultureIgnoreCase) with
                | true -> updateWithFilterText ($"^{longestCommonPrefix}") state |> InputChanged
                | false -> updateWithFilterText longestCommonPrefix state |> InputChanged
            else
                //do nothing
                DoNothing state

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let arrowUpInplace (state:DisplayState) =
        state.TryGoUpBy(1)
        state
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let backspaceInplace (state:DisplayState) =
        state.SelectedIndex <- 0
        state.FilterText <- state.FilterText[.. state.FilterText.Length - 2]
        state
        
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let addFilterCharInplace (c:char) (state:DisplayState) =
        state.SelectedIndex <- 0
        state.FilterText <- $"%s{state.FilterText}%c{c}"
        state

    
        