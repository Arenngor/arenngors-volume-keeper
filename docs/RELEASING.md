# Releases

1. Make the source changes and update VERSION. Check CHANGELOG.md.
2. Run scripts/Build.ps1, tests/Test.ps1, and tests/Render.ps1 on Windows.
3. Complete the live tests in TESTING.md and record which machines passed.
4. Optional publisher signing: install the Windows SDK signing tools, put a valid
   code-signing certificate in the certificate store, then run:

   ```powershell
   .\scripts\Sign.ps1 -CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT
   ```

   No certificate is bundled and no certificate has been purchased. Do not put
   private keys or credentials in Git. Signing was not performed for this beta.
5. Run scripts/Package.ps1 **after signing**, if applicable. It generates the ZIP
   and SHA-256 checksum. Verify and test the extracted release, not just build/.
6. Review staged files for user data and local paths. Commit the source. Tag the
   version only when approved for release. Publish binaries/checksums manually
   to the intended repository release page after a remote is configured.

The included CI runs builds/tests and uploads a build artifact. It does not
publish a release, sign binaries, or push a tag automatically.

## Local repository and a future GitHub remote

The first repository is local. To publish later, create the intended GitHub
repository, then add its verified URL as origin and push main. No account or
visibility choice is assumed here. Keep the default branch main.

Issue templates are ready. Once a remote exists, its Issues page can be added to
About and README. Do not invent a support URL before the repository is created.

## Licensing review

MIT is included for this project's implementation. THIRD_PARTY_NOTICES.md records
the reference used for the process-role heuristic. No reference repository source
or executable is bundled. If future contributions copy third-party code, review
its license and preserve required notices before merging it.
