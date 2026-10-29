[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$productionRoot = Join-Path $projectRoot 'XDConverter'
$testGuid = [Guid]::NewGuid().ToString().ToUpperInvariant()
$id = '{' + $testGuid + '}_is1'
$testName = 'XD Converter Production Test ' + $testGuid.Substring(0,8)
$root = Join-Path $projectRoot ('artifacts\converter-production\installer-test-' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $root 'installed'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$script = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'installer\XDConverter.iss'))
$script = [regex]::Replace($script,'(?m)^#define Root.*$','#define Root "' + $projectRoot + '\"')
$script = $script.Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest')
$script = $script.Replace('3D477D64-7020-48F0-A621-66CF2958722B',$testGuid).Replace('AppMutex=Local\XDConverter.App','AppMutex=Local\XDConverter.Production.Test.' + $testGuid)
$script = $script.Replace('english.ConverterName=XD Converter','english.ConverterName=' + $testName).Replace('german.ConverterName=XD Konverter','german.ConverterName=' + $testName)
$script = [regex]::Replace($script,'(?m)^OutputDir=.*$','OutputDir=' + $root)
$script = [regex]::Replace($script,'(?m)^OutputBaseFilename=.*$','OutputBaseFilename=test-setup')
$source = Join-Path $root 'test.iss'
[System.IO.File]::WriteAllText($source,$script,(New-Object System.Text.UTF8Encoding($false)))
& (Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe') /Q $source
if ($LASTEXITCODE -ne 0) { throw 'Test setup compilation failed.' }
$setup = Start-Process -FilePath (Join-Path $root 'test-setup.exe') -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=english /DIR="' + $target + '"') -PassThru -Wait -WindowStyle Hidden
if ($setup.ExitCode -ne 0) { throw "Production setup failed: $($setup.ExitCode)" }
$checks = New-Object 'System.Collections.Generic.List[string]'
try {
    $exe = Join-Path $target 'XDConverter.exe'
    if ((Get-FileHash -LiteralPath $exe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'dist\XDConverter\XDConverter.exe')).Hash) { throw 'Installed production differs from tested production.' }
    $report = Join-Path $root 'installed-app-tests.txt'
    $test = Start-Process -FilePath $exe -ArgumentList @('--self-test','--report',('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($test.ExitCode -ne 0) { throw "Installed application tests failed: $report" }
    $checks.Add('PASS: separate production installer installs the tested EXE and engine; all actual conversions and bilingual UI tests pass from the installed copy.')
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $id
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) ($testName + '.lnk')
    if (-not (Test-Path -LiteralPath $key) -or -not (Test-Path -LiteralPath $shortcut)) { throw 'Production registration or English shortcut missing.' }
    $checks.Add('PASS: independent production registration and language-specific Start menu shortcut.')
    $link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
    if ($link.TargetPath -ne $exe -or $link.IconLocation -notin @(($exe + ',0'),($exe + ', 0'))) { throw ('Unexpected shortcut: target=' + $link.TargetPath + '; icon=' + $link.IconLocation) }
    $checks.Add('PASS: Start menu shortcut explicitly uses the installed converter EXE icon at index zero.')
    [System.IO.File]::WriteAllText((Join-Path $target 'personal-file.txt'),'retain')
}
finally {
    $uninstaller = [System.IO.Path]::GetFullPath((Join-Path $target 'unins000.exe'))
    if (-not $uninstaller.StartsWith([System.IO.Path]::GetFullPath($root) + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    $cleanup = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru -Wait -WindowStyle Hidden
    if ($cleanup.ExitCode -ne 0) { throw 'Production test cleanup failed.' }
}
if ((Test-Path -LiteralPath $key) -or [System.IO.File]::ReadAllText((Join-Path $target 'personal-file.txt')) -ne 'retain') { throw 'Production uninstall registration or data retention failed.' }
$checks.Add('PASS: production test installation removed; unrelated personal file retained.')
$checks.Add('NOTE: actual local production payload and setup script; isolated AppId and shortcut name, with elevation disabled for this test only. Existing Production, Suite and Burner installations are not changed.')
[System.IO.File]::WriteAllLines((Join-Path $projectRoot 'artifacts\converter-installer-test.txt'),$checks)
$checks
