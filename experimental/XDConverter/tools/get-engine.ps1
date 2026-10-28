[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$previewRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent $previewRoot)
$lock = [System.IO.File]::ReadAllText((Join-Path $previewRoot 'engine.lock.json')) | ConvertFrom-Json
if ($lock.sha256 -notmatch '^[a-f0-9]{64}$' -or $lock.url -notlike 'https://github.com/BtbN/FFmpeg-Builds/releases/download/*') { throw 'Invalid engine lock file.' }
$root = Join-Path $projectRoot ('.tools\converter-engine\' + $lock.sha256.Substring(0,12))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$archive = Join-Path $root 'engine.zip'
if (-not (Test-Path -LiteralPath $archive)) {
    Write-Host 'Downloading the pinned FFmpeg preview engine...'
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -UseBasicParsing -Uri $lock.url -OutFile ($archive + '.partial')
    if ((Get-FileHash -LiteralPath ($archive + '.partial')).Hash -ne $lock.sha256) { throw 'Engine checksum mismatch. Download will not be used.' }
    Move-Item -LiteralPath ($archive + '.partial') -Destination $archive
}
if ((Get-FileHash -LiteralPath $archive).Hash -ne $lock.sha256) { throw 'Cached engine checksum mismatch.' }
$unpacked = Join-Path $root 'unpacked'
if (-not (Test-Path -LiteralPath (Join-Path $unpacked 'verified.txt'))) {
    Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force
    [System.IO.File]::WriteAllText((Join-Path $unpacked 'verified.txt'),$lock.sha256)
}
$binary = Get-ChildItem -LiteralPath $unpacked -Filter ffmpeg.exe -File -Recurse | Select-Object -First 1
if (-not $binary -or -not (Test-Path -LiteralPath (Join-Path $binary.DirectoryName 'ffprobe.exe'))) { throw 'Engine tools missing from verified archive.' }
$binary.Directory.Parent.FullName
