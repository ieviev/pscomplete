# immediately chain into the next argument if its a switch
# or stop if input is expected
using namespace System.Management.Automation

# global:
New-Variable -Scope Global -Name PsCompleteSettings -Value ([PSCustomObject]@{
        AutoExpandCommands   = @("")
        ExpandByArgumentType = $false
        ForceClearBeforeUse  = $false
        TopRightHUDEnabled  = $false
        PromptText  = "❯ "
    })


function Invoke-GuiPsComplete() {
    [string] $bufferString = ''
    [int] $bufferCursorPosition = 0
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$bufferString, [ref]$bufferCursorPosition)
    if ($bufferString -eq '') { 
        $bufferString = "./" 
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert("./")
        $bufferCursorPosition = 2
    }
    [System.Management.Automation.CommandCompletion] $completion = TabExpansion2 $bufferString $bufferCursorPosition 

    # $frameh = $Host.UI.RawUI.WindowSize.Height - $Host.UI.RawUI.CursorPosition.Y - 1
    # if ($frameh -lt 8 -and $PsCompleteSettings.ForceClearBeforeUse) {
    #     AnsiClearScreen;
    # }
    
    Invoke-PsComplete `
        -CommandCompletion $completion `
        -BufferString $bufferString `
        -BufferCursorPosition $bufferCursorPosition
}


function Install-PsComplete() {
    $loadedAssemblies = `
        [System.AppDomain]::CurrentDomain.GetAssemblies() `
    | Where-Object Location `
    | ForEach-Object { $_.GetName().Name };
   
    if (!($loadedAssemblies.Contains('FSharp.Core'))) {
        Import-Module "$PSScriptRoot/FSharp.Core.dll"    
    }
    if (!($loadedAssemblies.Contains('pscomplete'))) {
        Import-Module "$PSScriptRoot/pscomplete.dll"   
    }

    Set-PSReadLineKeyHandler -Chord 'Tab' -ScriptBlock { 
        Invoke-GuiPsComplete;
    }
}

Install-PsComplete
