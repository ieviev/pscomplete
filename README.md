# PsComplete
cross-platform custom powershell autocomplete

rewritten from scratch to make it compatible with mac/linux



## demo  

https://user-images.githubusercontent.com/36763595/231883963-51af0857-ef83-47f1-9aca-f011304c1b04.mp4


## installation

(available at https://www.powershellgallery.com/packages/PsComplete)

- `Install-Module -Name PsComplete`
- `Import-Module -Name PsComplete`
- After that Tab is bound to the expander for this session

Additionally:
- Add `Import-Module -Name PsComplete` to your profile via `Invoke-Item $PROFILE` to have it permanently on
- completion can also be invoked programmatically with `Invoke-GuiPsComplete`

## features

- Performs well up to 100 000+ completions
- Press Tab to expand / select if only 1 remains
- Press Enter to select highlighted command

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

- pwsh can not access the preceding buffer directly, so the only way to make space for completions is to clear the entire buffer

#### some more trivia about the implementations

- the color Black does not exist in windows, it's the background color. however it does exist on linux
- blank color (-1) only exists on linux, throws an exception on windows
- the coordinate systems of linux pwsh and windows are different (windows coordinates are -1 relative to linux)
- there is no way to access the buffer on linux, but it can be overridden with a new array which is destructive to previous screen content
- there is no way to fill a rectangle on linux using SetBufferContents 

