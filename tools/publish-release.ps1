[CmdletBinding()]
param(
    [string]$Repository = 'ZokkaXDJ9/XD-Suite',
    [switch]$CreateRepository,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$tag = 'v0.3.1'
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository.' }
Push-Location $projectRoot
try {
    $assets = @('dist\XD-Suite-Setup-0.3.1.exe','dist\XD-Brenner-Setup-0.3.1.exe','dist\catalog.json','dist\SHA256SUMS.txt')
    foreach ($asset in $assets) { if (-not (Test-Path -LiteralPath $asset)) { throw "Missing release asset: $asset" } }
    $catalog = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'dist\catalog.json')) | ConvertFrom-Json
    $burner = $catalog.apps | Where-Object id -eq 'xd-burner'
    if ($burner.installerSha256 -ne (Get-FileHash -LiteralPath 'dist\XD-Brenner-Setup-0.3.1.exe').Hash -or $burner.installerUrl -notlike "https://github.com/$Repository/releases/download/$tag/*") { throw 'Catalog does not match the release assets or target repository.' }
    if ($CreateRepository) {
        if (Test-Path -LiteralPath '.git') { throw 'A repository already exists. Refusing to initialize or switch branches.' }
        & gh repo create $Repository --public --description 'Windows CD burner and application launcher with German and English interfaces.'
        if ($LASTEXITCODE -ne 0) { throw 'Repository creation failed.' }
        & git init -b main
        if ($LASTEXITCODE -ne 0) { throw 'Git initialization failed.' }
        & git remote add origin "https://github.com/$Repository.git"
        if ($LASTEXITCODE -ne 0) { throw 'Remote configuration failed.' }
    }
    if (-not (Test-Path -LiteralPath '.git')) { throw 'No local repository. Use -CreateRepository only after approving a new public repository.' }
    $remote = (& git remote get-url origin).Trim()
    if ($LASTEXITCODE -ne 0 -or $remote -notin @("https://github.com/$Repository.git","git@github.com:$Repository.git")) { throw 'Origin does not match target repository.' }
    $branch = (& git branch --show-current).Trim()
    & git add -A -- '.gitattributes' '.gitignore' 'LICENSE' 'README.md' 'build-release.ps1' 'XDBurner' 'XD.Common' 'XD.UpdateHelper' 'XDSuite' 'catalog' 'installer' 'licenses' 'tests' 'tools' 'docs'
    if ($LASTEXITCODE -ne 0) { throw 'Staging failed.' }
    & git diff --cached --check
    if ($LASTEXITCODE -ne 0) { throw 'Staged diff checks failed.' }
    & git diff --cached --quiet
    if ($LASTEXITCODE -eq 1) { & git commit -m 'Resolve the current release catalog through the GitHub API'; if ($LASTEXITCODE -ne 0) { throw 'Commit failed.' } }
    & git push -u origin $branch
    if ($LASTEXITCODE -ne 0) { throw 'Push failed.' }
    $releaseArgs = @('release','create',$tag,'--repo',$Repository,'--target',$branch,'--title','XD Suite & XD Burner 0.3.1','--notes-file','docs\release-notes-0.3.1.md')
    if (-not $Publish) { $releaseArgs += '--draft' }
    $releaseArgs += $assets
    & gh @releaseArgs
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Existing releases are not overwritten automatically.' }
}
finally { Pop-Location }
