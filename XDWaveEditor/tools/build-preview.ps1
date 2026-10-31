[CmdletBinding()]
param([string]$ToolsRoot,[switch]$SkipInstaller)
& (Join-Path $PSScriptRoot 'build-beta.ps1') @PSBoundParameters
