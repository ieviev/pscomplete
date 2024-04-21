# PsComplete
cross-platform custom powershell autocomplete

## demo  


https://github.com/ieviev/pscomplete/assets/36763595/06742872-b7dd-465e-a5a5-8c2d928421b9



## installation

(available at https://www.powershellgallery.com/packages/PsComplete)

- `install-module -name pscomplete`
- `import-module -name pscomplete`
- After that Tab is bound to the expander for this session

Additionally:
- Add `import-module -name pscomplete` to your profile (ex. `nano $PROFILE` / `notepad $PROFILE`) to have it permanently on
- completion can also be invoked programmatically with `invoke-guipscomplete`

## features

- Performs well up to 100 000+ completions
- Press Tab / Enter to pick the highlighted command

## configuration

the top-right context info in the demo is not on by default and 
can be turned on by adding this to your profile

```powershell
$PsCompleteSettings.TopRightHUDEnabled = $true; # off by default
```


<!-- 
## one caveat because of missing features in Powershell Core:

- pwsh can not access the preceding buffer directly, so the only way to make space for completions is to clear the entire buffer

#### some more trivia about the implementations

- the color Black does not exist in windows, it's the background color. however it does exist on linux
- blank color (-1) only exists on linux, throws an exception on windows
- the coordinate systems of linux pwsh and windows are different (windows coordinates are -1 relative to linux)
- there is no way to access the buffer on linux, but it can be overridden with a new array which is destructive to previous screen content
- there is no way to fill a rectangle on linux using SetBufferContents 
 -->
