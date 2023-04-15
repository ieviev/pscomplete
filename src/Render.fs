module pscomplete.Render

open System
open System.Management.Automation
open System.Management.Automation.Host
open System.Threading
open System.Threading.Tasks
open pscomplete.Helpers

type ExitKey =
    | None = 0
    | Tab = 1
    | Enter = 2
    | Escape = 3


type LoopContext =
    | Arrow
    | Input
    | InputAdded

type LoopArgs =
    {
        InitState : DisplayState
        Ui : PSHostRawUserInterface
        ExitCommand: DisplayState -> ExitKey ->  unit
    }

let startLoop (args:LoopArgs) (beforeKey:DisplayState * LoopContext ->unit) =
    let rec loop (ctx:LoopContext) (state: DisplayState) =
        beforeKey(state,ctx)

        let c = args.Ui.ReadKey(options = readkeyopts)

        match enum<ConsoleKey>(c.VirtualKeyCode) with
        | ConsoleKey.Tab -> 
            let newStateOpt = (DisplayState.tabPressed state)
            match newStateOpt with
            | DoNothing(v) -> loop Arrow v
            | InputChanged(v) -> loop Input v
            | Exit(v) -> args.ExitCommand state ExitKey.Tab
            
        | ConsoleKey.Enter -> args.ExitCommand state ExitKey.Enter
        | ConsoleKey.Escape -> args.ExitCommand state ExitKey.Escape
        | ConsoleKey.C when c.ControlKeyState.HasFlag(ControlKeyStates.LeftCtrlPressed) -> args.ExitCommand state ExitKey.Escape
        | ConsoleKey.LeftArrow -> loop Arrow (DisplayState.arrowLeftInplace state)
        | ConsoleKey.RightArrow -> loop Arrow (DisplayState.arrowRightInplace state)
        | ConsoleKey.UpArrow -> loop Arrow (DisplayState.arrowUpInplace state)
        | ConsoleKey.DownArrow -> loop Arrow (DisplayState.arrowDownInplace state)
        // | ConsoleKey.OemPeriod -> getCompletionAndExit ExitKey.Period
        // | ConsoleKey.Oem2 -> getCompletionAndExit ExitKey.Slash // forward-slash
        // | ConsoleKey.Oem5 -> getCompletionAndExit ExitKey.Backslash // backslash
        | ConsoleKey.Backspace -> loop Input (DisplayState.backspaceInplace state)
        | keycode ->
            match int keycode with
            | n when c.Character = '\u0000' ->loop LoopContext.Arrow (state) // ignore non-printable characters
            | _ -> loop LoopContext.Input (DisplayState.addFilterCharInplace c.Character state)
    
    loop LoopContext.Input args.InitState