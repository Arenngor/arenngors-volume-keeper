param([Parameter(Mandatory=$true)][string]$CertificateThumbprint,[string]$TimestampServer='http://timestamp.digicert.com')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'build\DiscordVolumeKeeper.exe'
$signer=Get-Command signtool.exe -ErrorAction Stop
& $signer.Source sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampServer /td SHA256 $exe
if($LASTEXITCODE-ne 0) { throw 'Code signing failed.' }
& $signer.Source verify /pa $exe
if($LASTEXITCODE-ne 0) { throw 'Signature verification failed.' }
Write-Output 'Signature verified. Run scripts/Package.ps1 to package the signed executable.'
