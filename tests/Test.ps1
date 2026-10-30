$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'build\DiscordVolumeKeeper.exe'
[Reflection.Assembly]::LoadFrom($exe) | Out-Null
Add-Type -AssemblyName System.Web.Extensions
function Assert-True($value,$name) { if(-not $value) { throw "FAIL: $name" }; Write-Output "PASS: $name" }
function Assert-Near($actual,$expected,$name) { Assert-True ([Math]::Abs($actual-$expected)-lt 0.0001) $name }
$policy=New-Object DiscordKeeper.VolumePolicy
Assert-Near ($policy.Desired('voice-a',0.05,0.30,$true)) 0.30 'Voice initializes from an inherited low level'
$policy.Committed('voice-a',$true)
Assert-Near ($policy.Desired('voice-a',0.45,0.30,$true)) 0.45 'Manual voice increase survives events'
Assert-Near ($policy.Desired('voice-a',0.10,0.30,$true)) 0.10 'Manual voice decrease survives events'
Assert-Near ($policy.Desired('voice-b',0.05,0.30,$true)) 0.30 'New voice session initializes independently'
Assert-Near ($policy.Desired('notification',0.80,0.05,$false)) 0.05 'Notification ceiling lowers a loud session'
Assert-Near ($policy.Desired('notification',0.02,0.05,$false)) 0.02 'Quieter notification remains quieter'
Assert-Near ($policy.Desired('notification',0,0.05,$false)) 0 'Zero volume is preserved'
$policy.Reset()
Assert-Near ($policy.Desired('voice-a',0.45,0.30,$true)) 0.30 'Apply now resets voice initialization'
$fresh=New-Object DiscordKeeper.Settings
Assert-True (-not $fresh.VoiceDefaultEnabled) 'New users default to notification-only behavior'
$legacy=[DiscordKeeper.Settings]::FromJson('{"NotificationCapPercent":10,"VoiceDefaultPercent":42}')
Assert-True ($legacy.VoiceDefaultEnabled-and $legacy.NotificationCapPercent-eq 10-and $legacy.VoiceDefaultPercent-eq 42) 'Legacy users retain their saved volumes and voice behavior'
$explicit=[DiscordKeeper.Settings]::FromJson('{"NotificationCapPercent":5,"VoiceDefaultPercent":30,"VoiceDefaultEnabled":false}')
Assert-True (-not $explicit.VoiceDefaultEnabled) 'Explicitly disabled voice remains disabled'
$serializer=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$roundtrip=[DiscordKeeper.Settings]::FromJson($serializer.Serialize($explicit))
Assert-True (-not $roundtrip.VoiceDefaultEnabled) 'Voice toggle persists through serialization'
$rejected=$false
try { [DiscordKeeper.Settings]::FromJson('{"NotificationCapPercent":101}') | Out-Null } catch { $rejected=$true }
Assert-True $rejected 'Invalid notification range rejected'
$rejected=$false
try { [DiscordKeeper.Settings]::FromJson('{"VoiceDefaultPercent":-1}') | Out-Null } catch { $rejected=$true }
Assert-True $rejected 'Invalid voice range rejected'
Assert-True ([DiscordKeeper.SessionRouter]::Role('discord --type=utility --utility-sub-type=audio.mojom.AudioService')-eq 'Notification') 'Audio service maps to notifications'
Assert-True ([DiscordKeeper.SessionRouter]::Role('discord --type=renderer')-eq 'Voice') 'Discord child audio processes remain eligible for voice'
Assert-True ([DiscordKeeper.SessionRouter]::Role('discord')-eq 'Voice') 'Main Discord process maps to voice'
Assert-True ([DiscordKeeper.SessionRouter]::Role('')-eq 'Unknown') 'Unavailable command line remains unknown'
$notification=New-Object DiscordKeeper.Row
$notification.PID=123; $notification.Device=1; $notification.Percent=80; $notification.Changed=$true
$voice=New-Object DiscordKeeper.Row
$voice.PID=456; $voice.Device=0; $voice.Percent=45
$report=[DiscordKeeper.DiagnosticReport]::Create($explicit,[DiscordKeeper.Row[]]@($notification),[DiscordKeeper.Row[]]@($voice))
Assert-True ($report.Contains('Notification | 123 | 1 | 5% | changed')) 'Diagnostic report reflects changed notification level'
Assert-True ($report.Contains('Voice | 456 | 0 | 45% | unchanged')) 'Diagnostic report preserves observed voice level'
Assert-True (-not $report.Contains($env:USERPROFILE)) 'Diagnostic report excludes the user profile path'
Assert-True ($report.Contains('Voice defaults: disabled')) 'Diagnostic report shows disabled voice mode'
$voice.Error='Private failure at C:\Users\PrivateUser\secret.txt'
$safeFailure=[DiscordKeeper.DiagnosticReport]::Create($explicit,[DiscordKeeper.Row[]]@(),[DiscordKeeper.Row[]]@($voice))
Assert-True ($safeFailure.Contains('failed (Core Audio)')-and -not $safeFailure.Contains('PrivateUser')) 'Diagnostic failures omit private exception messages'
$bytes=[IO.File]::ReadAllBytes($exe)
$pe=[BitConverter]::ToInt32($bytes,0x3c)
Assert-True ([BitConverter]::ToUInt16($bytes,$pe+24+68)-eq 2) 'Executable has GUI subsystem, not console'
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
Assert-True ($version-eq (Get-Content -LiteralPath (Join-Path $repo 'VERSION') -Raw).Trim()) 'Binary version matches release version'
foreach($file in @(Get-ChildItem -LiteralPath (Join-Path $repo 'scripts') -Filter '*.ps1')) {
    $tokens=$null; $errorsFound=$null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errorsFound) | Out-Null
    Assert-True ($errorsFound.Count-eq 0) ($file.Name+' has valid PowerShell syntax')
}
Write-Output 'All automated checks passed. Live audio and installer lifecycle require the manual checklist.'
