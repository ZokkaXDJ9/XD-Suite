[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$id = [Guid]::NewGuid().ToString()
$fixtureRoot = Join-Path $projectRoot ('artifacts\uninstall-test-' + $id)
$target = Join-Path $fixtureRoot 'installed'
$encoding = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$stub = Join-Path $fixtureRoot 'stub.cs'
[IO.File]::WriteAllText($stub,'class Fixture { static void Main() {} }',$encoding)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe ('/out:' + (Join-Path $fixtureRoot 'UninstallFixture.exe')) $stub
if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
$script = @"
[Setup]
AppId={{$id}
AppName=XD Suite uninstall fixture
AppVersion=0.0.1
DefaultDirName=$target
PrivilegesRequired=lowest
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=$fixtureRoot
OutputBaseFilename=FixtureSetup
UninstallDisplayIcon={app}\UninstallFixture.exe
[Files]
Source: "$fixtureRoot\UninstallFixture.exe"; DestDir: "{app}"
"@
$iss = Join-Path $fixtureRoot 'fixture.iss'
[IO.File]::WriteAllText($iss,$script,$encoding)
& (Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe') /Q $iss
if ($LASTEXITCODE -ne 0) { throw 'Fixture installer compilation failed.' }
$install = Start-Process -FilePath (Join-Path $fixtureRoot 'FixtureSetup.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -PassThru -Wait -WindowStyle Hidden
if ($install.ExitCode -ne 0) { throw 'Fixture installation failed.' }
[IO.File]::WriteAllText((Join-Path $fixtureRoot 'keep.txt'),'Unrelated file must remain.',$encoding)
$report = Join-Path $projectRoot 'artifacts\suite-uninstall-test.txt'
$test = Start-Process -FilePath (Join-Path $projectRoot 'dist\XDSuite\XDSuite.exe') -ArgumentList @('--test-uninstall',('"' + $fixtureRoot + '"'),'--report',('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
if ($test.ExitCode -ne 0) { throw "Uninstall integration test failed: $report" }
if (Test-Path -LiteralPath ('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + $id + '}_is1')) { throw 'Fixture registration remains.' }
Get-Content -LiteralPath $report
