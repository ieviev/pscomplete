# immediately chain into the next argument if its a switch
# or stop if input is expected
using namespace System.Management.Automation
function CompletePsArgument($replacement) {
    if ($replacement.ResultType -eq 'ProviderContainer') {
    }
    switch ($replacement.ArgumentType) {
        "psobject" {
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert('.');
            Invoke-GuiPsComplete;
        } 
        "switch" { 
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' -');
            Invoke-GuiPsComplete;
        }
        "IDictionary" { 
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' @{ "" = "" }');
            [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition($cursorPosition + $replacement.CompletionText.Length + 4);
        }
        "string array" { 
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ');
        }
        { @("CommandTypes", "ActionPreference") -contains $_ } {
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ');
            Invoke-GuiPsComplete;
        }
        Default {
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ');
            # [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ')
            
        }
    }
}


## Command Helpers
function CommandGetType {
    [OutputType([System.Management.Automation.CommandTypes])]
    Param ([CommandInfo]$commandinfo)   
    $commandinfo 
    | Select-Object -First 1 -ExpandProperty CommandType
}
function GetPositionParameters {
    [OutputType([CommandParameterInfo])]
    Param ($parameters)   
    $parameters
    | Where-Object -Property Position -NE ([Int32]::MinValue)
    | Sort-Object -Property Position
}

# $PsCompleteSettings = 
#     Get-Variable -Name PsCompleteSettings -ErrorVariable $ErrorSettingsFound
    
# global:
New-Variable -Scope Global -Name PsCompleteSettings -Value ([PSCustomObject]@{
        AutoExpandCommands   = @("")
        ExpandByArgumentType = $false
        ForceClearBeforeUse  = $true
    })


