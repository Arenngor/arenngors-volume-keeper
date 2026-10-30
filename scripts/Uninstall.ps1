[CmdletBinding(SupportsShouldProcess=$true)]
param()
$ErrorActionPreference='Stop'
$target=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$expected=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\ArenngorsVolumeKeeper'))
if (-not [String]::Equals($target,$expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Uninstall works only from the managed per-user installation. For portable copies, disable startup in the app, exit, and delete the extracted folder.' }
$marker=Join-Path $target 'installation.id'
if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne 'ArenngorsVolumeKeeper:per-user:v1') { throw 'Installation marker is missing or invalid. No files were removed.' }
$exe=Join-Path $target 'DiscordVolumeKeeper.exe'
foreach($process in @(Get-Process -Name DiscordVolumeKeeper -ErrorAction SilentlyContinue)) {
    try { $path=$process.MainModule.FileName } catch { throw 'Exit all Volume Keeper copies before uninstalling.' }
    if ([String]::Equals($path,$exe,[StringComparison]::OrdinalIgnoreCase)) { throw 'Exit Volume Keeper through its tray menu before uninstalling.' }
}
if (-not $PSCmdlet.ShouldProcess($target,'Remove installed application, owned startup entry and shortcuts; retain preferences')) { return }
$run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$value=(Get-ItemProperty -LiteralPath $run -Name DiscordVolumeKeeper -ErrorAction SilentlyContinue).DiscordVolumeKeeper
if ([String]::Equals($value,('"'+$exe+'" --tray'),[StringComparison]::OrdinalIgnoreCase)) { Remove-ItemProperty -LiteralPath $run -Name DiscordVolumeKeeper }
$shortcutPath=Join-Path ([Environment]::GetFolderPath('Programs')) "Arenngor's Volume Keeper.lnk"
if(Test-Path -LiteralPath $shortcutPath) {
    $shell=New-Object -ComObject WScript.Shell
    $shortcut=$shell.CreateShortcut($shortcutPath)
    if([String]::Equals($shortcut.TargetPath,$exe,[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $shortcutPath }
}
$key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ArenngorsVolumeKeeper'
if(Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key }
foreach($name in @('DiscordVolumeKeeper.exe','DiscordVolumeKeeper.exe.previous','DiscordVolumeKeeper.exe.new','README.md','LICENSE','THIRD_PARTY_NOTICES.md','Uninstall.cmd','installation.id','scripts\Uninstall.ps1')) {
    $path=Join-Path $target $name
    if(Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
}
foreach($directory in @((Join-Path $target 'scripts'),$target)) {
    if((Test-Path -LiteralPath $directory) -and @(Get-ChildItem -LiteralPath $directory -Force).Count-eq 0) { Remove-Item -LiteralPath $directory }
}
Write-Output 'Volume Keeper uninstalled. Settings remain in %LOCALAPPDATA%\DiscordVolumeKeeper.'
