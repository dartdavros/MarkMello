# Packaging Baseline

This folder captures the first release baseline from `ADR-0004`.

## Release matrix

- `MarkMello-setup-win-x64.exe`
- `MarkMello-setup-win-arm64.exe`
- `MarkMello-macos-arm64.dmg`
- `MarkMello-macos-x64.dmg`
- `MarkMello-linux-x86_64.AppImage`

## Windows

- `windows/MarkMello.iss` is the per-user Inno Setup installer.
- `windows/build-installer.ps1` compiles the installer from a published app folder.
- `windows/sign-files.ps1` signs published binaries and installers when a PFX certificate is provided.
- The installer registers MarkMello as an available `.md` handler and adds `Open with MarkMello`-style shell integration without forcing a system-wide default.
- `.github/workflows/release-windows.yml` is the Windows-first GitHub Releases pipeline.

GitHub Actions flow:

- pushing a `v*` tag runs verify -> draft release creation -> both Windows installers -> asset upload -> publish
- tags with prerelease suffixes like `v0.6.0-beta.1` stay GitHub prereleases and are not marked as latest
- `workflow_dispatch` can create or refresh a release manually and optionally keep it as a draft after upload

Example:

```powershell
dotnet publish .\src\MarkMello.Desktop\MarkMello.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false `
  -p:MarkMelloReleaseOwner=dartdavros `
  -p:MarkMelloReleaseRepo=MarkMello `
  -o .\publish\win-x64

.\packaging\windows\build-installer.ps1 `
  -PublishDir .\publish\win-x64 `
  -RuntimeId win-x64 `
  -Version 0.6.0
```

## macOS

- `macos/Info.plist` is a template for the signed `.app` bundle metadata.
- The template declares Markdown document handling through bundle metadata, not installer hacks.
- Replace `$(MARKMELLO_BUNDLE_ID)`, `$(MARKMELLO_VERSION)`, and `$(MARKMELLO_BUILD_NUMBER)` in the release pipeline before building and notarizing the DMG.

## Linux

- `linux/markmello.desktop` is the desktop entry baseline for AppImage packaging.
- The desktop entry advertises Markdown support through `MimeType=text/markdown;` and includes AppImage-specific metadata keys.
- The first packaging pass intentionally stays AppImage-first instead of adding distro-specific installers.

## Update source

The in-app `Settings -> Updates` flow reads GitHub Releases coordinates from the desktop build metadata:

- `MarkMelloReleaseOwner`
- `MarkMelloReleaseRepo`

Those values default in `src/MarkMello.Desktop/MarkMello.Desktop.csproj`, and can be overridden in CI or local release builds with MSBuild properties.

## Signing and notarization

This baseline prepares the repository for signed distribution, but actual signing credentials stay out of source control:

- Windows signing should be injected into the release pipeline when calling `signtool`.
- macOS signing and notarization should be applied during DMG creation with Apple Developer ID credentials.

### GitHub Actions secrets for Windows signing

If you want the Windows workflow to sign artifacts, configure:

- `WINDOWS_SIGNING_CERT_BASE64` — base64-encoded `.pfx`
- `WINDOWS_SIGNING_CERT_PASSWORD` — password for that certificate
