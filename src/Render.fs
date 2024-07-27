module pscomplete.Render

open System
open System.Management.Automation.Host

type ExitKey =
    | None = 0
    | Tab = 1
    | Enter = 2
    | Escape = 3

[<Struct>]
type LoopContext =
    | Arrow
    | Input
    | InputAdded

type LoopArgs = {
    InitState: DisplayState
    Ui: PSHostRawUserInterface
    ExitCommand: DisplayState -> ExitKey -> unit
}

let startLoop (args: LoopArgs) (beforeKey: DisplayState * LoopContext -> unit) =
    let rec loop (ctx: LoopContext) (state: DisplayState) =
        beforeKey (state, ctx)

        let c = Console.ReadKey(intercept = true)

        match c.Key with
        | ConsoleKey.Tab ->
            match (DisplayState.tabPressed state) with
            | DoNothing(v) -> loop Arrow v
            | InputChanged(v) -> loop Input v
            | Exit(v) -> args.ExitCommand state ExitKey.Tab
        | ConsoleKey.UpArrow when c.Modifiers.HasFlag(ConsoleModifiers.Control) ->
            loop Arrow (DisplayState.pageStart state)
        | ConsoleKey.DownArrow when c.Modifiers.HasFlag(ConsoleModifiers.Control) ->
            loop Arrow (DisplayState.pageEnd state)
        | ConsoleKey.Enter -> args.ExitCommand state ExitKey.Enter
        // escape keys
        | ConsoleKey.Escape -> args.ExitCommand state ExitKey.Escape
        | ConsoleKey.C when c.Modifiers.HasFlag(ConsoleModifiers.Control) ->
            args.ExitCommand state ExitKey.Escape
        | ConsoleKey.F9 -> args.ExitCommand state ExitKey.Escape
        //
        | ConsoleKey.LeftArrow -> loop Arrow (DisplayState.arrowLeftInplace state)
        | ConsoleKey.RightArrow -> loop Arrow (DisplayState.arrowRightInplace state)
        | ConsoleKey.UpArrow -> loop Arrow (DisplayState.arrowUpInplace state)
        | ConsoleKey.DownArrow -> loop Arrow (DisplayState.arrowDownInplace state)
        | ConsoleKey.Home -> loop Arrow (DisplayState.pageStart state)
        | ConsoleKey.End -> loop Arrow (DisplayState.pageEnd state)
        | ConsoleKey.Backspace -> loop Input (DisplayState.backspaceInplace state)
        | keycode ->
            match int keycode with
            | n when c.KeyChar = '\u0000' -> loop LoopContext.Arrow (state) // ignore non-printable characters
            | _ -> loop LoopContext.Input (DisplayState.addFilterCharInplace c.KeyChar state)

    loop LoopContext.Input args.InitState
