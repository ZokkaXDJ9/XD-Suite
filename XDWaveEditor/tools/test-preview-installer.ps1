[CmdletBinding()]
param([string]$ToolsRoot)
& (Join-Path $PSScriptRoot 'test-beta-installer.ps1') @PSBoundParameters
