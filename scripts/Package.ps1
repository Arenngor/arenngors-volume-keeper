$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$version=(Get-Content -LiteralPath (Join-Path $repo 'VERSION') -Raw).Trim()
$exe=Join-Path $repo 'build\DiscordVolumeKeeper.exe'
if(-not (Test-Path -LiteralPath $exe)) { throw 'Build first.' }
$dist=Join-Path $repo 'dist'
$name="ArenngorsVolumeKeeper-$version-windows"
$folder=Join-Path $dist $name
New-Item -ItemType Directory -Path $folder,(Join-Path $folder 'scripts') -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $folder 'DiscordVolumeKeeper.exe') -Force
foreach($nameToCopy in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md','Install.cmd','Uninstall.cmd')) {
    Copy-Item -LiteralPath (Join-Path $repo $nameToCopy) -Destination (Join-Path $folder $nameToCopy) -Force
}
foreach($script in @('Install.ps1','Uninstall.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination (Join-Path $folder ('scripts\'+$script)) -Force
}
$zip=Join-Path $dist "$name.zip"
Compress-Archive -LiteralPath $folder -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($zip+'.sha256') -Value ($hash+'  '+[IO.Path]::GetFileName($zip)) -Encoding ASCII
Write-Output "Packaged $zip"
