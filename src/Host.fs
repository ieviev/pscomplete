namespace pscomplete
open System.Management.Automation
open Helpers
open System.Management.Automation.Host
open System.Management.Automation.Language
open System.Reflection

type HudInfo = {
    Lines : string[]
}


[<Sealed>]
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
    // let _blankbuffer = rawUI.NewBufferCellArray(Size(_frameWidth, _frameHeight), bufferCell ' ')
    let _scriptBlockAst = 
        lazy 
            let mutable tokens: Token[] = [||]
            let mutable errors: ParseError[] = [||]
            let scriptBlockAst =
                System.Management.Automation.Language.Parser.ParseInput(bufferString, &tokens, &errors)
            scriptBlockAst

    let _pipelineAst = 
        lazy 
            let lastStatement = _scriptBlockAst.Value.EndBlock.Statements |> Seq.last
            match lastStatement with
            | :? PipelineAst as pipelineast -> Some pipelineast
            | _ -> None
    let _commandInfo : Lazy<CommandInfo option> = 
        lazy 
            let astOpt = _pipelineAst.Value
            astOpt
            |> Option.bind (fun ast -> 
                let lastPipelineElement = ast.PipelineElements |> Seq.last
                match lastPipelineElement with
                | :? CommandAst as commandast ->
                    let commandName = commandast.GetCommandName()
                    let commandInfo = cmdlet.InvokeCommand.GetCommand(commandName, CommandTypes.All)
                    Some(commandInfo)
                | _ -> None
            )
    member this.FrameHeight = _frameHeight
    member this.FrameWidth = _frameWidth
    member val FrameTopLeft = _frameTopLeft
    member val RawUI = rawUI
    // member val BlankBuffer = _blankbuffer
    member val Settings = settings
    member val Cmdlet = cmdlet
    member val BufferString = bufferString
    member val ScriptBlockAst = _scriptBlockAst
    member val CommandInfo = _commandInfo
    member val PipelineAst = _pipelineAst
        
        
    member val AvailableProperties = 
        lazy 
            let asto = _pipelineAst.Value
            match asto with 
            | None -> Error ("no ast")
            | Some ast -> 
                // get prev pipeline output
                if ast.PipelineElements.Count > 1 then
                    match ast.PipelineElements[(ast.PipelineElements.Count - 2)] with 
                    | :? CommandAst as ca -> 
                        let commandName = ca.GetCommandName()
                        let commandInfo = cmdlet.InvokeCommand.GetCommand(commandName, CommandTypes.All)
                        commandInfo.OutputType
                        |> Seq.collect (fun v -> 
                            v.Type.GetProperties()
                            |> Seq.map (fun v -> v.Name, v.PropertyType)
                        )
                        |> Seq.distinctBy fst
                        |> Seq.toArray
                        |> Ok
                    | _ ->     
                        Error("invalid pipeline elem")
                elif ast.PipelineElements.Count = 1 then
                    match ast.PipelineElements[0] with 
                    | :? CommandExpressionAst as ce -> 
                        match ce.Expression with 
                        | :? MemberExpressionAst as me -> 
                            match me.Expression with 
                            | :? TypeExpressionAst as te ->
                                let rt = te.TypeName.GetReflectionType()
                                let props1 = 
                                    rt.GetProperties()
                                    |> Seq.map (fun v -> v.Name, v.PropertyType )   
                                let props2 = 
                                    rt.GetFields()
                                    |> Seq.map (fun v -> v.Name, v.FieldType )   
                                Seq.append props1 props2
                                |> Seq.toArray
                                |> Ok
                            | _ -> Error("t1")
                        | _ -> Error("t2")
                    | _ -> 
                        Error("t3")
                else 
                    match _commandInfo.Value with 
                    | None -> Error ("no ci")
                    | Some ci -> 
                        ci.Parameters
                        |> Seq.map (fun v -> v.Key,v.Value.ParameterType)
                        |> Seq.distinctBy fst
                        |> Seq.toArray
                        |> Ok
                    
            
            
    
    
