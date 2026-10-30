# Third-party notices

This application contains C# and PowerShell implementation developed for this
project. It uses Windows Core Audio COM interfaces and .NET Framework components
provided by Windows. No NuGet packages, downloaded helper executables, fonts or
Discord assets are bundled. The icon was drawn for this project.

The idea of distinguishing Discord notification audio by the
`audio.mojom.AudioService` process was informed by
[bradf4/Discord_Set_Notification_Volume](https://github.com/bradf4/Discord_Set_Notification_Volume).
That repository's C++ source and binary are not included. Its root did not list
a license during the release review; do not copy its source into this project
without confirming permission. Attribution here acknowledges the reference and
does not imply a license grant to its code.

Windows interface declarations express the public API contracts; the application
and event-handling implementation are maintained here. Discord is a third-party
product. Arenngor's Volume Keeper is independent and is not endorsed by Discord.
