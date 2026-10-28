[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $projectRoot ('artifacts\update-fixture-' + [Guid]::NewGuid().ToString('N'))
$compiler = Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$helper = Join-Path $projectRoot 'XD.UpdateHelper\bin\Release\net472\XDUpdateHelper.exe'
foreach ($file in @($compiler,$csc,$helper)) { if (-not (Test-Path -LiteralPath $file)) { throw "Missing test prerequisite: $file" } }
$encoding = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$target = Join-Path $fixtureRoot 'installed'
$testId = [Guid]::NewGuid().ToString().ToUpperInvariant()
$uninstallMarker = Join-Path $fixtureRoot 'uninstall-invoked.txt'
$checks = New-Object 'System.Collections.Generic.List[string]'
function Check([bool]$passed,[string]$message) { if (-not $passed) { throw $message }; $checks.Add('PASS: ' + $message) }
function Wait-File([string]$path,[int]$seconds = 20) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    while (-not (Test-Path -LiteralPath $path)) { if ([DateTime]::UtcNow -gt $deadline) { throw "Timed out: $path" }; Start-Sleep -Milliseconds 100 }
}
foreach ($version in @('1.0.0','2.0.0')) {
    $source = @'
using System;
using System.IO;
using System.Threading;
using System.Reflection;
[assembly: AssemblyVersion("VERSION.0")]
class Fixture {
  static void Main(string[] args) {
    string root = AppDomain.CurrentDomain.BaseDirectory;
    if (args.Length > 0 && args[0] == "--record-uninstall") { File.WriteAllText(args[1], "uninstaller ran"); return; }
    if (args.Length > 0 && args[0] == "--wait") {
      File.WriteAllText(Path.Combine(root,"ready.txt"),"VERSION");
      while (!File.Exists(Path.Combine(root,"exit-parent.txt"))) Thread.Sleep(100);
      return;
    }
    File.WriteAllText(Path.Combine(root,"restarted.txt"),"VERSION|" + String.Join(" ",args));
  }
}
'@
    $sourcePath = Join-Path $fixtureRoot ('app-' + $version + '.cs')
    [System.IO.File]::WriteAllText($sourcePath,$source.Replace('VERSION',$version),$encoding)
    & $csc /nologo /target:winexe ('/out:' + (Join-Path $fixtureRoot ('app-' + $version + '.exe'))) $sourcePath
    if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
    $setup = @"
[Setup]
AppId={{$testId}
AppName=XD Suite isolated update test
AppVersion=$version
DefaultDirName=$target
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
UninstallDisplayName=XD Suite isolated update test $testId
OutputDir=$fixtureRoot
OutputBaseFilename=setup-$version
SetupLogging=yes
CloseApplications=no
RestartApplications=no
[Languages]
Name: english; MessagesFile: compiler:Default.isl
[Types]
Name: full; Description: Full
[Components]
Name: suite; Description: Suite; Types: full; Flags: fixed
[Files]
Source: $fixtureRoot\app-$version.exe; DestDir: {app}; DestName: XDSuite.exe; Flags: ignoreversion
[UninstallRun]
Filename: {app}\XDSuite.exe; Parameters: "--record-uninstall ""$uninstallMarker"""; Flags: runhidden
"@
    $script = Join-Path $fixtureRoot ('setup-' + $version + '.iss')
    [System.IO.File]::WriteAllText($script,$setup,$encoding)
    & $compiler /Q $script
    if ($LASTEXITCODE -ne 0) { throw 'Fixture setup compilation failed.' }
}
$initial = Start-Process -FilePath (Join-Path $fixtureRoot 'setup-1.0.0.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -PassThru -Wait -WindowStyle Hidden
Check ($initial.ExitCode -eq 0) 'Initial fixture installed with a separate test AppId.'
$settings = Join-Path $target 'user-settings.json'
$tool = Join-Path $fixtureRoot 'independent-tool.txt'
[System.IO.File]::WriteAllText($settings,'{"language":"de","retained":true}')
[System.IO.File]::WriteAllText($tool,'independently installed tool')
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$toolHash = (Get-FileHash -LiteralPath $tool).Hash
$parent = Start-Process -FilePath (Join-Path $target 'XDSuite.exe') -ArgumentList '--wait' -PassThru -WindowStyle Hidden
Wait-File (Join-Path $target 'ready.txt')
$updateSetup = Join-Path $fixtureRoot 'setup-2.0.0.exe'
$hash = (Get-FileHash -LiteralPath $updateSetup).Hash
$arguments = '--parent ' + $parent.Id + ' --setup "' + $updateSetup + '" --target "' + $target + '" --sha256 ' + $hash + ' --language english'
$update = Start-Process -FilePath $helper -ArgumentList $arguments -PassThru -WindowStyle Hidden
Start-Sleep -Milliseconds 500
Check (-not $update.HasExited) 'Updater waits for the running app to close.'
[System.IO.File]::WriteAllText((Join-Path $target 'exit-parent.txt'),'exit')
if (-not $update.WaitForExit(45000)) { throw 'Updater did not finish within 45 seconds. Inspect the fixture and helper log.' }
Check ($update.ExitCode -eq 0) 'Actual update helper completed successfully.'
Wait-File (Join-Path $target 'restarted.txt')
Check (([System.IO.File]::ReadAllText((Join-Path $target 'restarted.txt'))) -eq '2.0.0|--update-completed') 'Updated version restarted from the same installation folder.'
Check ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'Existing settings retained byte for byte.'
Check ((Get-FileHash -LiteralPath $tool).Hash -eq $toolHash) 'Independent tool retained byte for byte.'
Check (-not (Test-Path -LiteralPath $uninstallMarker)) 'No uninstaller was invoked during the update.'
Check ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $target 'XDSuite.exe')).FileVersion -eq '2.0.0.0') 'Version 2 replaced version 1 in place.'
# Deliberate cleanup of this test registration only, after proving the updater did not uninstall.
$uninstaller = Join-Path $target 'unins000.exe'
$resolved = [System.IO.Path]::GetFullPath($uninstaller)
if (-not $resolved.StartsWith($fixtureRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup path.' }
$cleanup = Start-Process -FilePath $resolved -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru -Wait -WindowStyle Hidden
Check ($cleanup.ExitCode -eq 0) 'Isolated test installation cleaned up.'
Check (Test-Path -LiteralPath $uninstallMarker) 'Uninstall detector is functional: it fires only during deliberate test cleanup.'
$checks.Add('Fixture: ' + $fixtureRoot)
$report = Join-Path $projectRoot 'artifacts\suite-update-test.txt'
[System.IO.File]::WriteAllLines($report,$checks,$encoding)
$checks
