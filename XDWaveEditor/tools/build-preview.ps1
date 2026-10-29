[CmdletBinding()]
param([string]$ToolsRoot, [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$appSource = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent $appSource
if (-not $ToolsRoot) {
    $commonGit = (& git -C $projectRoot rev-parse --path-format=absolute --git-common-dir).Trim()
    if ($commonGit.StartsWith('/')) {
        $cygpath = Join-Path (Split-Path -Parent (Get-Command git).Source) 'cygpath.exe'
        if (Test-Path -LiteralPath $cygpath) { $commonGit = (& $cygpath -w $commonGit).Trim() }
    }
    $ToolsRoot = Join-Path (Split-Path -Parent $commonGit) '.tools'
}
$outputRoot = Join-Path $projectRoot 'artifacts\wave-editor-preview-test2'
$appRoot = Join-Path $outputRoot 'app'
$production = @('XDSuite','XDBurner','XDConverter','XD.Common','XD.UpdateHelper','catalog','installer','build-release.ps1','tools\publish-release.ps1')
function Snapshot {
    $result = @{}
    foreach ($relative in $production) {
        $path = Join-Path $projectRoot $relative
        $files = if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Item -LiteralPath $path } else { Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object FullName -NotMatch '\\(bin|obj)\\' }
        foreach ($file in $files) { $result[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
    }
    $result
}
$before = Snapshot
$lock = [IO.File]::ReadAllText((Join-Path $appSource 'engine.lock.json')) | ConvertFrom-Json
if ($lock.sha256 -notmatch '^[a-f0-9]{64}$' -or $lock.url -notlike 'https://github.com/BtbN/FFmpeg-Builds/releases/download/*') { throw 'Invalid engine lock.' }
$archive = Join-Path $ToolsRoot ('converter-engine\' + $lock.sha256.Substring(0,12) + '\engine.zip')
if (-not (Test-Path -LiteralPath $archive)) { throw 'Pinned converter engine archive not found. Supply -ToolsRoot pointing to the existing .tools directory.' }
if ((Get-FileHash -LiteralPath $archive).Hash -ne $lock.sha256) { throw 'Engine archive checksum mismatch.' }
$engineRoot = Join-Path $projectRoot ('.tools\wave-engine\' + $lock.sha256.Substring(0,12))
if (-not (Test-Path -LiteralPath (Join-Path $engineRoot 'verified.txt'))) {
    Expand-Archive -LiteralPath $archive -DestinationPath $engineRoot -Force
    [IO.File]::WriteAllText((Join-Path $engineRoot 'verified.txt'),$lock.sha256)
}
$binary = Get-ChildItem -LiteralPath $engineRoot -Filter ffmpeg.exe -File -Recurse | Select-Object -First 1
if (-not $binary) { throw 'Engine binary missing.' }
$payload = $binary.Directory.Parent.FullName
& (Join-Path $PSScriptRoot 'build-icon.ps1')
New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
& dotnet publish (Join-Path $appSource 'XDWaveEditor.csproj') -c Release -r win-x64 --self-contained true -o $appRoot '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=embedded' '-p:RuntimeFrameworkVersion=8.0.33'
if ($LASTEXITCODE -ne 0) { throw 'Wave editor build failed.' }
New-Item -ItemType Directory -Path (Join-Path $appRoot 'engine'),(Join-Path $appRoot 'licenses') -Force | Out-Null
Copy-Item -Path (Join-Path $payload 'bin\*') -Destination (Join-Path $appRoot 'engine') -Force
Copy-Item -LiteralPath (Join-Path $payload 'LICENSE.txt') -Destination (Join-Path $appRoot 'engine\LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $payload 'doc') -Destination (Join-Path $appRoot 'engine') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $appSource 'engine.lock.json'),(Join-Path $appSource 'README.md'),(Join-Path $projectRoot 'LICENSE') -Destination $appRoot -Force
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'licenses') -File | Where-Object Name -Match '^(dotnet|wpf)' | Copy-Item -Destination (Join-Path $appRoot 'licenses') -Force
$exe = Join-Path $appRoot 'XDWaveEditor.Preview.exe'
$suiteVersion = ([xml][IO.File]::ReadAllText((Join-Path $projectRoot 'XDSuite\XDSuite.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($version.FileVersion -ne ($suiteVersion + '.0') -or $version.ProductVersion -ne ($suiteVersion + '-preview+wave-editor.2')) { throw 'Preview version differs from suite working version.' }
$report = Join-Path $outputRoot 'wave-editor-tests.txt'
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test','--report',('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
if ($test.ExitCode -ne 0) { throw "Wave editor tests failed: $report" }
if (-not $SkipInstaller) {
    & (Join-Path $ToolsRoot 'InnoSetup\ISCC.exe') /Q (Join-Path $appSource 'installer\Preview.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Preview installer compilation failed.' }
}
$after = Snapshot
if ($before.Count -ne $after.Count) { throw 'Production source tree changed.' }
foreach ($key in $before.Keys) { if ($after[$key] -ne $before[$key]) { throw "Production source changed: $key" } }
[IO.File]::WriteAllText((Join-Path $outputRoot 'isolation-check.txt'),'PASS: Suite, Burner, Converter, shared code, update helper, live catalog and production installers remain byte-identical. No live app was built or installed. No release or catalog publication.')
Write-Host "Local preview ready: $exe"
