[CmdletBinding()]
param([ValidateSet('Build','Publish','Plan')][string]$Action='Build',
      [string]$Branch=$env:GITHUB_REF_NAME,
      [long]$RunNumber=$env:GITHUB_RUN_NUMBER,
      [int]$RunAttempt=$env:GITHUB_RUN_ATTEMPT)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$root=Split-Path -Parent $PSScriptRoot
$repository='ZokkaXDJ9/XD-Suite'
if($Branch -notmatch '^app/[a-z0-9-]+$' -or $RunNumber -lt 1 -or $RunAttempt -lt 1 -or $RunAttempt -gt 999){throw 'Invalid branch or workflow run identity.'}
$betaConfigs=@(Get-ChildItem -Path (Join-Path $root '*\beta.json') -File | Where-Object {([IO.File]::ReadAllText($_.FullName)|ConvertFrom-Json).branch -eq $Branch})
if($betaConfigs.Count -ne 1){throw ('Expected one beta.json for '+$Branch)}
$source=$betaConfigs[0].Directory.FullName
$config=[IO.File]::ReadAllText($betaConfigs[0].FullName)|ConvertFrom-Json
$buildNumber=1000000000L+$RunNumber*1000L+$RunAttempt
if($Action -eq 'Plan'){@{branch=$Branch;source=$source;buildNumber=$buildNumber}|ConvertTo-Json;return}
if($env:GITHUB_ACTIONS -ne 'true' -or $env:GITHUB_REPOSITORY -ne $repository -or $env:GITHUB_SHA -notmatch '^[a-f0-9]{40}$'){throw 'Release CI must run on the owning GitHub repository.'}
if((& git -C $root rev-parse HEAD).Trim() -ne $env:GITHUB_SHA){throw 'Checkout differs from the triggering commit.'}
$output=Join-Path $root 'artifacts\beta'
if($Action -eq 'Build'){
    & (Join-Path $PSScriptRoot 'get-installer-compiler.ps1')
    $lock=[IO.File]::ReadAllText((Join-Path $source 'engine.lock.json'))|ConvertFrom-Json
    if($lock.sha256 -notmatch '^[a-f0-9]{64}$' -or $lock.url -notlike 'https://github.com/BtbN/FFmpeg-Builds/releases/download/*'){throw 'Invalid engine lock.'}
    $cache=Join-Path $root ('.tools\converter-engine\'+$lock.sha256.Substring(0,12))
    New-Item -ItemType Directory -Force $cache | Out-Null
    $archive=Join-Path $cache 'engine.zip'
    if(-not(Test-Path -LiteralPath $archive)){
        Invoke-WebRequest -UseBasicParsing -Uri $lock.url -OutFile ($archive+'.partial')
        if((Get-FileHash -LiteralPath ($archive+'.partial')).Hash -ne $lock.sha256){throw 'Downloaded engine checksum mismatch.'}
        Move-Item -LiteralPath ($archive+'.partial') -Destination $archive
    }
    if((Get-FileHash -LiteralPath $archive).Hash -ne $lock.sha256){throw 'Engine checksum mismatch.'}
    & (Join-Path $source 'tools\build-beta.ps1') -ToolsRoot (Join-Path $root '.tools') -BuildNumber $buildNumber
    & (Join-Path $source 'tools\test-beta-installer.ps1') -ToolsRoot (Join-Path $root '.tools')
    return
}
$catalog=[IO.File]::ReadAllText((Join-Path $output 'beta-catalog.json'))|ConvertFrom-Json
if($catalog.schemaVersion -ne 1 -or $catalog.apps.Count -ne 1){throw 'Expected one tested beta package.'}
$package=$catalog.apps[0]
if($package.branch -ne $Branch -or $package.channel -ne 'beta' -or $package.registryId -ne $config.registryId -or $package.build -notmatch ('\.'+$buildNumber+'$')){throw 'Package does not match the workflow run.'}
$uri=[uri]$package.installerUrl
$parts=$uri.AbsolutePath.Split('/')
if($uri.Host -ne 'github.com' -or $parts.Length -ne 7 -or $package.installerUrl -notlike ('https://github.com/'+$repository+'/releases/download/*')){throw 'Invalid prerelease installer URL.'}
$tag=[uri]::UnescapeDataString($parts[5])
$setup=Join-Path $output ([IO.Path]::GetFileName($uri.AbsolutePath))
if((Get-FileHash -LiteralPath $setup).Hash -ne $package.installerSha256 -or (Get-Item -LiteralPath $setup).Length -ne $package.installerSizeBytes){throw 'Tested installer no longer matches its catalog.'}
$notes=Join-Path $output 'release-notes.md'
$body=$config.nameDe+' '+$package.version+'-Beta'+[Environment]::NewLine+[Environment]::NewLine+'Automatischer Beta-Build / Automatic beta build: '+$package.build+[Environment]::NewLine+'Branch: '+$Branch+[Environment]::NewLine+'Commit: '+$env:GITHUB_SHA+[Environment]::NewLine+'CI: '+$env:GITHUB_SERVER_URL+'/'+$repository+'/actions/runs/'+$env:GITHUB_RUN_ID+[Environment]::NewLine+[Environment]::NewLine+'Build, application checks and isolated installer checks passed. Physical media and audible playback still require manual testing.'
[IO.File]::WriteAllText($notes,$body,(New-Object Text.UTF8Encoding($false)))
& gh release create $tag --repo $repository --target $env:GITHUB_SHA --title ($config.nameEn+' '+$package.version+'-Beta - Build '+$buildNumber) --notes-file $notes --draft --prerelease --latest=false $setup (Join-Path $output 'beta-catalog.json')
if($LASTEXITCODE -ne 0){throw 'Prerelease draft creation failed. Existing releases are not replaced.'}
$releases=(& gh api ('repos/'+$repository+'/releases?per_page=100'))|ConvertFrom-Json
if($LASTEXITCODE -ne 0){throw 'Cannot inspect uploaded draft.'}
$draft=@($releases|Where-Object{$_.tag_name -eq $tag -and $_.draft -and $_.prerelease -and $_.target_commitish -eq $env:GITHUB_SHA})
if($draft.Count -ne 1){throw 'Expected one draft for the triggering commit.'}
foreach($file in @($setup,(Join-Path $output 'beta-catalog.json'))){
    $asset=@($draft[0].assets|Where-Object name -eq ([IO.Path]::GetFileName($file)))
    if($asset.Count -ne 1 -or $asset[0].size -ne (Get-Item -LiteralPath $file).Length -or $asset[0].digest -ne ('sha256:'+(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant())){throw 'Uploaded asset verification failed.'}
}
& gh api --method PATCH ('repos/'+$repository+'/releases/'+$draft[0].id) -F draft=false -F prerelease=true -f make_latest=false --jq .html_url
if($LASTEXITCODE -ne 0){throw 'Verified draft could not be published.'}
