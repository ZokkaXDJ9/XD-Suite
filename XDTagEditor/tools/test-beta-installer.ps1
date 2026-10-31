[CmdletBinding()]
param([string]$ToolsRoot)
$ErrorActionPreference='Stop'
$source=Split-Path -Parent $PSScriptRoot
$root=Split-Path -Parent $source
$config=[IO.File]::ReadAllText((Join-Path $source 'beta.json')) | ConvertFrom-Json
if(-not $ToolsRoot){ throw 'Supply -ToolsRoot pointing to the suite .tools directory.' }
$testId=[Guid]::NewGuid().ToString().ToUpperInvariant()
$fixture=Join-Path $root ('artifacts\beta\installer-test-'+$testId)
$target=Join-Path $fixture 'installed'
New-Item -ItemType Directory -Force $fixture | Out-Null
$text=[IO.File]::ReadAllText((Join-Path $source 'installer\Beta.iss'))
$text=[regex]::Replace($text,'(?m)^#define Root.*$','#define Root "'+$root+'\"')
$text=$text.Replace($config.registryId.Substring(1,36),$testId).Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest')
$text=[regex]::Replace($text,'(?m)^OutputDir=.*$','OutputDir='+$fixture)
$text=[regex]::Replace($text,'(?m)^OutputBaseFilename=.*$','OutputBaseFilename=test-setup')
$text=[regex]::Replace($text,'(?m)^AppMutex=.*$','AppMutex=Local\XDBeta.Test.'+$testId)
$text=[regex]::Replace($text,'(?ms)^\[Icons\]\r?\n.*?(?=^\[)','')
$text=$text.Replace('#include "..\..\installer\UpgradePaths.iss"','#include "'+$root+'\installer\UpgradePaths.iss"')
[IO.File]::WriteAllText((Join-Path $fixture 'test.iss'),$text,(New-Object Text.UTF8Encoding($false)))
& (Join-Path $ToolsRoot 'InnoSetup\ISCC.exe') /Q (Join-Path $fixture 'test.iss')
if($LASTEXITCODE -ne 0){ throw 'Isolated beta setup compilation failed.' }
$setup=Start-Process (Join-Path $fixture 'test-setup.exe') -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=english /DIR="'+$target+'"') -Wait -PassThru -WindowStyle Hidden
if($setup.ExitCode -ne 0){ throw 'Isolated beta installation failed.' }
$key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{'+$testId+'}_is1'
$checks=New-Object 'Collections.Generic.List[string]'
try {
    $exe=Join-Path $target ($config.program+'.Beta.exe')
    if((Get-FileHash -LiteralPath $exe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $root ('artifacts\beta\app\'+$config.program+'.Beta.exe'))).Hash){ throw 'Installed beta differs from tested build.' }
    if((Get-ItemProperty -LiteralPath $key).DisplayVersion -ne '0.5.2-Beta'){ throw 'Installer must display the Beta suffix.' }
    $checks.Add('PASS: real beta payload installed with independent registration and visible 0.5.2-Beta version.')
    $engine=(Get-ChildItem -LiteralPath (Join-Path $root '.tools\beta-engine') -Filter ffmpeg.exe -File -Recurse | Select-Object -First 1)
    $engineArgument=if($config.program -eq 'XDWaveEditor'){$engine.DirectoryName}else{$engine.FullName}
    $report=Join-Path $fixture 'installed-tests.txt'
    $test=Start-Process $exe -ArgumentList @('--self-test','--engine',('"'+$engineArgument+'"'),'--report',('"'+$report+'"')) -Wait -PassThru -WindowStyle Hidden
    if($test.ExitCode -ne 0){ throw ('Installed beta tests failed: '+$report) }
    $checks.Add('PASS: full application checks pass from the installed copy.')
    [IO.File]::WriteAllText((Join-Path $target 'personal-file.txt'),'retain')
}
finally {
    $uninstaller=[IO.Path]::GetFullPath((Join-Path $target 'unins000.exe'))
    if(-not $uninstaller.StartsWith([IO.Path]::GetFullPath($fixture)+'\',[StringComparison]::OrdinalIgnoreCase)){ throw 'Unsafe isolated cleanup path.' }
    $remove=Start-Process $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru -WindowStyle Hidden
    if($remove.ExitCode -ne 0){ throw 'Isolated beta uninstall failed.' }
}
if((Test-Path -LiteralPath $key) -or [IO.File]::ReadAllText((Join-Path $target 'personal-file.txt')) -ne 'retain'){ throw 'Beta uninstall failed to retain personal data or remove its registration.' }
$checks.Add('PASS: independent uninstall removes its registration and retains unrelated user files. Production registrations, folders and shortcuts were not used.')
[IO.File]::WriteAllLines((Join-Path $root 'artifacts\beta\installer-test.txt'),$checks)
$checks
