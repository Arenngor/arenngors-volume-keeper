[CmdletBinding(SupportsShouldProcess=$true)]
param()
$ErrorActionPreference='Stop'
$source = Split-Path $PSScriptRoot -Parent
$base = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
$target = [IO.Path]::GetFullPath((Join-Path $base 'ArenngorsVolumeKeeper'))
if (-not $target.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation directory.' }
$exe = Join-Path $target 'DiscordVolumeKeeper.exe'
$payload = Join-Path $source 'DiscordVolumeKeeper.exe'
if (-not (Test-Path -LiteralPath $payload -PathType Leaf)) { throw 'Run Install.cmd from an extracted release ZIP, not the source repository.' }
$marker = Join-Path $target 'installation.id'
$identity = 'ArenngorsVolumeKeeper:per-user:v1'
if (Test-Path -LiteralPath $target) {
    $children = @(Get-ChildItem -LiteralPath $target -Force)
    if ($children.Count -gt 0 -and (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $identity)) {
        throw "This folder already contains files without a Volume Keeper installation marker: $target"
    }
}
foreach ($process in @(Get-Process -Name DiscordVolumeKeeper -ErrorAction SilentlyContinue)) {
    try { $runningPath=$process.MainModule.FileName } catch { throw 'A Volume Keeper process could not be inspected. Exit all Volume Keeper copies and retry.' }
    if ([String]::Equals($runningPath,$exe,[StringComparison]::OrdinalIgnoreCase)) { throw 'Exit the installed Volume Keeper through its tray menu, then run Install.cmd again.' }
}
if (-not $PSCmdlet.ShouldProcess($target,'Install/update Volume Keeper, Start menu shortcut and per-user uninstall entry')) { return }
New-Item -ItemType Directory -Path $target -Force | Out-Null
Set-Content -LiteralPath $marker -Value $identity -Encoding ASCII
$same = [String]::Equals([IO.Path]::GetFullPath($source),$target,[StringComparison]::OrdinalIgnoreCase)
if (-not $same) {
    if (Test-Path -LiteralPath $exe) { Copy-Item -LiteralPath $exe -Destination ($exe+'.previous') -Force }
    $temporary = Join-Path $target 'DiscordVolumeKeeper.exe.new'
    Copy-Item -LiteralPath $payload -Destination $temporary -Force
    Move-Item -LiteralPath $temporary -Destination $exe -Force
    foreach ($name in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md','Uninstall.cmd')) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $target $name) -Force
    }
    New-Item -ItemType Directory -Path (Join-Path $target 'scripts') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination (Join-Path $target 'scripts\Uninstall.ps1') -Force
}
$shortcutPath=Join-Path ([Environment]::GetFolderPath('Programs')) "Arenngor's Volume Keeper.lnk"
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath=$exe
$shortcut.WorkingDirectory=$target
$shortcut.IconLocation="$exe,0"
$shortcut.Save()
$run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$previous = (Get-ItemProperty -LiteralPath $run -Name DiscordVolumeKeeper -ErrorAction SilentlyContinue).DiscordVolumeKeeper
if ($previous) { Set-ItemProperty -LiteralPath $run -Name DiscordVolumeKeeper -Value ('"'+$exe+'" --tray') }
$uninstall='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ArenngorsVolumeKeeper'
New-Item -Path $uninstall -Force | Out-Null
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
$properties=@{
 DisplayName="Arenngor's Volume Keeper"; DisplayVersion=$version; Publisher='Arenngor';
 InstallLocation=$target; DisplayIcon=$exe;
 UninstallString=('"'+$env:SystemRoot+'\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $target 'scripts\Uninstall.ps1')+'"')
}
foreach($key in $properties.Keys) { New-ItemProperty -LiteralPath $uninstall -Name $key -Value $properties[$key] -PropertyType String -Force | Out-Null }
foreach($key in @('NoModify','NoRepair')) { New-ItemProperty -LiteralPath $uninstall -Name $key -Value 1 -PropertyType DWord -Force | Out-Null }
Write-Output "Installed $version in $target"
Write-Output 'Open it from the Start menu. Your saved settings have been preserved.'
