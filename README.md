# Arenngor's Volume Keeper

Keep Discord notification sounds below a comfortable volume. An independent,
lightweight Windows tray app with optional voice-volume defaults.

**Status: 0.3.0-beta.1.** The notification cap and voice Apply now behavior have
been exercised on the original user's PC. Multi-PC and installer lifecycle testing
are still pending; see [the test checklist](docs/TESTING.md).

## Use the app

Extract a release ZIP before opening anything inside it. Either run
`DiscordVolumeKeeper.exe` as a portable app, or run `Install.cmd` to install it
for your Windows account and create a Start menu shortcut. No administrator
rights are required. Requires Windows 10/11 with .NET Framework 4.x.

Set **Notification ceiling** and click **Save settings**. Notifications already
quieter than the ceiling stay quieter. Mute state is preserved.

**Default voice volume is off for new users.** If enabled, it sets each new voice
session once, then allows manual adjustment. **Apply now** applies the displayed
voice default to existing sessions when the checkbox is enabled. With it off,
Apply now only enforces the notification ceiling. Voice/media sessions are found
by process identity; Discord streaming audio may also be affected when voice
defaults are enabled.

Closing the window leaves the purple volume-bars icon in the notification area
near the clock. Double-click it for settings; right-click for Pause, Diagnostics,
About or Exit. Start with Windows is optional and starts hidden. While paused,
Apply now does not alter audio; resume first.

## Install, update or remove

- Exit the running app before an update. Extract the new release and run
  `Install.cmd`. The per-user installer keeps preferences and retains the previous
  executable as a rollback copy. Updates are manual; the app downloads nothing.
- An installed copy can be removed through Windows Installed apps or its
  `Uninstall.cmd`. Exit it first. Preferences are retained for reinstalling.
- For portable copies, uncheck Start with Windows and save, exit, then delete the
  extracted folder. If you move a portable app, save Start with Windows again to
  refresh its stored executable path.
- Existing users keep settings in `%LOCALAPPDATA%\DiscordVolumeKeeper`. Very old
  settings without a voice-enabled flag retain the old enabled preference.

## Diagnostics and bug reports

Open **Diagnostics** and choose Copy report or Save report. Include the report,
your Discord version and the steps that caused the problem when reporting it to
the person who supplied the app or a future repository issue tracker. There is no
public issue URL configured yet.

The report includes version, Windows version, role, process ID, output index and
volumes from the last audio-event snapshot. It excludes usernames, executable
paths, command lines, messages and audio. Nothing is uploaded automatically.
Local error logs are separate; inspect them before sharing.

## Limits

Desktop Discord Stable, PTB and Canary are recognized. Browser Discord is not.
The app uses Windows audio events, not a repeating volume-enforcement timer.
The first sound may start before an event is handled. Windows speaker volume
still controls the actual loudness; this is a mixer ceiling, not a peak limiter.
Discord updates or linked mixer sessions can change how role detection works.
Check Diagnostics if voice sessions are missing or the wrong slider moves.

Builds are currently unsigned. A release archive includes a SHA-256 checksum for
integrity checks; it does not authenticate the publisher. Signing support exists
but requires the publisher's code-signing certificate.

## Build from source

On Windows with Git and the .NET Framework C# compiler:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package.ps1
```

The executable is written to `build/`; release files to `dist/`. Source control
excludes binaries, personal settings, logs and certificates. The code uses only
Windows/.NET Framework APIs. CI configuration is included for a future GitHub
remote. See [release instructions](docs/RELEASING.md) and [privacy](docs/PRIVACY.md).

MIT licensed. See [LICENSE](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md).
