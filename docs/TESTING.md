# Beta test checklist

Automated policy, settings, routing, diagnostic privacy and executable checks run
through `tests/Test.ps1`. `tests/Render.ps1` constructs the settings and diagnostic
windows.
These are not substitutes for Core Audio tests.

## Recorded validation

| Check | State |
| --- | --- |
| Original notification ceiling on the first test PC | User confirmed on earlier builds |
| Voice Apply now correction | User confirmed on the previous fix build |
| Notification-only beta policy/settings/routing/diagnostic tests | Pending Windows CI |
| Notification-only beta settings/diagnostic layout | Pending Windows desktop testing |
| Installer dry-run and unmanaged-uninstall rejection | Passed; no installation performed |
| Installer/update/uninstall full lifecycle | Pending Windows desktop testing |
| Second independent Windows PC | Pending |
| Hardware/device matrix below | Pending |

## Release candidate procedure

1. Start from a new Windows profile or temporarily back up the saved settings.
   Confirm there is no voice default control and the saved notification ceiling
   is carried over from an older settings file.
2. With Discord idle, play a notification and verify its mixer level is capped.
   Try a notification level below the cap and check it stays there.
3. Join a call. Set voice to a different level from the cap, trigger a ping,
   and verify voice stays at the chosen level. Repeat after restarting Discord.
4. On a setup where Windows reports linked sessions, check the skip reason in
   Diagnostics and verify neither slider moves. If both sounds share a single
   indistinguishable stream, pause the app and record the Discord version.
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

## Windows System Sounds

With Discord closed, enable the System Sounds cap at 25%. Play a Windows
sound from the Sound control panel and check the System Sounds mixer entry.
Raise that entry above the ceiling and verify it is capped again. Check that
a lower level stays lower and that app and master sliders are unchanged.
Disable the cap, raise System Sounds and verify the keeper leaves it alone.
Repeat with Discord open and distinct settings for each cap.

## Suggested coverage

Record Windows version, Discord version/channel, app version, output device,
notification candidate and skip counts, result and tester. Use at least Windows 10 and
11, speakers, a USB headset, Bluetooth headphones, Stable/PTB/Canary where
available, and a multi-output configuration. Streaming/media audio needs explicit
testing to check whether media and notifications share an audio session.

Do not label an untested row as passing. Keep release beta status until this
matrix and the installer lifecycle have been exercised.
