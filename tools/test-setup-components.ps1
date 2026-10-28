[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $projectRoot ('artifacts\components-' + [Guid]::NewGuid().ToString('N'))
$encoding = New-Object System.Text.UTF8Encoding($false)
$compiler = Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
foreach ($folder in @('installer','dist\XDBurner','dist\XDSuite','XDBurner\Assets','XDSuite\Assets')) { New-Item -ItemType Directory -Path (Join-Path $fixtureRoot $folder) -Force | Out-Null }
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $fixtureRoot
foreach ($app in @('XDBurner','XDSuite')) { Copy-Item -LiteralPath (Join-Path $projectRoot ($app + '\Assets\' + $app + '.ico')) -Destination (Join-Path $fixtureRoot ($app + '\Assets')) }
$stub = Join-Path $fixtureRoot 'stub.cs'
[System.IO.File]::WriteAllText($stub,'class Fixture { static void Main(string[] args) { if (args.Length == 2 && args[0] == "--record-uninstall") System.IO.File.WriteAllText(args[1], "uninstalled"); } }',$encoding)
foreach ($app in @('XDBurner','XDSuite')) {
    & $csc /nologo /target:winexe ('/out:' + (Join-Path $fixtureRoot ('dist\' + $app + '\' + $app + '.exe'))) $stub
    if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
}
$ids = @{}
foreach ($app in @('XDBurner','XDSuite')) {
    $id = [Guid]::NewGuid().ToString().ToUpperInvariant()
    $ids[$app] = $id
    $source = [System.IO.File]::ReadAllText((Join-Path $projectRoot ('installer\' + $app + '.iss')))
    $originalId = if ($app -eq 'XDBurner') { 'B5AF424C-85D1-47D4-AF62-30A5A15C5094' } else { 'F80CC53F-43F1-4E46-B7F4-61435E75303D' }
    $source = $source.Replace($originalId,$id).Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest')
    $source = [regex]::Replace($source,'(?m)^AppMutex=.*$','AppMutex=Local\XDTest.' + $id)
    # Never create, replace or delete real product shortcuts in this isolated test.
    $source = [regex]::Replace($source,'(?ms)^\[(Icons|InstallDelete)\]\r?\n.*?(?=^\[)','')
    [System.IO.File]::WriteAllText((Join-Path $fixtureRoot ('installer\' + $app + '.iss')),$source,$encoding)
}
$paths = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'installer\UpgradePaths.iss')).Replace('{autopf}\{#ProgramFolder}',$fixtureRoot + '\installed-{#ProgramFolder}')
[System.IO.File]::WriteAllText((Join-Path $fixtureRoot 'installer\UpgradePaths.iss'),$paths,$encoding)
foreach ($app in @('XDBurner','XDSuite')) {
    & $compiler /Q (Join-Path $fixtureRoot ('installer\' + $app + '.iss'))
    if ($LASTEXITCODE -ne 0) { throw 'Production-script fixture compilation failed.' }
}
$suiteSetup = Join-Path $fixtureRoot 'dist\XD-Suite-Setup-0.4.0.exe'
$suiteDir = Join-Path $fixtureRoot 'installed-XDSuite'
$burnerDir = Join-Path $fixtureRoot 'installed-XDBurner'
function Run([string]$file,[string]$arguments) {
    $p = Start-Process -FilePath $file -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
    if ($p.ExitCode -ne 0) { throw "Fixture failed ($($p.ExitCode)): $file" }
}
function Check([bool]$value,[string]$message) { if (-not $value) { throw $message }; $checks.Add('PASS: ' + $message) }
$checks = New-Object 'System.Collections.Generic.List[string]'
Run $suiteSetup '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=english /COMPONENTS=suite /TASKS=""'
Check ((Test-Path -LiteralPath (Join-Path $suiteDir 'XDSuite.exe')) -and -not (Test-Path -LiteralPath (Join-Path $burnerDir 'XDBurner.exe'))) 'Deselecting the burner installs only the suite.'
Run $suiteSetup '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=german /COMPONENTS=suite,burner /TASKS=""'
Check (Test-Path -LiteralPath (Join-Path $burnerDir 'XDBurner.exe')) 'Selecting the burner runs the embedded standalone setup.'
foreach ($app in @('XDBurner','XDSuite')) {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + $ids[$app] + '}_is1'
    Check (Test-Path -LiteralPath $key) ($app + ' has its own installation registration.')
    Check ((Get-ItemProperty -LiteralPath $key).'Inno Setup: Language' -eq 'german') ($app + ' receives the chosen setup language.')
}
Run (Join-Path $suiteDir 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
Check (Test-Path -LiteralPath (Join-Path $burnerDir 'XDBurner.exe')) 'Removing the suite leaves the independently installed burner intact.'
Run (Join-Path $burnerDir 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
foreach ($app in @('XDBurner','XDSuite')) {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + $ids[$app] + '}_is1'
    Check (-not (Test-Path -LiteralPath $key)) ($app + ' test registration cleaned up.')
}
$checks.Add('NOTE: Production setup scripts and Pascal component code, isolated AppIds and stub binaries; elevation is disabled for this test only.')
$legacy = Join-Path $fixtureRoot 'profile\AppData\Local\Programs\XDSuite'
$uninstallMarker = Join-Path $fixtureRoot 'legacy-uninstaller-ran.txt'
$oldSetup = @"
[Setup]
AppId={{$($ids['XDSuite'])}
AppName=XD Suite isolated legacy test
AppVersion=0.1.0
DefaultDirName=$legacy
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=$fixtureRoot
OutputBaseFilename=legacy-setup
[Files]
Source: $fixtureRoot\dist\XDSuite\XDSuite.exe; DestDir: {app}; Flags: ignoreversion
[UninstallRun]
Filename: {app}\XDSuite.exe; Parameters: "--record-uninstall ""$uninstallMarker"""; Flags: runhidden
"@
$oldScript = Join-Path $fixtureRoot 'legacy.iss'
[System.IO.File]::WriteAllText($oldScript,$oldSetup,$encoding)
& $compiler /Q $oldScript
if ($LASTEXITCODE -ne 0) { throw 'Legacy fixture compilation failed.' }
Run (Join-Path $fixtureRoot 'legacy-setup.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
Run (Join-Path $legacy 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
Check (Test-Path -LiteralPath $uninstallMarker) 'Legacy uninstall detector is functional before migration.'
Remove-Item -LiteralPath $uninstallMarker
Run (Join-Path $fixtureRoot 'legacy-setup.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
[System.IO.File]::WriteAllText((Join-Path $legacy 'settings.json'),'{"preserve":true}')
[System.IO.File]::WriteAllText((Join-Path $legacy 'personal-file.txt'),'preserve this file')
$before = (Get-FileHash -LiteralPath (Join-Path $legacy 'settings.json')).Hash
Run $suiteSetup ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=english /COMPONENTS=suite /TASKS="" /DIR="' + $legacy + '"')
Check (Test-Path -LiteralPath (Join-Path $suiteDir 'XDSuite.exe')) 'Legacy AppData /DIR is redirected to the new default application directory.'
Check ((Get-FileHash -LiteralPath (Join-Path $suiteDir 'settings.json')).Hash -eq $before) 'Relocation retains existing settings byte for byte.'
Check ([System.IO.File]::ReadAllText((Join-Path $suiteDir 'personal-file.txt')) -eq 'preserve this file') 'Relocation retains unrelated user files.'
Check (-not (Test-Path -LiteralPath $uninstallMarker)) 'Relocation never invokes the old uninstaller.'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + $ids['XDSuite'] + '}_is1'
Check (((Get-ItemProperty -LiteralPath $key).InstallLocation).TrimEnd('\') -eq $suiteDir) 'Installation registration points to the new directory.'
Run (Join-Path $suiteDir 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
$checks.Add('Fixture: ' + $fixtureRoot)
[System.IO.File]::WriteAllLines((Join-Path $projectRoot 'artifacts\setup-components-test.txt'),$checks,$encoding)
$checks
