[CmdletBinding()]
param([string]$ToolsRoot)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $ToolsRoot) {
    $common = (& git -C $projectRoot rev-parse --path-format=absolute --git-common-dir).Trim()
    if ($common.StartsWith('/')) {
        $cygpath = Join-Path (Split-Path -Parent (Get-Command git).Source) 'cygpath.exe'
        if (Test-Path -LiteralPath $cygpath) { $common = (& $cygpath -w $common).Trim() }
    }
    $ToolsRoot = Join-Path (Split-Path -Parent $common) '.tools'
}
$id = [Guid]::NewGuid().ToString().ToUpperInvariant()
$fixture = Join-Path $projectRoot ('artifacts\wave-installer-' + $id)
$target = Join-Path $fixture 'installed'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$source = [IO.File]::ReadAllText((Join-Path $projectRoot 'XDWaveEditor\installer\Preview.iss'))
$source = $source.Replace('#define Root AddBackslash(SourcePath) + "..\..\"', '#define Root "' + $projectRoot + '\"')
$source = $source.Replace('A17BCC67-3916-4833-ABF1-91A9658AF359',$id).Replace('PrivilegesRequired=admin','PrivilegesRequired=lowest').Replace('DefaultDirName={autopf}\XDWaveEditorPreview','DefaultDirName=' + $target)
$source = $source.Replace('AppMutex=Local\XDWaveEditor.Preview.App','AppMutex=Local\XDWaveEditor.Test.' + $id)
$source = $source.Replace('{autoprograms}\XD Wave Editor Vorschau.lnk', $fixture + '\Unused-DE.lnk').Replace('{autoprograms}\XD Wave Editor Preview.lnk',$fixture + '\Unused-EN.lnk')
$source = $source.Replace('{autoprograms}\{cm:EditorName}', $fixture + '\EditorShortcut')
$iss = Join-Path $fixture 'Fixture.iss'; [IO.File]::WriteAllText($iss,$source,(New-Object System.Text.UTF8Encoding($false)))
& (Join-Path $ToolsRoot 'InnoSetup\ISCC.exe') /Q ('/O' + $fixture) /FFixture $iss
if ($LASTEXITCODE -ne 0) { throw 'Isolated preview installer compilation failed.' }
$p = Start-Process -FilePath (Join-Path $fixture 'Fixture.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LANG=english' -PassThru -Wait -WindowStyle Hidden
if ($p.ExitCode -ne 0) { throw 'Fixture installation failed.' }
$exe = Join-Path $target 'XDWaveEditor.Preview.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Installed executable missing.' }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $fixture 'EditorShortcut.lnk'))
if ($shortcut.TargetPath -ne $exe -or $shortcut.IconLocation -ne ($exe + ',0')) { throw 'Shortcut or icon is incorrect.' }
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + $id + '}_is1'
if ((Get-ItemProperty -LiteralPath $key).'Inno Setup: Language' -ne 'english') { throw 'Setup language not forwarded.' }
$report = Join-Path $projectRoot 'artifacts\wave-editor-preview-test2\installed-tests.txt'
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test','--report',('"' + $report + '"')) -PassThru -Wait -WindowStyle Hidden
if ($test.ExitCode -ne 0) { throw "Installed editor tests failed: $report" }
$personal = Join-Path $target 'personal-note.txt'; [IO.File]::WriteAllText($personal,'Keep user data.')
$uninstall = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru -Wait -WindowStyle Hidden
if ($uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath $exe) -or (Test-Path -LiteralPath $key) -or -not (Test-Path -LiteralPath $personal)) { throw 'Isolated uninstall or user data preservation failed.' }
$lines = @('PASS: real editor payload installed under isolated per-user AppId.', 'PASS: English setup language, application shortcut and embedded icon verified.', 'PASS: full audio, editing, WPF and muted playback tests passed from installed copy.', 'PASS: independent uninstall removed only the fixture and retained its personal note.', 'NOTE: only the fixture disables elevation; the real setup requests admin rights and uses Program Files. No live product was installed or removed.')
$lines | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts\wave-editor-preview-test2\installer-tests.txt') -Encoding UTF8
$lines
