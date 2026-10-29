[CmdletBinding()]
param([switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$previewRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent $previewRoot
$artifactRoot = Join-Path $projectRoot 'artifacts\converter-preview'
$appRoot = Join-Path $artifactRoot 'app'
$sourcePaths = @('XDSuite','XDBurner','XD.Common','XD.UpdateHelper','catalog','installer','build-release.ps1','tools\publish-release.ps1')
function ProductionSnapshot {
    $result = @{}
    foreach ($relative in $sourcePaths) {
        $path = Join-Path $projectRoot $relative
        $files = if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Item -LiteralPath $path } else { Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object FullName -NotMatch '\\(bin|obj)\\' }
        foreach ($file in $files) { $result[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
    }
    $result
}
$before = ProductionSnapshot
& (Join-Path $PSScriptRoot 'build-icon.ps1')
New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
$engineRoot = & (Join-Path $PSScriptRoot 'get-engine.ps1')
if (-not (Test-Path -LiteralPath (Join-Path $engineRoot 'bin\ffmpeg.exe'))) { throw 'Verified engine missing.' }
& dotnet publish (Join-Path $previewRoot 'XDConverter.csproj') -c Release -r win-x64 --self-contained true -o $appRoot '-p:PreviewBuild=true' '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=embedded' '-p:RuntimeFrameworkVersion=8.0.33'
if ($LASTEXITCODE -ne 0) { throw 'Preview build failed.' }
New-Item -ItemType Directory -Path (Join-Path $appRoot 'engine') -Force | Out-Null
Copy-Item -Path (Join-Path $engineRoot 'bin\*') -Destination (Join-Path $appRoot 'engine') -Force
Copy-Item -LiteralPath (Join-Path $engineRoot 'LICENSE.txt') -Destination (Join-Path $appRoot 'engine\LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $engineRoot 'doc') -Destination (Join-Path $appRoot 'engine') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $previewRoot 'engine.lock.json'),(Join-Path $previewRoot 'README.md'),(Join-Path $projectRoot 'LICENSE') -Destination $appRoot -Force
New-Item -ItemType Directory -Path (Join-Path $appRoot 'licenses') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'licenses') -File | Where-Object Name -Match '^(dotnet|wpf)' | Copy-Item -Destination (Join-Path $appRoot 'licenses') -Force
$exe = Join-Path $appRoot 'XDConverter.Preview.exe'
$programVersion = ([xml][System.IO.File]::ReadAllText((Join-Path $projectRoot 'XDConverter\XDConverter.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ }
$appVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($appVersion.FileVersion -ne ($programVersion + '.0') -or -not $appVersion.ProductVersion.StartsWith($programVersion + '-preview+')) { throw 'Preview product version must match its declared application version; distinguish test builds through metadata only.' }
$report = Join-Path $artifactRoot 'converter-tests.txt'
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test','--report',('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
if ($test.ExitCode -ne 0) { throw "Preview integration tests failed: $report" }
if (-not $SkipInstaller) {
    $compiler = Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe'
    & $compiler /Q (Join-Path $previewRoot 'installer\Preview.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Preview setup compilation failed.' }
}
$after = ProductionSnapshot
if ($before.Count -ne $after.Count) { throw 'Production source tree changed during preview build.' }
foreach ($key in $before.Keys) { if ($after[$key] -ne $before[$key]) { throw "Production file changed: $key" } }
[System.IO.File]::WriteAllText((Join-Path $artifactRoot 'isolation-check.txt'),'PASS: live application sources, shared components, catalog, installers and release scripts remain byte-identical. No production app was built or installed.')
Write-Host "Preview ready: $exe"
