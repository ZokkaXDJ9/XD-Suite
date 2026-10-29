[CmdletBinding()]
param([string]$RuntimeVersion = '8.0.33', [string]$Repository = 'ZokkaXDJ9/XD-Suite')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$version = ([xml][IO.File]::ReadAllText((Join-Path $projectRoot 'XDSuite\XDSuite.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ }
Push-Location $projectRoot
function Publish-App([string]$project, [string]$name, [scriptblock]$prepare = {}) {
    $output = Join-Path $projectRoot ('dist\' + $name)
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $output '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=embedded' "-p:RuntimeFrameworkVersion=$RuntimeVersion"
    if ($LASTEXITCODE -ne 0) { throw "Release build failed: $name" }
    Copy-Item -LiteralPath 'LICENSE','README.md' -Destination $output -Force
    Copy-Item -LiteralPath 'licenses' -Destination $output -Recurse -Force
    & $prepare $output
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
    $catalog = [IO.File]::ReadAllText((Join-Path $projectRoot 'catalog\catalog.json')) | ConvertFrom-Json
    $burner = $catalog.apps | Where-Object id -eq 'xd-burner'
    $burnerVersion = ([xml][IO.File]::ReadAllText((Join-Path $projectRoot 'XDBurner\XDBurner.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ }
    if ($burner.version -ne $burnerVersion) { throw 'A burner version change requires a deliberate burner release. This build reuses its already released setup.' }
    $burnerFilename = "XD-Brenner-Setup-$burnerVersion.exe"
    $burnerSetup = Join-Path $projectRoot ('dist\' + $burnerFilename)
    if (-not (Test-Path -LiteralPath $burnerSetup) -or (Get-FileHash -LiteralPath $burnerSetup).Hash -ne $burner.installerSha256) {
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -UseBasicParsing -Uri $burner.installerUrl -OutFile ($burnerSetup + '.partial')
        if ((Get-FileHash -LiteralPath ($burnerSetup + '.partial')).Hash -ne $burner.installerSha256 -or (Get-Item -LiteralPath ($burnerSetup + '.partial')).Length -ne $burner.installerSizeBytes) { throw 'Released burner setup verification failed.' }
        Move-Item -LiteralPath ($burnerSetup + '.partial') -Destination $burnerSetup -Force
    }
    if ((Get-Item -LiteralPath $burnerSetup).Length -ne $burner.installerSizeBytes) { throw 'Released burner setup size mismatch.' }
    Write-Host "Reusing unchanged XD Burner $burnerVersion with its original SHA-256."
    $converter = $catalog.apps | Where-Object id -eq 'xd-converter'
    $converterVersion = ([xml][IO.File]::ReadAllText((Join-Path $projectRoot 'XDConverter\XDConverter.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ }
    if ([version]$converterVersion -gt [version]$version) { throw 'Converter version exceeds the suite working version.' }
    if ($converterVersion -eq $version) {
    & (Join-Path $projectRoot 'XDConverter\tools\build-icon.ps1')
    $engineRoot = & (Join-Path $projectRoot 'XDConverter\tools\get-engine.ps1')
    Publish-App '.\XDConverter\XDConverter.csproj' 'XDConverter' {
        param($output)
        New-Item -ItemType Directory -Path (Join-Path $output 'engine') -Force | Out-Null
        Copy-Item -Path (Join-Path $engineRoot 'bin\*') -Destination (Join-Path $output 'engine') -Force
        Copy-Item -LiteralPath (Join-Path $engineRoot 'LICENSE.txt') -Destination (Join-Path $output 'engine\LICENSE.txt') -Force
        Copy-Item -LiteralPath (Join-Path $engineRoot 'doc') -Destination (Join-Path $output 'engine') -Recurse -Force
        Copy-Item -LiteralPath '.\XDConverter\engine.lock.json' -Destination $output -Force
    }
    $converterFilename = "XD-Konverter-Setup-$converterVersion.exe"
    Build-Setup '.\installer\XDConverter.iss' $converterFilename
    $converterSetup = Get-Item -LiteralPath (Join-Path $projectRoot ('dist\' + $converterFilename))
    $converter.version = $converterVersion
    $converter.installerUrl = "https://github.com/$Repository/releases/download/v$version/$converterFilename"
    $converter.installerSha256 = (Get-FileHash -LiteralPath $converterSetup.FullName).Hash.ToLowerInvariant()
    $converter.installerSizeBytes = $converterSetup.Length
    }
    else {
        if ($converter.version -ne $converterVersion) { throw 'Unchanged converter must retain its catalog version.' }
        $converterFilename = "XD-Konverter-Setup-$converterVersion.exe"
        $converterPath = Join-Path $projectRoot ('dist\' + $converterFilename)
        if (-not (Test-Path -LiteralPath $converterPath) -or (Get-FileHash -LiteralPath $converterPath).Hash -ne $converter.installerSha256) {
            Invoke-WebRequest -UseBasicParsing -Uri $converter.installerUrl -OutFile ($converterPath + '.partial')
            if ((Get-FileHash -LiteralPath ($converterPath + '.partial')).Hash -ne $converter.installerSha256 -or (Get-Item -LiteralPath ($converterPath + '.partial')).Length -ne $converter.installerSizeBytes) { throw 'Released converter setup verification failed.' }
            Move-Item -LiteralPath ($converterPath + '.partial') -Destination $converterPath -Force
        }
        if ((Get-Item -LiteralPath $converterPath).Length -ne $converter.installerSizeBytes) { throw 'Released converter setup size mismatch.' }
        Write-Host "Reusing unchanged XD Converter $converterVersion with its original SHA-256."
    }
    $json = $catalog | ConvertTo-Json -Depth 12
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $projectRoot 'catalog\catalog.json'), $json, $encoding)
    [System.IO.File]::WriteAllText((Join-Path $projectRoot 'dist\catalog.json'), $json, $encoding)
    Publish-App '.\XDSuite\XDSuite.csproj' 'XDSuite'
    & dotnet build '.\XD.UpdateHelper\XD.UpdateHelper.csproj' -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Update helper build failed.' }
    Copy-Item -LiteralPath '.\XD.UpdateHelper\bin\Release\net472\XDUpdateHelper.exe','.\XD.UpdateHelper\bin\Release\net472\XDUpdateHelper.exe.config' -Destination '.\dist\XDSuite' -Force
    Build-Setup '.\installer\XDSuite.iss' "XD-Suite-Setup-$version.exe"
    $sums = foreach ($file in @($burnerFilename,$converterFilename,"XD-Suite-Setup-$version.exe",'catalog.json')) { (Get-FileHash -LiteralPath (Join-Path $projectRoot ('dist\' + $file)) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $file }
    [System.IO.File]::WriteAllLines((Join-Path $projectRoot 'dist\SHA256SUMS.txt'), $sums)
    Write-Host 'Fertig: XD Suite und XD Konverter Setups; unverändertes Brenner-Setup wiederverwendet.'
}
finally { Pop-Location }
