# ADR 0004: Cross-Platform Distribution, Packaging, and Updates

## Status

Accepted

## Date

2026-04-19

## Context

MarkMello is moving from a development-phase desktop application to a product that should be installable and updateable by end users.

The product goals for this stage are:

- publish releases through GitHub Releases;
- provide a clear install flow on Windows, macOS, and Linux;
- keep the fast path simple inside the app;
- support Markdown file opening through native OS integration where it is realistic and supportable;
- minimize surprise by following the native expectations of each operating system.

The product requirements and decisions gathered for this stage are:

- Windows should use a downloadable installer binary;
- the Windows installer should be per-user;
- the update check should download the update artifact rather than only opening the release page;
- releases should live on GitHub;
- the app should integrate with `.md` files and shell context where the platform supports it cleanly;
- code signing should be part of the release pipeline.

## Decision

MarkMello will use a platform-specific distribution strategy under one shared release source and one shared update-discovery model.

- GitHub Releases will be the single source of truth for public release artifacts.
- The app will check GitHub Releases to determine whether a newer version exists.
- The app will choose a release asset by current platform and architecture.
- The app will download the correct asset directly from GitHub Releases.
- The install and post-download flow will remain native to each OS instead of forcing one cross-platform installer model.

### Windows

Windows will use a signed per-user installer executable.

- Packaging technology: Inno Setup.
- Release artifacts: one installer per supported architecture.
- Initial architecture scope: `win-x64` and `win-arm64`.
- Installation model: per-user.
- Installer responsibilities:
  - let the user choose an install folder;
  - install the app in that folder;
  - create a desktop shortcut;
  - register MarkMello as a handler available for Markdown files;
  - add shell integration such as `Open with MarkMello`;
  - support standard uninstall and upgrade behavior.

The updater on Windows may download the installer and launch it.

Important constraint:

- MarkMello should register itself as an available handler for Markdown files, but it should not try to silently force itself as the default `.md` application on modern Windows systems.

### macOS

macOS will use signed and notarized application distribution outside the Mac App Store.

- Packaging technology: signed `.app` bundle distributed in a signed and notarized `.dmg`.
- Release artifacts: one DMG per supported architecture.
- Initial architecture scope: `macos-arm64` and `macos-x64`.
- Signing model: Apple Developer ID.
- Trust model: notarization required for normal Gatekeeper experience.

The updater on macOS will download the correct DMG and then hand off to the native installation flow.

MarkMello should expose Markdown document handling through application bundle metadata rather than through installer-specific hacks. Finder integration should follow normal macOS document registration.

Important constraint:

- macOS should not imitate Windows-style installer behavior such as desktop shortcut creation or silent in-place replacement of the currently running app.

### Linux

Linux will start with an upstream-friendly direct-download format rather than a distro-specific installer.

- Packaging technology for the first supported Linux release: AppImage.
- Release artifacts: one AppImage per supported architecture.
- Initial architecture scope: `linux-x86_64`, with `linux-aarch64` optional if there is real demand.
- Desktop integration should be provided through desktop entry and MIME registration metadata that is compatible with Linux desktop standards.

The updater on Linux will download the new AppImage and then reveal or hand off the file for the user to replace the previous binary.

Important constraints:

- MarkMello should advertise Markdown support through desktop metadata.
- MarkMello should not attempt to force itself as the default Markdown application across Linux desktops.
- Linux will not use a fake universal installer flow that tries to normalize all distributions.

Follow-up packaging may later add `.deb` packages if deeper distro integration becomes necessary. This is explicitly out of scope for the first packaging pass.

## Release Matrix

The intended public release matrix is:

- `MarkMello-setup-win-x64.exe`
- `MarkMello-setup-win-arm64.exe`
- `MarkMello-macos-arm64.dmg`
- `MarkMello-macos-x64.dmg`
- `MarkMello-linux-x86_64.AppImage`

Additional Linux artifacts may be added later without changing the core update model.

## Update Model

The application update feature will follow one shared discovery flow and three platform-native installation endings.

Shared discovery flow:

