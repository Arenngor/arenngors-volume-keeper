$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$version=(Get-Content -LiteralPath (Join-Path $repo 'VERSION') -Raw).Trim()
$release=Join-Path $repo ("dist\ArenngorsVolumeKeeper-$version-windows")
$marker=Join-Path $env:LOCALAPPDATA 'Programs\ArenngorsVolumeKeeper\installation.id'
$before=Test-Path -LiteralPath $marker
& (Join-Path $release 'scripts\Install.ps1') -WhatIf
if((Test-Path -LiteralPath $marker)-ne $before) { throw 'Installer WhatIf changed installation marker state.' }
Write-Output 'PASS: Installer WhatIf performs no installation'
$rejected=$false
try { & (Join-Path $repo 'scripts\Uninstall.ps1') -WhatIf } catch { $rejected=$_.Exception.Message.Contains('managed per-user installation') }
if(-not $rejected) { throw 'Uninstaller did not reject an unmanaged source directory.' }
Write-Output 'PASS: Uninstaller rejects unmanaged directories before deletion'
