# Privacy

Volume Keeper operates locally. It does not use the network, record audio, inspect
Discord conversations, or upload telemetry. It reads Discord process command
lines locally to map Windows audio sessions to notification versus voice/media
roles. It changes only those sessions' mixer volumes when enabled.

Settings live under `%LOCALAPPDATA%\DiscordVolumeKeeper`. Local error logs may
include error descriptions and paths; inspect them before sharing. Diagnostic
reports intentionally contain only app/OS version, timestamp, role, process ID,
output index, volume and last-operation category. Export or copy is user initiated.

The per-user installer writes only its application folder, Start menu shortcut,
uninstall registration and an existing app-owned startup entry. Enabling startup
in the app writes its per-user Run entry. Uninstall retains preferences and logs.
