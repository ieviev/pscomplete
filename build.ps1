#!/usr/bin/env pwsh

# debug
# dotnet build src -c Debug
# Copy-Item "$PSScriptRoot/src/bin/Debug/net8.0/pscomplete.dll" "./PsComplete/pscomplete.dll" -Force

# release
dotnet build src -c Release
Copy-Item "$PSScriptRoot/src/bin/Release/net8.0/pscomplete.dll" "./PsComplete/pscomplete.dll" -Force

Import-Module $PSScriptRoot\PsComplete\PsComplete.psm1
