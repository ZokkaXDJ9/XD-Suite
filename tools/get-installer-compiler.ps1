$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolsDirectory = Join-Path $projectRoot '.tools'
New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null
$package = Join-Path $toolsDirectory 'innosetup-7.1.0-x64.exe'
$expected = '0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f'
if (-not (Test-Path -LiteralPath $package)) {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe' -OutFile $package
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expected) { throw 'SHA-256 mismatch. The compiler will not be run.' }
if ((Get-AuthenticodeSignature -LiteralPath $package).Status -ne 'Valid') { throw 'The compiler installer does not have a valid Authenticode signature.' }
$destination = Join-Path $toolsDirectory 'InnoSetup'
$process = Start-Process -FilePath $package -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/CURRENTUSER','/NOICONS',('/DIR="' + $destination + '"')) -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Compiler setup failed ($($process.ExitCode))." }
Write-Host "Inno Setup compiler ready: $destination\ISCC.exe"
