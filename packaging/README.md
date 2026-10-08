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
- `signpath/README.md` describes SignPath CI signing for the app and Windows installers.
- `windows/sign-files.ps1` remains available for explicit local PFX signing.
- `windows/markmello-installer.ico` is the installer icon generated from the shared master icon.
- The installer registers MarkMello as an available `.md` handler and adds `Open with MarkMello`-style shell integration without forcing a system-wide default.
- `.github/workflows/release-windows.yml` coordinates desktop releases through separate platform build workflows.

GitHub Actions flow:

- pushing a `v*` tag runs verify -> draft release creation -> Windows installers + macOS DMGs -> asset upload -> publish
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
- `macos/MarkMello.icns` is the bundle icon generated from the shared master icon.
- `macos/build-app-bundle.sh` assembles an unsigned `.app` bundle from a `dotnet publish` folder.
- `macos/build-dmg.sh` wraps that bundle into an unsigned `.dmg` for GitHub Releases.
- The template declares Markdown document handling through bundle metadata, not installer hacks.
- Replace `$(MARKMELLO_BUNDLE_ID)`, `$(MARKMELLO_VERSION)`, and `$(MARKMELLO_BUILD_NUMBER)` in the release pipeline before building the DMG.
- Without an Apple Developer account, the macOS release stays unsigned and non-notarized. Users should expect Gatekeeper to require an explicit first-run approval after download.

## Linux

- `linux/build-appimage.sh` assembles the AppDir from a published app folder and runs `appimagetool`.
- `linux/markmello.desktop` is the desktop entry baseline for AppImage packaging.
- `linux/markmello.png` is the launcher icon generated from the shared master icon.
- The desktop entry advertises Markdown support through `MimeType=text/markdown;` and includes AppImage-specific metadata keys.
- The first packaging pass intentionally stays AppImage-first instead of adding distro-specific installers.

## Shared icon source

- `packaging/assets/base-803-crop.png` is the icon master tracked in the repository.
- `packaging/generate-icons.py` regenerates Windows `.ico`, macOS `.icns`, and Linux `.png` assets from that master.
- The generator preserves the original artwork framing and only resizes it for platform-specific formats.

Example:

```powershell
$py = "C:\Users\drmar\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
& $py .\packaging\generate-icons.py
```

## Update source

The in-app `Settings -> Updates` flow reads GitHub Releases coordinates from the desktop build metadata:

- `MarkMelloReleaseOwner`
- `MarkMelloReleaseRepo`

Those values default in `src/MarkMello.Desktop/MarkMello.Desktop.csproj`, and can be overridden in CI or local release builds with MSBuild properties.

## Signing and notarization

This baseline prepares the repository for signed distribution, but actual signing credentials stay out of source control:

- Windows CI signing uses SignPath before packaging and before uploading the installer.
- macOS signing and notarization should be added later, once Apple Developer ID credentials are available.

### GitHub Actions configuration for Windows signing

- `SIGNPATH_API_TOKEN` — Actions secret containing the existing CI builds user's token.
- `SIGNPATH_ORGANIZATION_ID` — Actions variable containing the SignPath organization ID.
- `packaging/signpath/README.md` — artifact XML, initial test, and production setup.

The `Test SignPath Windows` workflow signs both Windows architectures without
creating or updating a GitHub Release. Normal releases require `release-signing`.
The old `WINDOWS_SIGNING_CERT_BASE64` / `WINDOWS_SIGNING_CERT_PASSWORD` secrets
are no longer used by CI.