1. User opens `Settings -> Updates`.
2. The app checks the latest suitable release from GitHub Releases.
3. The app compares the current version with the latest published version.
4. If no newer version exists, the app shows that the current version is up to date.
5. If a newer version exists, the app offers download of the correct asset for the current platform and architecture.

Platform-specific completion:

- Windows: download installer and offer to launch it.
- macOS: download DMG and offer to open it.
- Linux: download AppImage and offer to reveal it in the file system.

The app should not perform background network checks on startup. Update checks are manual and user-initiated.

## Rationale

This approach keeps the product aligned with its desktop-reader identity while avoiding fragile cross-platform installer abstractions.

- It preserves one public release source.
- It gives users a native install story on each OS.
- It keeps update logic understandable inside the app.
- It avoids over-promising full automatic update behavior where the platform discourages it.
- It keeps future packaging expansion possible without invalidating the initial release model.

## Consequences

### Positive

- GitHub Releases becomes the single place for publishing and update discovery.
- Windows gets the richest installer integration where that model fits best.
- macOS distribution stays compatible with Gatekeeper expectations.
- Linux distribution stays simple and upstream-controlled for the first release.
- The update UI can stay mostly uniform even though installation differs by platform.

### Negative

- Packaging and signing pipelines will be different per platform.
- The updater cannot end with the exact same UX on every OS.
- Linux desktop integration will be less uniform than Windows integration.
- macOS release automation will require Apple signing and notarization credentials.

### Accepted Tradeoffs

- We prefer platform-native install behavior over a fake single installer story.
- We prefer manual update checks over background update infrastructure.
- We prefer direct GitHub-hosted assets over a separate update backend in the first release.
- We accept that file association support means "registered and available" rather than "forcibly default" on platforms that protect that choice.

## Out of Scope

The following items are not part of this decision:

- automatic silent background updates;
- mandatory self-replacement of the running app after download;
- Flatpak or Flathub distribution in the first packaging pass;
- `.deb` or `.rpm` packaging in the first packaging pass;
- a custom backend for update manifests;
- auto-setting MarkMello as the default Markdown handler across all platforms.

## Implementation Notes

The expected implementation order is:

1. Windows packaging baseline with Inno Setup.
2. GitHub Release publication format and asset naming.
3. Manual in-app update check against GitHub Releases.
4. Windows download-and-launch installer flow.
5. macOS signing, notarization, and DMG distribution.
6. Linux AppImage packaging and download flow.

## References

- Apple Developer: [Developer ID](https://developer.apple.com/support/developer-id/)
- Apple Developer: [Signing your apps for Gatekeeper](https://developer.apple.com/developer-id/)
- Apple Developer: [Notarizing macOS software before distribution](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)
- Apple Developer: [CFBundleDocumentTypes](https://developer.apple.com/documentation/BundleResources/Information-Property-List/CFBundleDocumentTypes)
- GitHub Docs: [REST API for releases](https://docs.github.com/en/rest/releases/releases)
- Inno Setup: [DefaultDirName](https://jrsoftware.org/ishelp/topic_setup_defaultdirname.htm)
- Inno Setup: [Icons Section](https://jrsoftware.org/ishelp/topic_iconssection.htm)
- Inno Setup: [ChangesAssociations](https://jrsoftware.org/ishelp/topic_setup_changesassociations.htm)
- Inno Setup: [AppId](https://jrsoftware.org/ishelp/topic_setup_appid.htm)
- Microsoft: [File type and URI associations model](https://learn.microsoft.com/en-us/windows/compatibility/file-type-and-protocol-associations-model)
- Microsoft: [SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)
- AppImage Docs: [Welcome to the AppImage documentation](https://docs.appimage.org/index.html)
- AppImage Docs: [Desktop integration](https://docs.appimage.org/reference/desktop-integration.html)
- AppImage Docs: [Signing AppImages](https://docs.appimage.org/packaging-guide/optional/signatures.html?highlight=signing)
- freedesktop.org: [Desktop Entry Specification - Registering MIME Types](https://specifications.freedesktop.org/desktop-entry/latest/mime-types.html)
- freedesktop.org: [Association between MIME types and applications - Default Application](https://specifications.freedesktop.org/mime-apps-spec/latest/default.html)
