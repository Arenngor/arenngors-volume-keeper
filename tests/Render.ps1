param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory) { $OutputDirectory=Join-Path $repo 'build\preview' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Reflection.Assembly]::LoadFrom((Join-Path $repo 'build\DiscordVolumeKeeper.exe')) | Out-Null
$config=New-Object DiscordKeeper.Settings
$engine=New-Object DiscordKeeper.Engine($config)
function Save-Preview($form,$name) {
    $form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=[Drawing.Point]::new(-10000,-10000)
    $form.Show(); [Windows.Forms.Application]::DoEvents()
    $image=[Drawing.Bitmap]::new($form.Width,$form.Height)
    try { $form.DrawToBitmap($image,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $image.Save((Join-Path $OutputDirectory $name)) } finally { $image.Dispose() }
}
try {
    $window=[DiscordKeeper.SettingsWindow]::new($engine,$config,[Action]{})
    Save-Preview $window 'settings.png'
    $flags=[Reflection.BindingFlags]'NonPublic,Instance'
    $check=$window.GetType().GetField('voiceEnabled',$flags).GetValue($window)
    $numeric=$window.GetType().GetField('voice',$flags).GetValue($window)
    if($numeric.Enabled) { throw 'Fresh install voice field should be disabled.' }
    $check.Checked=$true
    if(-not $numeric.Enabled) { throw 'Voice field did not enable.' }
    $check.Checked=$false
    if($numeric.Enabled) { throw 'Voice field did not disable.' }
    $diagnostics=[DiscordKeeper.DiagnosticsWindow]::new($engine)
    Save-Preview $diagnostics 'diagnostics.png'
    $diagnostics.Close(); $diagnostics.Dispose()
    $window.Shutdown(); $window.Dispose()
} finally { $engine.Dispose() }
Write-Output "Rendered settings/diagnostics and checked voice toggle in $OutputDirectory"
