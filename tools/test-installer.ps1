[CmdletBinding()]
param([switch]$SkipLegacyUpgrade)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run installer integration tests from an elevated PowerShell.' }
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + '\'
$suiteDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'Installed XD Suite'))
$burnerDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'Installed XD Burner'))
$legacyDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot 'Legacy Upgrade'))
$burnerKey = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B5AF424C-85D1-47D4-AF62-30A5A15C5094}_is1'
$suiteKey = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{F80CC53F-43F1-4E46-B7F4-61435E75303D}_is1'
foreach ($path in @($suiteDirectory,$burnerDirectory,$legacyDirectory)) { if (-not $path.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $path)) { throw "Unexpected or existing test directory: $path" } }
foreach ($key in @($burnerKey,$suiteKey,$burnerKey.Replace('HKLM:','HKCU:'),$suiteKey.Replace('HKLM:','HKCU:'))) { if (Test-Path -LiteralPath $key) { throw 'An installation already exists. Refusing to overwrite it for testing.' } }
$report = [System.Collections.Generic.List[string]]::new()
$report.Add('XD Suite / XD Burner installation round-trip')
$report.Add([DateTimeOffset]::Now.ToString('O'))
$suiteSetup = Join-Path $projectRoot 'dist\XD-Suite-Setup-0.5.0.exe'
$burnerSetup = Join-Path $projectRoot 'dist\XD-Brenner-Setup-0.4.1.exe'
$menuRoot = [Environment]::GetFolderPath('CommonPrograms')
function Run-Process([string]$file,[string[]]$arguments) {
    $p = Start-Process -FilePath $file -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
    if ($p.ExitCode -ne 0) { throw "Process failed ($($p.ExitCode)): $file" }
}
function Uninstall([string]$directory) { Run-Process (Join-Path $directory 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') }
function Mark-Directory([string]$directory) { [System.IO.File]::WriteAllText((Join-Path $directory 'unrelated-user-file.txt'),'Must survive uninstall.') }
function Clean-Marker([string]$directory) {
    if (-not $directory.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup path outside artifacts.' }
    $marker = Join-Path $directory 'unrelated-user-file.txt'
    if (-not (Test-Path -LiteralPath $marker)) { throw 'Uninstaller removed an unrelated file.' }
    Remove-Item -LiteralPath $marker
    [System.IO.Directory]::Delete($directory,$false)
}
try {
    Run-Process $suiteSetup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/COMPONENTS=suite','/LANG=english','/TASKS=""',('/DIR="' + $suiteDirectory + '"'))
    $suiteExe = Join-Path $suiteDirectory 'XDSuite.exe'
    if (-not (Test-Path -LiteralPath $suiteKey) -or -not (Test-Path -LiteralPath (Join-Path $menuRoot 'XD Suite.lnk'))) { throw 'Suite registration or shortcut missing.' }
    Run-Process $suiteExe @('--self-test','--report',('"' + (Join-Path $artifactRoot 'installed-suite-tests.txt') + '"'))
    $report.Add('PASS: independent, offline, administrator suite installation and tests from installed application')
    Run-Process $suiteExe @('--test-install',('"' + $burnerDirectory + '"'),'--setup',('"' + $burnerSetup + '"'),'--report',('"' + (Join-Path $artifactRoot 'suite-real-install.txt') + '"'),'--english')
    $burnerExe = Join-Path $burnerDirectory 'XDBurner.exe'
    if ((Get-FileHash -LiteralPath $burnerExe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'dist\XDBurner\XDBurner.exe')).Hash) { throw 'Installed burner differs from release.' }
    if (-not (Test-Path -LiteralPath $burnerKey) -or -not (Test-Path -LiteralPath (Join-Path $menuRoot 'XD Burner.lnk'))) { throw 'Burner registration or English shortcut missing.' }
    Run-Process $burnerExe @('--self-test','--report',('"' + (Join-Path $artifactRoot 'installed-burner-tests.txt') + '"'))
    Run-Process $suiteExe @('--test-launch','--report',('"' + (Join-Path $artifactRoot 'suite-real-launch.txt') + '"'))
    $report.Add('PASS: actual burner setup verified and run by suite backend; custom install directory discovered; burner launched by suite')
    Mark-Directory $suiteDirectory
    Mark-Directory $burnerDirectory
    Uninstall $suiteDirectory
    if (Test-Path -LiteralPath $suiteKey) { throw 'Suite registration remains.' }
    if (-not (Test-Path -LiteralPath $burnerExe) -or -not (Test-Path -LiteralPath $burnerKey)) { throw 'Removing suite damaged standalone burner.' }
    $p = Start-Process -FilePath $burnerExe -ArgumentList '--english' -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2
    $p.Refresh()
    if ($p.HasExited -or -not $p.Responding -or -not $p.CloseMainWindow() -or -not $p.WaitForExit(10000)) { throw 'Burner does not run independently after suite removal.' }
    $report.Add('PASS: uninstalling suite leaves burner installed; burner starts and closes independently without suite')
    Uninstall $burnerDirectory
    if ((Test-Path -LiteralPath $burnerKey) -or (Test-Path -LiteralPath (Join-Path $menuRoot 'XD Burner.lnk'))) { throw 'Burner uninstall left registration or shortcut.' }
    Clean-Marker $suiteDirectory
    Clean-Marker $burnerDirectory
    $report.Add('PASS: separate uninstallers remove owned files and preserve unrelated user files')

    $report.Add('NOTE: AppData relocation is covered by tools/test-setup-components.ps1 with isolated installation identities.')
    $report.Add('ALL INSTALLER CHECKS PASSED')
}
catch { $report.Add('FAIL: ' + $_.Exception.Message); throw }
finally { [System.IO.File]::WriteAllLines((Join-Path $artifactRoot 'installer-test-02.txt'),$report); $report | Write-Output }
