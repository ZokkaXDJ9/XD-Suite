[CmdletBinding()]
param([string]$RuntimeVersion = '8.0.33', [string]$Repository = 'ZokkaXDJ9/XD-Suite')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$version = '0.3.1'
Push-Location $projectRoot
function Publish-App([string]$project, [string]$name) {
    $output = Join-Path $projectRoot ('dist\' + $name)
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $output '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=embedded' "-p:RuntimeFrameworkVersion=$RuntimeVersion"
    if ($LASTEXITCODE -ne 0) { throw "Release build failed: $name" }
    Copy-Item -LiteralPath 'LICENSE','README.md' -Destination $output -Force
    Copy-Item -LiteralPath 'licenses' -Destination $output -Recurse -Force
    $report = Join-Path $projectRoot ('artifacts\' + $name + '-release-tests.txt')
    $test = Start-Process -FilePath (Join-Path $output ($name + '.exe')) -ArgumentList @('--self-test','--report',('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($test.ExitCode -ne 0) { throw "Integration tests failed: $report" }
}
function Build-Setup([string]$script, [string]$filename) {
    $stage = Join-Path $projectRoot ('artifacts\setup-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    & $compiler '/Q' ('/O' + $stage) $script
    if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed: $script" }
    Move-Item -LiteralPath (Join-Path $stage $filename) -Destination (Join-Path $projectRoot ('dist\' + $filename)) -Force
    [System.IO.Directory]::Delete($stage, $false)
}
try {
    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid GitHub repository.' }
    New-Item -ItemType Directory -Path 'artifacts','dist' -Force | Out-Null
    $compiler = $env:ISCC_PATH
    if (-not $compiler) { $compiler = Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe' }
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler missing. Run tools\get-installer-compiler.ps1 or set ISCC_PATH.' }
    Publish-App '.\XDBurner\XDBurner.csproj' 'XDBurner'
    $burnerFilename = "XD-Brenner-Setup-$version.exe"
    Build-Setup '.\installer\XDBurner.iss' $burnerFilename
    $burnerSetup = Get-Item -LiteralPath (Join-Path $projectRoot ('dist\' + $burnerFilename))
    $catalog = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'catalog\catalog.json')) | ConvertFrom-Json
    $burner = $catalog.apps | Where-Object id -eq 'xd-burner'
    $burner.version = $version
    $burner.installerUrl = "https://github.com/$Repository/releases/download/v$version/$burnerFilename"
    $burner.installerSha256 = (Get-FileHash -LiteralPath $burnerSetup.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $burner.installerSizeBytes = $burnerSetup.Length
    $json = $catalog | ConvertTo-Json -Depth 12
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $projectRoot 'catalog\catalog.json'), $json, $encoding)
    [System.IO.File]::WriteAllText((Join-Path $projectRoot 'dist\catalog.json'), $json, $encoding)
    Publish-App '.\XDSuite\XDSuite.csproj' 'XDSuite'
    & dotnet build '.\XD.UpdateHelper\XD.UpdateHelper.csproj' -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Update helper build failed.' }
    Copy-Item -LiteralPath '.\XD.UpdateHelper\bin\Release\net472\XDUpdateHelper.exe','.\XD.UpdateHelper\bin\Release\net472\XDUpdateHelper.exe.config' -Destination '.\dist\XDSuite' -Force
    Build-Setup '.\installer\XDSuite.iss' "XD-Suite-Setup-$version.exe"
    $sums = foreach ($file in @($burnerFilename,"XD-Suite-Setup-$version.exe",'catalog.json')) { (Get-FileHash -LiteralPath (Join-Path $projectRoot ('dist\' + $file)) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $file }
    [System.IO.File]::WriteAllLines((Join-Path $projectRoot 'dist\SHA256SUMS.txt'), $sums)
    Write-Host 'Fertig: XD Brenner und XD Suite Setups.'
}
finally { Pop-Location }
