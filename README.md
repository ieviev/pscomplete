# PsComplete
cross-platform custom powershell autocomplete

rewritten from scratch to make it compatible with mac/linux

## demo  

https://user-images.githubusercontent.com/36763595/201194825-270d5d32-793a-4f42-bb34-56abf2389836.mp4

## installation

- `Install-Module -Name PsComplete`
- `Import-Module -Name PsComplete`
- After that Tab is bound to the expander for this session

Additionally:
- Add `Import-Module -Name PsComplete` to your profile via `Invoke-Item $PROFILE` to have it permanently on
- completion can also be invoked programmatically with `Invoke-GuiPsComplete`

## features

- Search with regex (`^<start>.*<filter>`)
- Performs well up to 100 000+ completions
- Auto-expands to positional parameters (ex. Get-Process (pos.0))
- Press Tab again to immediately select the next parameter (useful for switches)
- Press Enter to finish command
- Optionally auto-expands to command parameters (ex. Get-Process (pos.0)) , see config


## configuration 

PsComplete also has some settings you can override in your profile

#### ForceClearBeforeUse - clears the buffer if not enough space - may erase some preceding buffer space
```
$PsCompleteSettings.ForceClearBeforeUse = $true;
```
#### AutoExpandCommands - immediately presses tab again after finishing one of these commands
```
$PsCompleteSettings.AutoExpandCommands = @(
    "Get-Process"
    "Get-Culture"
)
```

## one caveat because of missing features in Powershell Core:

- Only works when there is enough free space under the current command (use clear or ForceClearBeforeUse)


<!-- 
- the color Black does not exist in windows, it's the background color. however it does exist on linux
- blank color (-1) only exists on linux, throws an exception on windows
- the coordinate systems of linux pwsh and windows are different (windows coordinates are -1)
- there is no way to access the buffer on linux, but it can be overridden with a new array which is destructive to previous screen contents
- there is no way to fill a rectangle on linux using SetBufferContents 
-->


