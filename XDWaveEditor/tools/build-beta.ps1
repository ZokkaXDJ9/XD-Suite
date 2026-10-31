[CmdletBinding()]
param([string]$ToolsRoot, [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$appSource = Split-Path -Parent $PSScriptRoot
$root = Split-Path -Parent $appSource
$config = [IO.File]::ReadAllText((Join-Path $appSource 'beta.json')) | ConvertFrom-Json
if ((& git -C $root branch --show-current).Trim() -ne $config.branch) { throw 'Build beta only on its application branch.' }
if (-not $ToolsRoot) {
    $commonGit = (& git -C $root rev-parse --path-format=absolute --git-common-dir).Trim()
    if ($commonGit.StartsWith('/')) { $cygpath = Join-Path (Split-Path -Parent (Get-Command git).Source) 'cygpath.exe'; $commonGit = (& $cygpath -w $commonGit).Trim() }
    $ToolsRoot = Join-Path (Split-Path -Parent $commonGit) '.tools'
}
$version = ([xml][IO.File]::ReadAllText((Join-Path $root 'XDSuite\XDSuite.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ }
$output = Join-Path $root 'artifacts\beta'
$app = Join-Path $output 'app'
New-Item -ItemType Directory -Force $app | Out-Null
$production = @('XDSuite','XDBurner','XDConverter','XD.Common','XD.UpdateHelper','catalog','installer','build-release.ps1')
function Snapshot {
    $files = @{}
    foreach($part in $production) {
        $path = Join-Path $root $part
        $items = if(Test-Path -LiteralPath $path -PathType Leaf){ Get-Item -LiteralPath $path }else{ Get-ChildItem -LiteralPath $path -File -Recurse | Where-Object FullName -NotMatch '\\(bin|obj)\\' }
        foreach($item in $items){ $files[$item.FullName] = (Get-FileHash -LiteralPath $item.FullName).Hash }
    }
    $files
}
$before = Snapshot
& (Join-Path $PSScriptRoot 'build-icon.ps1')
& dotnet publish (Join-Path $appSource ($config.program + '.csproj')) -c Release -r win-x64 --self-contained true -o $app '-p:RuntimeFrameworkVersion=8.0.33' '-p:DebugType=embedded'
if($LASTEXITCODE -ne 0){ throw 'Beta build failed.' }
$exe = Join-Path $app ($config.program + '.Beta.exe')
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if($info.ProductVersion -ne ($version + '-Beta+' + $config.build)){ throw 'Beta version mismatch.' }
$lock = [IO.File]::ReadAllText((Join-Path $appSource 'engine.lock.json')) | ConvertFrom-Json
$archive = Join-Path $ToolsRoot ('converter-engine\' + $lock.sha256.Substring(0,12) + '\engine.zip')
if((Get-FileHash -LiteralPath $archive).Hash -ne $lock.sha256){ throw 'Pinned test/encoding engine checksum mismatch.' }
$engineRoot = Join-Path $root ('.tools\beta-engine\' + $lock.sha256.Substring(0,12))
if(-not (Test-Path -LiteralPath (Join-Path $engineRoot 'verified.txt'))){ New-Item -ItemType Directory -Force $engineRoot | Out-Null; Expand-Archive -LiteralPath $archive -DestinationPath $engineRoot -Force; [IO.File]::WriteAllText((Join-Path $engineRoot 'verified.txt'),$lock.sha256) }
$binary = Get-ChildItem -LiteralPath $engineRoot -Filter ffmpeg.exe -File -Recurse | Select-Object -First 1
$payload = $binary.Directory.Parent.FullName
if($config.includeEngine){
    New-Item -ItemType Directory -Force (Join-Path $app 'engine') | Out-Null
    Copy-Item -Path (Join-Path $payload 'bin\*') -Destination (Join-Path $app 'engine') -Force
    Copy-Item -LiteralPath (Join-Path $payload 'LICENSE.txt') -Destination (Join-Path $app 'engine\LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $payload 'doc') -Destination (Join-Path $app 'engine') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $appSource 'engine.lock.json') -Destination $app -Force
}
Copy-Item -LiteralPath (Join-Path $appSource 'README.md'),(Join-Path $root 'LICENSE') -Destination $app -Force
New-Item -ItemType Directory -Force (Join-Path $app 'licenses') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'licenses') -File | Where-Object Name -Match '^(dotnet|wpf)' | Copy-Item -Destination (Join-Path $app 'licenses') -Force
if(Test-Path -LiteralPath (Join-Path $appSource 'licenses')){ Copy-Item -Path (Join-Path $appSource 'licenses\*') -Destination (Join-Path $app 'licenses') -Force }
$manifest = @(Get-ChildItem -LiteralPath $app -File -Recurse | Where-Object Name -ne 'payload-files.txt' | ForEach-Object { $_.FullName.Substring($app.Length + 1) }) + @('payload-files.txt')
[IO.File]::WriteAllLines((Join-Path $app 'payload-files.txt'),$manifest,(New-Object Text.UTF8Encoding($false)))
$report = Join-Path $output 'tests.txt'
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test','--engine',('"'+$binary.DirectoryName+'"'),'--report',('"'+$report+'"')) -PassThru -Wait -WindowStyle Hidden
if($test.ExitCode -ne 0){ throw ('Beta tests failed: '+$report) }
foreach($language in @('--german','--english')) {
    $render = Start-Process -FilePath $exe -ArgumentList @('--render',('"'+$output+'"'),$language) -PassThru -Wait -WindowStyle Hidden
    if($render.ExitCode -ne 0){ throw 'Beta UI render failed.' }
}
if(-not $SkipInstaller){
    & (Join-Path $ToolsRoot 'InnoSetup\ISCC.exe') /Q (Join-Path $appSource 'installer\Beta.iss')
    if($LASTEXITCODE -ne 0){ throw 'Beta setup failed.' }
    $setup = Get-Item -LiteralPath (Join-Path $output $config.setup)
    $setupVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($setup.FullName)
    if($setupVersion.FileVersion.Trim() -ne ($version+'.0') -or $setupVersion.ProductVersion.Trim() -ne ($version+'-Beta')){ throw 'Installer version differs from its beta application.' }
    $package = @{ id=$config.id; name=@{de=$config.nameDe;en=$config.nameEn}; description=@{de=$config.descriptionDe;en=$config.descriptionEn}; category=@{de='Audio / Beta';en='Audio / Beta'}; version=$version; build=$info.ProductVersion; branch=$config.branch; channel='beta'; installerUrl=('https://github.com/ZokkaXDJ9/XD-Suite/releases/download/'+$config.releaseTag+'/'+$config.setup); installerSha256=(Get-FileHash -LiteralPath $setup.FullName).Hash.ToLowerInvariant(); installerSizeBytes=$setup.Length; executable=($config.program+'.Beta.exe'); registryId=$config.registryId; installFolder=$config.folder; features=@(@{de='Getrennte Beta-Installation';en='Separate beta installation'},@{de='Deutsch und Englisch';en='German and English'}) }
    [IO.File]::WriteAllText((Join-Path $output 'beta-catalog.json'),(@{schemaVersion=1;apps=@($package)} | ConvertTo-Json -Depth 10),(New-Object Text.UTF8Encoding($false)))
}
$after=Snapshot
if($before.Count -ne $after.Count){ throw 'Production source tree changed.' }
foreach($key in $before.Keys){ if($before[$key] -ne $after[$key]){ throw ('Production source changed: '+$key) } }
[IO.File]::WriteAllText((Join-Path $output 'isolation-check.txt'),'PASS: Stable applications, shared code, catalog and production installers remain byte-identical. No stable release was built or installed.')
Write-Host ('Beta ready: '+$exe)
