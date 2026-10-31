[CmdletBinding()]
param()
& (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'design\build-beta-icon.ps1') -AppSource (Split-Path -Parent $PSScriptRoot) -Artwork (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets\App.svg') -SymbolColor '#5947AD'
