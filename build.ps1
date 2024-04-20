#!/usr/bin/env pwsh
dotnet build src -c Release
Copy-Item "$PSScriptRoot/src/bin/Release/net8.0/pscomplete.dll" "./PsComplete/pscomplete.dll" -Force
Import-Module $PSScriptRoot\PsComplete\PsComplete.psd1
