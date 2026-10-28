[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$previewRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent $previewRoot)
$id = '{D22B76B3-3CED-4977-9E12-86CA4C4D237A}_is1'
foreach ($hive in @('HKCU','HKLM')) {
    if (Test-Path -LiteralPath ($hive + ':\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $id)) { throw 'An existing preview installation must not be replaced by this test.' }
}
$root = Join-Path $projectRoot ('artifacts\converter-preview\installer-test-' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $root 'installed'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$script = [System.IO.File]::ReadAllText((Join-Path $previewRoot 'installer\Preview.iss'))
$script = [regex]::Replace($script,'(?m)^#define Root.*$','#define Root "' + $projectRoot + '\"')
$script = $script.Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest')
$script = [regex]::Replace($script,'(?m)^OutputDir=.*$','OutputDir=' + $root)
$script = [regex]::Replace($script,'(?m)^OutputBaseFilename=.*$','OutputBaseFilename=test-setup')
$source = Join-Path $root 'test.iss'
[System.IO.File]::WriteAllText($source,$script,(New-Object System.Text.UTF8Encoding($false)))
& (Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe') /Q $source
if ($LASTEXITCODE -ne 0) { throw 'Test setup compilation failed.' }
$setup = Start-Process -FilePath (Join-Path $root 'test-setup.exe') -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=english /DIR="' + $target + '"') -PassThru -Wait -WindowStyle Hidden
if ($setup.ExitCode -ne 0) { throw "Preview setup failed: $($setup.ExitCode)" }
$checks = New-Object 'System.Collections.Generic.List[string]'
try {
    $exe = Join-Path $target 'XDConverter.Preview.exe'
    if ((Get-FileHash -LiteralPath $exe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'artifacts\converter-preview\app\XDConverter.Preview.exe')).Hash) { throw 'Installed preview differs from tested preview.' }
    $report = Join-Path $root 'installed-app-tests.txt'
    $test = Start-Process -FilePath $exe -ArgumentList @('--self-test','--report',('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($test.ExitCode -ne 0) { throw "Installed application tests failed: $report" }
    $checks.Add('PASS: separate preview installer installs the tested EXE and engine; all actual conversions and bilingual UI tests pass from the installed copy.')
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $id
    if (-not (Test-Path -LiteralPath $key) -or -not (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'XD Converter Preview.lnk'))) { throw 'Preview registration or English shortcut missing.' }
    $checks.Add('PASS: independent preview registration and language-specific Start menu shortcut.')
    [System.IO.File]::WriteAllText((Join-Path $target 'personal-file.txt'),'retain')
}
finally {
    $uninstaller = [System.IO.Path]::GetFullPath((Join-Path $target 'unins000.exe'))
    if (-not $uninstaller.StartsWith([System.IO.Path]::GetFullPath($root) + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    $cleanup = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru -Wait -WindowStyle Hidden
    if ($cleanup.ExitCode -ne 0) { throw 'Preview test cleanup failed.' }
}
if ((Test-Path -LiteralPath $key) -or [System.IO.File]::ReadAllText((Join-Path $target 'personal-file.txt')) -ne 'retain') { throw 'Preview uninstall registration or data retention failed.' }
$checks.Add('PASS: preview test installation removed; unrelated personal file retained.')
$checks.Add('NOTE: actual local preview payload and setup script; administrator elevation disabled only for this isolated test. Production Suite and Burner installers are never run.')
[System.IO.File]::WriteAllLines((Join-Path $projectRoot 'artifacts\converter-preview\installer-test.txt'),$checks)
$checks
