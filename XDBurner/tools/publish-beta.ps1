[CmdletBinding()]
param([switch]$Publish)
$ErrorActionPreference='Stop'
$source=Split-Path -Parent $PSScriptRoot
$root=Split-Path -Parent $source
$config=[IO.File]::ReadAllText((Join-Path $source 'beta.json')) | ConvertFrom-Json
if((& git -C $root branch --show-current).Trim() -ne $config.branch){ throw 'Beta publication requires its application branch.' }
if(@(& git -C $root status --porcelain).Count -ne 0){ throw 'Commit the tested beta branch before publishing.' }
$output=Join-Path $root 'artifacts\beta'
$catalog=[IO.File]::ReadAllText((Join-Path $output 'beta-catalog.json')) | ConvertFrom-Json
$package=$catalog.apps[0]
$setup=Join-Path $output $config.setup
if($package.channel -ne 'beta' -or $package.branch -ne $config.branch -or (Get-FileHash -LiteralPath $setup).Hash -ne $package.installerSha256 -or (Get-Item -LiteralPath $setup).Length -ne $package.installerSizeBytes){ throw 'Tested beta catalog does not match the installer.' }
$commit=(& git -C $root rev-parse HEAD).Trim()
& git -C $root push -u origin $config.branch
if($LASTEXITCODE -ne 0){ throw 'Beta branch push failed.' }
& gh release create $config.releaseTag --repo ZokkaXDJ9/XD-Suite --target $commit --title ($config.nameEn+' '+$package.version+'-Beta') --notes-file (Join-Path $source 'README-beta.md') --draft --prerelease --latest=false $setup (Join-Path $output 'beta-catalog.json')
if($LASTEXITCODE -ne 0){ throw 'Beta draft creation failed. Existing releases are never overwritten.' }
$releases=(& gh api 'repos/ZokkaXDJ9/XD-Suite/releases?per_page=100') | ConvertFrom-Json
if($LASTEXITCODE -ne 0){ throw 'Cannot verify beta release.' }
$release=@($releases | Where-Object { $_.tag_name -eq $config.releaseTag -and $_.draft -and $_.prerelease -and $_.target_commitish -eq $commit })
if($release.Count -ne 1){ throw 'Expected one beta draft for the tested branch commit.' }
foreach($file in @($setup,(Join-Path $output 'beta-catalog.json'))){
    $asset=@($release[0].assets | Where-Object name -eq ([IO.Path]::GetFileName($file)))
    if($asset.Count -ne 1 -or $asset[0].size -ne (Get-Item -LiteralPath $file).Length -or $asset[0].digest -ne ('sha256:'+(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant())){ throw 'Uploaded beta asset verification failed.' }
}
if($Publish){
    & gh api --method PATCH ('repos/ZokkaXDJ9/XD-Suite/releases/'+$release[0].id) -F draft=false -F prerelease=true -f make_latest=false --jq .html_url
    if($LASTEXITCODE -ne 0){ throw 'Beta publication failed; verified draft retained.' }
}
