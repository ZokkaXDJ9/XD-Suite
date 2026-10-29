[CmdletBinding()]
param([string]$Repository = 'ZokkaXDJ9/XD-Suite', [switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository.' }
Push-Location $projectRoot
try {
    $version = ([xml][IO.File]::ReadAllText((Join-Path $projectRoot 'XDSuite\XDSuite.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ }
    $tag = 'v' + $version
    if ((& git branch --show-current).Trim() -ne 'main') { throw 'Publish suite releases only from main.' }
    if ((& git remote get-url origin).Trim() -notin @("https://github.com/$Repository.git", "git@github.com:$Repository.git")) { throw 'Origin does not match target repository.' }
    if (@(& git status --porcelain).Count -ne 0) { throw 'Commit the tested release before publishing.' }
    $catalog = [IO.File]::ReadAllText((Join-Path $projectRoot 'dist\catalog.json')) | ConvertFrom-Json
    $assets = [Collections.Generic.List[string]]::new()
    foreach ($name in @("XD-Suite-Setup-$version.exe", 'catalog.json', 'SHA256SUMS.txt')) { $assets.Add('dist\' + $name) }
    foreach ($app in $catalog.apps) {
        if ([version]$app.version -gt [version]$version) { throw 'An application version exceeds the suite.' }
        $name = [IO.Path]::GetFileName(([uri]$app.installerUrl).AbsolutePath)
        $path = Join-Path $projectRoot ('dist\' + $name)
        if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -ne $app.installerSha256 -or (Get-Item -LiteralPath $path).Length -ne $app.installerSizeBytes) { throw "Catalog artifact mismatch: $name" }
        if ($app.installerUrl.StartsWith("https://github.com/$Repository/releases/download/$tag/")) { $assets.Add('dist\' + $name) }
    }
    $sums = @{}
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $projectRoot 'dist\SHA256SUMS.txt'))) {
        $parts = $line.Trim() -split '\s+', 2
        if ($parts.Count -eq 2) { $sums[$parts[1].TrimStart('*')] = $parts[0] }
    }
    foreach ($asset in $assets) {
        if (-not (Test-Path -LiteralPath $asset)) { throw "Missing release asset: $asset" }
        $name = [IO.Path]::GetFileName($asset)
        if ($name -ne 'SHA256SUMS.txt' -and (Get-FileHash -LiteralPath $asset).Hash -ne $sums[$name]) { throw "Release checksum mismatch: $name" }
    }
    & git push origin main
    if ($LASTEXITCODE -ne 0) { throw 'Push failed.' }
    $commit = (& git rev-parse HEAD).Trim()
    & gh release create $tag --repo $Repository --target $commit --title "XD Suite $version" --notes-file "docs\release-notes-$version.md" --draft @assets
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Existing releases are not overwritten.' }
    $remoteJson = & gh api "repos/$Repository/releases/tags/$tag"
    if ($LASTEXITCODE -ne 0) { throw 'Draft release verification failed.' }
    $remote = $remoteJson | ConvertFrom-Json
    if (-not $remote.draft) { throw 'Expected an unpublished draft.' }
    foreach ($asset in $assets) {
        $name = [IO.Path]::GetFileName($asset)
        $found = @($remote.assets | Where-Object name -eq $name)
        if ($found.Count -ne 1 -or $found[0].size -ne (Get-Item -LiteralPath $asset).Length) { throw "Uploaded release asset mismatch: $name" }
        $digest = 'sha256:' + (Get-FileHash -LiteralPath $asset).Hash.ToLowerInvariant()
        if ($found[0].digest -ne $digest) { throw "Uploaded release digest mismatch: $name" }
    }
    if ($Publish) {
        & gh release edit $tag --repo $Repository --draft=false --latest
        if ($LASTEXITCODE -ne 0) { throw 'Release publication failed; draft retained.' }
    }
}
finally { Pop-Location }
