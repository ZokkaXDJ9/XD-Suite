[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets\XDConverter.ico'))) { throw 'Application icon missing.' }
