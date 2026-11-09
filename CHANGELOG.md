# Changelog

## 0.3.0-beta.3 (unreleased)

- Retain the separate Windows System Sounds cap and saved settings.
- Restore the optional Discord voice default and Apply now.
- Cap separate Discord notification sessions even when they share a saved
  identifier. Windows may restore the ping level to voice after a restart.

## 0.3.0-beta.2

- Add an optional, independent ceiling for Windows System Sounds.
- Remove voice defaults and keep the saved notification ceiling.
- Skip shared or ambiguous audio sessions and show the reason in Diagnostics.

## 0.3.0-beta.1

- Prepare the first versioned source repository and shareable release package.
- Default voice adjustments off for new users; preserve existing preferences.
- Add About, version metadata and privacy-conscious diagnostic reports.
- Retain the child-process voice detection fix and confirmed Apply now status.
- Add per-user install/update/uninstall scripts and release checksums.
- Add automated tests, Windows CI, release instructions and a manual test matrix.
- Include MIT license and reference/provenance notices.

This beta remains unsigned. Installer lifecycle and testing on additional PCs
remain pending.
