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
    Invoke-PsComplete `
        -CommandCompletion $completion `
        -BufferString $bufferString `
        -BufferCursorPosition $bufferCursorPosition
}


function Install-PsComplete() {
    if ([System.Reflection.Assembly]::Load("FSharp.Core") -eq $null){
        Import-Module "$PSScriptRoot/FSharp.Core.dll"    
    }
    
    try {
        Import-Module "$PSScriptRoot/pscomplete.dll"   
    }
    catch {

    }

    Set-PSReadLineKeyHandler -Chord 'Tab' -ScriptBlock { 
        Invoke-GuiPsComplete;
    }
}

Install-PsComplete