function HandleCompletionCommand($commandname) {
    
    $command = Get-Command $commandname
    [System.Management.Automation.CommandTypes] $commandType = CommandGetType $command
    # debug
    # @{r = $command; r2 = $commandname } | ConvertTo-Json -Depth 5 > $env:HOME/Desktop/sample2.json                    
    switch ($commandType) {
        ([System.Management.Automation.CommandTypes]::Application) { 
            ## e.g. apt, cmd, /bin/sh - insert space after
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ')
            $cmdlen = "$replacement.CompletionText".Length + 1
            [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition($cmdlen)
        }
        ([System.Management.Automation.CommandTypes]::Cmdlet) {
            $params = $command.ParameterSets[0].Parameters
            $posParameters = GetPositionParameters($params)
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ')                
            if ($posParameters.Length -gt 0) {
                $p1 = $posParameters[0]
                [System.Type] $p1Type = $p1.ParameterType
                
                if ($PsCompleteSettings.ExpandByArgumentType) {
                    switch ($p1Type.FullName) {
                                        ("System.String[]") { 
                            # [Microsoft.PowerShell.PSConsoleReadLine]::Insert("''")
                            $cmdlen = "$replacement.CompletionText".Length + 1
                            [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition($cmdlen)
                        }
                        Default {}
                    }
                }
                if ($PsCompleteSettings.AutoExpandCommands.Contains($command.Name)) {
                    Invoke-GuiPsComplete
                }
            }
                            
        }
        
        ([System.Management.Automation.CommandTypes]::Function) {
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ')
        }
                        
        Default {
            [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ')
        }
    }
}

function AnsiClearScreen() {
    Write-Host -NoNewline "`e[2J"
    Write-Host -NoNewline "`e[H"
}

function quoteIfNeeded( [string] $str) {
    if ($str.Contains(' ') -and ( -not $str.Contains('.'))) { 
        return $str;
    } 
    else { 
        return $str;
    }
}


function Invoke-GuiPsComplete() {
    $buffer = ''
    $cursorPosition = 0
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$buffer, [ref]$cursorPosition)
    if ($buffer -eq '') { return }
    $completion = TabExpansion2 $buffer $cursorPosition 
    
    if ($completion.CompletionMatches.Count -eq 0) {
        return
    }

    $useAnsiWorkaround = $false
    ## create space for completions via ansi escape sequence
    $frameh = $Host.UI.RawUI.WindowSize.Height - $Host.UI.RawUI.CursorPosition.Y - 1
    if ($frameh -lt 6 -and $PsCompleteSettings.ForceClearBeforeUse) {
        AnsiClearScreen;
        $useAnsiWorkaround = $true;
    }
    
    $replacement = 
    Invoke-PsComplete `
        -Content $completion.CompletionMatches `
        -CommandParameter "$buffer" `
    
    ## for remote host commands to work correctly
    $colonIndex = "$buffer".IndexOf(':');
    $lastSpaceIndex = "$buffer".LastIndexOf(' ');
    if ($colonIndex -lt $lastSpaceIndex) {
        $colonIndex = -1;
    }


    # debug
    # @{r = $replacement; r2 = $completion } | ConvertTo-Json -Depth 5 > "/mnt/ramdisk/logfile.json"

    if ($replacement) {
        if ($useAnsiWorkaround ) { AnsiClearScreen }
        $quoted = quoteIfNeeded($replacement.CompletionText);

        switch ($replacement.ExitKey) {
            Tab {
                ## ex scp host:/home/user/
                if ($colonIndex -ne -1) {
                    $commandHost = "$buffer".Substring(0, $colonIndex)
                    $fullCompletionText = "$($commandHost):$($replacement.CompletionText)"
                    [Microsoft.PowerShell.PSConsoleReadLine]::Replace(
                        0, 
                        "$buffer".Length, 
                        $fullCompletionText
                    )
                }
                else {

                    [Microsoft.PowerShell.PSConsoleReadLine]::Replace(
                        $completion.ReplacementIndex, 
                        $completion.ReplacementLength, 
                        $quoted)

                    if ($replacement.ResultType -eq 'Command') {
                        HandleCompletionCommand $replacement.CompletionText
                    }
                    elseif ($replacement.ResultType -eq 'ParameterName') {
                        CompletePsArgument $replacement
                    }
                    elseif ($replacement.ResultType -eq 'ProviderContainer') {
                        if ("$quoted".Contains(" ")) {
                            # if item contains spaces to nothing for now.
                            # 'asd fgh'/ is not valid
                        }
                        else {
                            
                            if ([System.Environment]::OSVersion.Platform -eq 'Unix') {
                                [Microsoft.PowerShell.PSConsoleReadLine]::Insert('/');
                            }
                            else {
                                [Microsoft.PowerShell.PSConsoleReadLine]::Insert('\');
                            }
                        }
                        
                    }
                    else {
                        ## e.g. apt install[SPACE]
                        [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ');
                    }
                }
            }
            Enter {
                ## ex scp host:/home/user/
                if ($colonIndex -ne -1) {
                    $commandHost = "$buffer".Substring(0, $colonIndex)
                    $fullCompletionText = "$($commandHost):$($replacement.CompletionText)"
                    [Microsoft.PowerShell.PSConsoleReadLine]::Replace(
                        0, 
                        "$buffer".Length, 
                        $fullCompletionText
                    )
                }
                else {
                    [Microsoft.PowerShell.PSConsoleReadLine]::Replace($completion.ReplacementIndex, $completion.ReplacementLength, $quoted)
                    if ($replacement.ResultType -eq 'ProviderContainer') {
                        if ("$quoted".Contains(" ")) {
                            # if item contains spaces to nothing for now.
                            # 'asd fgh'/ is not valid
                        }
                        else {
                            if ([System.Environment]::OSVersion.Platform -eq 'Unix') {
                                [Microsoft.PowerShell.PSConsoleReadLine]::Insert('/');
                            }
                            else {
                                [Microsoft.PowerShell.PSConsoleReadLine]::Insert('\');
                            }
                        }
                        
                    }
                    else {
                        [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ');
                    }
                }
            }
            Escape {
                [Microsoft.PowerShell.PSConsoleReadLine]::SetCursorPosition($cursorPosition);
            }
            ## if there is a single option
            None {
                if ($colonIndex -ne -1) {
                    $commandHost = "$buffer".Substring(0, $colonIndex)
                    $fullCompletionText = "$($commandHost):$($replacement.CompletionText)"
                    [Microsoft.PowerShell.PSConsoleReadLine]::Replace(
                        0, 
                        "$buffer".Length, 
                        $fullCompletionText
                    )
                }
                else {
                    [Microsoft.PowerShell.PSConsoleReadLine]::Replace($completion.ReplacementIndex, $completion.ReplacementLength, $replacement.CompletionText)
                    if ($replacement.ResultType -eq 'Command') {
                        HandleCompletionCommand $replacement.CompletionText
                    }
                    elseif ($replacement.ResultType -eq 'ProviderContainer') {
                        if ([System.Environment]::OSVersion.Platform -eq 'Unix') {
                            [Microsoft.PowerShell.PSConsoleReadLine]::Insert('/');
                        }
                        else {
                            [Microsoft.PowerShell.PSConsoleReadLine]::Insert('\');
                        }
                    }
                    else {
                        [Microsoft.PowerShell.PSConsoleReadLine]::Insert(' ');
                    }
                }
            }
        }
    }
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
