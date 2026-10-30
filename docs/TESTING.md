# Beta test checklist

Automated policy, settings, routing, diagnostic privacy and executable checks run
through `tests/Test.ps1`. `tests/Render.ps1` constructs the settings and diagnostic
windows and verifies that the voice toggle enables/disables the numeric input.
These are not substitutes for Core Audio tests.

## Recorded validation

| Check | State |
| --- | --- |
| Original notification ceiling on the first test PC | User confirmed on earlier builds |
| Voice Apply now correction | User confirmed on the previous fix build |
| Current beta policy/settings/routing/diagnostic tests | Passed during release preparation |
| Current beta settings/diagnostic layout | Rendered and visually checked |
| Installer dry-run and unmanaged-uninstall rejection | Passed; no installation performed |
| Installer/update/uninstall full lifecycle | Pending Windows desktop testing |
| Second independent Windows PC | Pending |
| Hardware/device matrix below | Pending |

## Release candidate procedure

1. Start from a new Windows profile or temporarily back up the saved settings.
   Confirm voice defaults are off and notifications cap at the configured level.
2. Join a Discord call and play notifications. Test levels above and below the
   ceiling, mute/unmute, and notifications while another person is speaking.
3. Enable voice defaults. Confirm a new voice session gets its default; manually
   raise and lower it and confirm it stays changed after notifications.
4. Change the displayed default and choose Apply now. Verify the slider changes
   and the status reports the actual voice-session count. Disable voice defaults
   and verify Apply now leaves voice alone.
5. Test Pause/Resume, closing to tray, reopening, Exit and duplicate app launches.
6. Restart Discord, switch outputs, unplug/reconnect a USB headset, sleep/wake and
   restart the Windows Audio service. Check role detection and recovery.
7. Copy diagnostics. Confirm it describes the observed sessions and excludes
   usernames, executable paths, command lines, messages and recordings.
8. Run Install.cmd. Confirm Start menu launch and optional hidden Windows startup.
   Exit, install a newer build, and verify preferences and startup are preserved.
9. Uninstall from Windows Installed apps with the app closed. Confirm startup and
   owned shortcuts are gone and preferences remain. Verify a portable copy still
   works. Never delete a folder holding unrelated files as part of testing.

## Suggested coverage

Record Windows version, Discord version/channel, app version, output device,
notification/voice session counts, result and tester. Use at least Windows 10 and
11, speakers, a USB headset, Bluetooth headphones, Stable/PTB/Canary where
available, and a multi-output configuration. Streaming/media audio needs explicit
testing because non-notification Discord sessions share the voice role.

Do not label an untested row as passing. Keep release beta status until this
matrix and the installer lifecycle have been exercised.
