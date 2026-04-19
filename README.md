# MarkMello

> A fast, viewer-first Markdown reader for your desktop.

MarkMello opens local `.md` files quickly and keeps the document at the center of the experience. No workspace setup, no project tree, no sync account, and no editor overhead until you ask for it.

---

## Why it exists

Most Markdown tools assume you want an editor, a sidebar, a repo, and a lot of UI around the file. MarkMello is built for the simpler and more common case: you just want to open a Markdown document and read it comfortably.

- Fast startup and direct file opening
- A centered reading surface with document-wide text selection
- Local-first behavior with no network requirement to read your files
- Lazy edit mode instead of editor-first startup

## What it does

- Opens Markdown files from the file picker, drag and drop, or the command line
- Renders headings, paragraphs, lists, quotes, code blocks, tables, images, and links
- Persists reading preferences such as theme, font mode, font size, line height, and content width
- Supports a split edit mode with save, save as, and dirty-state handling
- Provides manual GitHub Releases update checks in `Settings -> Updates`

## Install

Download the latest assets from the [latest release](../../releases/latest).

### Windows

1. Download `MarkMello-setup-win-x64.exe` or `MarkMello-setup-win-arm64.exe`, depending on your machine.
2. Run the installer.
3. Launch MarkMello from the Start menu or open a `.md` file with it.

### macOS

1. Download `MarkMello-macos-arm64.dmg` for Apple Silicon or `MarkMello-macos-x64.dmg` for Intel Macs.
2. Open the DMG.
3. Drag `MarkMello.app` into `Applications`.
4. Launch the app from `Applications`.

Current macOS builds are unsigned and not notarized. On first launch, Gatekeeper may block the app. If that happens, either right-click the app and choose `Open`, or allow it in `System Settings -> Privacy & Security`.

### Linux

If a Linux AppImage is attached to a release, install it like this:

```bash
chmod +x MarkMello-linux-x86_64.AppImage
./MarkMello-linux-x86_64.AppImage
```

If no Linux asset is published for the release you want, build from source instead.

## Open a file

You can open a document in three ways:

1. Open a `.md` file from your file manager
2. Drag a Markdown file onto the window
3. Press `Ctrl+O` or `Cmd+O` inside the app

Command-line activation also works:

```bash
dotnet run --project ./src/MarkMello.Desktop/MarkMello.Desktop.csproj -- ./sample.md
```

## Reading and editing

MarkMello starts as a reader. The reading surface is centered, text selection works across the whole document, and the chrome stays out of the way until you need it.

Reading preferences apply live and persist between launches:

- Theme: System, Light, Dark
- Font mode: Serif, Sans, Mono
- Font size
- Line height
- Content width

Edit mode is intentionally secondary. Press `Ctrl+E` or `Cmd+E` to open the split editor only when you need it.

## Keyboard shortcuts

- `Ctrl+N` / `Cmd+N` — create a new Markdown document
- `Ctrl+O` / `Cmd+O` — open a file
- `Ctrl+E` / `Cmd+E` — toggle edit mode
- `Ctrl+S` / `Cmd+S` — save
- `Ctrl+Shift+S` / `Cmd+Shift+S` — save as
- `Ctrl+R` / `Cmd+R` / `F5` — reload the current file
- `Ctrl+,` / `Cmd+,` — toggle reading preferences
- `Escape` — clear the current load error state

## Build from source

Prerequisites:

- .NET SDK 9.0 or newer

Build and run:

```bash
dotnet restore ./MarkMello.sln
dotnet build ./MarkMello.sln -c Debug
dotnet run --project ./src/MarkMello.Desktop/MarkMello.Desktop.csproj
```

Try the included sample document:

```bash
dotnet run --project ./src/MarkMello.Desktop/MarkMello.Desktop.csproj -- ./sample.md
```

Create a local Release build:

```bash
dotnet build ./MarkMello.sln -c Release
```

## Repository layout

```text
src/
├── MarkMello.Domain
├── MarkMello.Application
├── MarkMello.Infrastructure
├── MarkMello.Presentation
└── MarkMello.Desktop
```

## Packaging and release notes

- Packaging notes live in [packaging/README.md](packaging/README.md)
- Desktop release automation lives in [.github/workflows/release-windows.yml](.github/workflows/release-windows.yml)
- Windows installers and macOS DMGs are published through GitHub Releases
- The in-app updater checks GitHub Releases manually from `Settings -> Updates`
