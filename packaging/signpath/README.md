# SignPath for Windows

The SignPath project slug is `MarkMello`. The existing policies are
`test-signing` and `release-signing`. Private keys stay in SignPath.

## One-time configuration

In the SignPath project's Artifact Configurations, create these configurations
using Custom XML (or edit existing configurations with matching slugs):

| Slug | XML source | Uploaded ZIP contents |
| --- | --- | --- |
| `windows-app` | `windows-app.xml` | `MarkMello.exe`, optional `MarkMello.dll` |
| `windows-installer` | `windows-installer.xml` | One `MarkMello-setup-win-*.exe` |

The GitHub upload action creates the ZIP envelope. Do not ZIP the files again:
that would add an extra archive level that these configurations do not match.
Only MarkMello-owned binaries are signed; dependency DLLs keep their original
signatures. App binaries are signed before Inno Setup embeds them in the installer.

Both configurations require `product-version` and `file-version` parameters.
They enforce `ProductName=MarkMello` and exact PE product/file versions before
signing. The workflow uses the same release version for the application and
installer's textual ProductVersion, and the same four-part Windows version for
FileVersion. The composite action passes both values to SignPath; local CI
checks reject mismatches before uploading an unsigned artifact.

Inno Setup pads the text fields with spaces. The packager terminates those
strings in their existing VERSIONINFO allocations and recomputes the PE
checksum before signing. It preserves section offsets and the appended
installer payload, refuses signed input, and rejects unexpected or truncated
metadata instead of weakening the SignPath restrictions.

After changing these XML files, update the matching Artifact Configurations in
the SignPath panel before running the workflow. Repository files do not update
the remote configuration automatically. Keep the Foundation's policy and
trusted-origin restrictions enabled.

Add the predefined GitHub.com Trusted Build System to the organization and link
it to the MarkMello project. Preserve the Foundation's release policy restrictions.
All jobs leading to the signing request use GitHub-hosted runners.

The existing `CI builds` user must be a Submitter for the signing policies.
Its notification address must be confirmed. Obtain its API token and save it in
the GitHub repository as the Actions secret `SIGNPATH_API_TOKEN`.
Do not commit the token or copy it into logs.

Add the repository Actions variable `SIGNPATH_ORGANIZATION_ID` using the ID shown
on the signing policy's CI Integration tab. Organization IDs are not secrets.

## First test

Push the `codex/signpath-windows` branch to start the first test. After the workflow
is merged into the default branch, `Test SignPath Windows` can also be run manually.
It builds Native AOT app
binaries and Inno Setup installers for win-x64 and win-arm64, signs both stages
through the `test-signing` policy, and verifies the resulting signatures.

This workflow does not create, update, or publish a GitHub Release. Download the
`windows-win-x64` / `windows-win-arm64` workflow artifacts to inspect the installers.
Unsigned intermediate artifacts are retained for three days for troubleshooting.
Test signatures are expected to be untrusted on customer machines.

The Windows installer displays the bundled `packaging/PRIVACY.txt` offline.
Automatic update checks are enabled by default. Its checked Additional Tasks
option reads and saves the same per-user preference as Settings > Updates.
Existing choices survive upgrades, including changes made from the application.
The installer invokes the real Native AOT executable's preference-only commands
as the original user, even when installing for all users with elevation.
Those commands exit before initializing Avalonia or contacting GitHub.

For silent installs, `/AUTOUPDATES=on` or `/AUTOUPDATES=off` sets the choice
explicitly; otherwise it is preserved. Native `/TASKS` and `/MERGETASKS`
overrides remain supported. The original user must be available for saving the
preference; failures stop Setup rather than silently changing the choice.
Turning checks off cancels delayed discovery in all open windows. Turning them
on schedules a new shared delay. Manual checking and downloading remain
available regardless of that choice.

Verification uses the Windows Authenticode trust provider to check the embedded
signature, not merely the presence of a certificate. Only the expected test
signer's untrusted-root result is tolerated during test-signing. System trust
stores are not modified.

After a successful test, send Philipp the GitHub Actions run URL and ask him to
review the setup and finish provisioning the production certificate.

## Releases

The `Release Desktop` workflow calls the same Windows build with
`release-signing`. There is no unsigned or PFX fallback in CI. Missing credentials,
invalid policies/certificates, signing failures, and signature verification
failures stop the Windows job before its assets are uploaded to a release.
The final release publication waits for successful builds.

Release-signed installers use a trusted `SignPath Foundation` certificate.
The release policy remains unusable until its production certificate is valid.
SHA256 checksums are calculated after the installer is signed.

The standalone `packaging/windows/sign-files.ps1` remains available for an
explicit local PFX signing operation; GitHub Actions no longer calls it.

## References

- https://docs.signpath.io/trusted-build-systems/github
- https://docs.signpath.io/artifact-configuration/syntax
- https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust
