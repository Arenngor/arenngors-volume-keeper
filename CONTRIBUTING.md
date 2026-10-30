# Contributing

Build with scripts/Build.ps1 and run tests/Test.ps1 before submitting a change.
For UI changes, render and inspect both windows with tests/Render.ps1. Describe
manual audio checks and their limits; do not claim a policy test verifies live
Discord session mapping. Keep the app independent of Discord client modification.

Bug reports should include app/Discord/Windows versions, the exported diagnostic
report, output device, reproduction steps and expected versus actual behavior.
Review the report before posting; do not include local logs without inspection.

Keep settings, executable paths, certificates, binaries and personal data out of
commits. Submit only code whose origin and license permit its inclusion.
