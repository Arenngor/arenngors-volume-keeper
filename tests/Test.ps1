$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $repo 'build\DiscordVolumeKeeper.exe'
[Reflection.Assembly]::LoadFrom($exe) | Out-Null
Add-Type -AssemblyName System.Web.Extensions
function Assert-True($value,$name) { if(-not $value) { throw "FAIL: $name" }; Write-Output "PASS: $name" }
function Assert-Near($actual,$expected,$name) { Assert-True ([Math]::Abs($actual-$expected)-lt 0.0001) $name }
$candidate=New-Object DiscordKeeper.SessionIdentity
$candidate.PID=123; $candidate.Group=[Guid]::NewGuid(); $candidate.Identifier='notifications'
$other=New-Object DiscordKeeper.SessionIdentity
$other.PID=456; $other.Group=[Guid]::NewGuid(); $other.Identifier='voice'
$sessions=[DiscordKeeper.SessionIdentity[]]@($candidate,$other)
Assert-True ($null -eq [DiscordKeeper.NotificationPolicy]::SkipReason($candidate,$sessions,$false)) 'Isolated notification session is eligible'
$originalGroup=$other.Group
$other.Group=$candidate.Group
Assert-True ([DiscordKeeper.NotificationPolicy]::SkipReason($candidate,$sessions,$false) -like 'grouped*') 'Grouped voice and notification sessions are skipped'
$other.Group=$originalGroup
$other.Identifier=$candidate.Identifier
Assert-True ($null -eq [DiscordKeeper.NotificationPolicy]::SkipReason($candidate,$sessions,$false)) 'Distinct Discord sessions may share their saved identifier'
$candidate.IsSystemSounds=$true
Assert-True ([DiscordKeeper.NotificationPolicy]::SkipReason($candidate,$sessions,$false) -like 'shared*') 'System Sounds retains the shared identifier safeguard'
$candidate.IsSystemSounds=$false
$other.Identifier='voice'; $other.PID=$candidate.PID
Assert-True ([DiscordKeeper.NotificationPolicy]::SkipReason($candidate,$sessions,$false) -like 'multiple*') 'Multiple sessions owned by the same process are skipped'
$other.PID=456; $candidate.OwnerStatus=1
Assert-True ([DiscordKeeper.NotificationPolicy]::SkipReason($candidate,$sessions,$false) -like 'shared*') 'Non-exclusive owner status is skipped'
Assert-Near ([DiscordKeeper.NotificationPolicy]::Ceiling(0.8,0.05)) 0.05 'Notification ceiling lowers a loud session'
Assert-Near ([DiscordKeeper.NotificationPolicy]::Ceiling(0.02,0.05)) 0.02 'Quieter notification remains quieter'
$targets=New-Object 'System.Collections.Generic.Dictionary[uint32,single]'
$targets[[uint32]123]=[single]0.05
$system=New-Object DiscordKeeper.SessionIdentity
$system.IsSystemSounds=$true
[float]$cap=0
Assert-True ([DiscordKeeper.NotificationPolicy]::TryGetCap($system,$targets,[single]0.25,[ref]$cap)) 'Windows System Sounds qualifies without a Discord process'
Assert-Near $cap 0.25 'Windows System Sounds uses its separate cap'
Assert-True (-not [DiscordKeeper.NotificationPolicy]::TryGetCap($system,$targets,$null,[ref]$cap)) 'System Sounds stays untouched when disabled'
$other.PID=0
Assert-True (-not [DiscordKeeper.NotificationPolicy]::TryGetCap($other,$targets,[single]0.25,[ref]$cap)) 'PID zero alone never selects a non-System Sounds session'
Assert-True ([DiscordKeeper.NotificationPolicy]::TryGetCap($candidate,$targets,[single]0.25,[ref]$cap)) 'Discord candidate still qualifies with System Sounds enabled'
Assert-Near $cap 0.05 'Discord still uses its own cap'
$legacy=[DiscordKeeper.Settings]::FromJson('{"NotificationCapPercent":10,"VoiceDefaultPercent":42,"VoiceDefaultEnabled":true}')
Assert-True ($legacy.NotificationCapPercent-eq 10) 'Old settings retain the notification ceiling'
Assert-True (-not $legacy.SystemSoundsCapEnabled -and $legacy.SystemSoundsCapPercent-eq 25) 'Existing settings leave the new cap off'
$systemSettings=[DiscordKeeper.Settings]::FromJson('{"SystemSoundsCapEnabled":true,"SystemSoundsCapPercent":0}')
Assert-True ($systemSettings.SystemSoundsCapEnabled -and $systemSettings.SystemSoundsCapPercent-eq 0) 'Zero is a valid enabled System Sounds cap'
$serializer=New-Object System.Web.Script.Serialization.JavaScriptSerializer
Assert-True ($legacy.VoiceDefaultEnabled -and $legacy.VoiceDefaultPercent-eq 42) 'Existing explicit voice settings are restored'
$roundtrip=[DiscordKeeper.Settings]::FromJson($serializer.Serialize($legacy))
Assert-True ($roundtrip.VoiceDefaultEnabled -and $roundtrip.VoiceDefaultPercent-eq 42) 'Voice settings persist'
$fresh=[DiscordKeeper.Settings]::FromJson('{"NotificationCapPercent":15,"SystemSoundsCapEnabled":true,"SystemSoundsCapPercent":25}')
Assert-True (-not $fresh.VoiceDefaultEnabled -and $fresh.VoiceDefaultPercent-eq 30) 'Beta 2 upgrades leave the voice default off'
Assert-True ($fresh.SystemSoundsCapEnabled -and $fresh.SystemSoundsCapPercent-eq 25 -and $fresh.NotificationCapPercent-eq 15) 'Beta 2 caps survive the upgrade'
$voicePolicy=New-Object DiscordKeeper.VoiceDefaults
Assert-Near ($voicePolicy.Desired('call-a',0.15,0.30)) 0.30 'A new call session gets the optional default'
$voicePolicy.Commit('call-a')
Assert-Near ($voicePolicy.Desired('call-a',0.55,0.30)) 0.55 'Manual voice changes survive later events'
Assert-Near ($voicePolicy.Desired('call-b',0.15,0.30)) 0.30 'A distinct call session gets the default independently'
$voicePolicy.Reset()
Assert-Near ($voicePolicy.Desired('call-a',0.55,0.30)) 0.30 'Apply now reinitializes existing voice sessions'
$rejected=$false
try { [DiscordKeeper.Settings]::FromJson('{"NotificationCapPercent":101}') | Out-Null } catch { $rejected=$true }
Assert-True $rejected 'Invalid notification range rejected'
Assert-True ([DiscordKeeper.SessionRouter]::IsNotificationCandidate('discord --type=utility --utility-sub-type=audio.mojom.AudioService')) 'Audio service remains a notification candidate'
Assert-True (-not [DiscordKeeper.SessionRouter]::IsNotificationCandidate('discord --type=renderer')) 'Discord renderer is not a notification candidate'
Assert-True (-not [DiscordKeeper.SessionRouter]::IsNotificationCandidate('discord')) 'Main Discord process is not a notification candidate'
$notification=New-Object DiscordKeeper.Row
$notification.PID=123; $notification.Device=1; $notification.Percent=80
$notification.SkipReason='grouped with another audio session'
$report=[DiscordKeeper.DiagnosticReport]::Create($legacy,[DiscordKeeper.Row[]]@($notification))
Assert-True ($report.Contains('Notification | 123 | 1 | 80% | skipped: grouped')) 'Diagnostics report the skipped session and unchanged volume'
$notification.Error='Private failure at C:\Users\PrivateUser\secret.txt'
$safeFailure=[DiscordKeeper.DiagnosticReport]::Create($legacy,[DiscordKeeper.Row[]]@($notification))
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
