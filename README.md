# MarkMello

Fast, viewer-first Markdown reader for the desktop. .NET 9 + Avalonia 12.

## Status

**M3 complete, M4 started — native markdown viewer + persisted reading settings.**

- Custom title bar (Windows), native chrome (macOS / Linux)
- States: Welcome / Viewing / LoadError / DragHovering
- Hover-reveal top bar (theme toggle + reading settings), status bar, reading progress
- Open file: `Ctrl+O`, drag & drop, command-line argument
- Reload: `F5` / `Ctrl+R`
- Theme behavior: first launch follows system theme; after explicit user choice the quick toggle switches between Light and Dark and restores that choice on the next launch
- Native markdown viewer: headings, paragraphs, lists, quotes, hr, code, links, tables, images
- Reading surface: centered column + serif typography, document-wide text selection, native Avalonia render path
- Reading settings: `Ctrl+,` / `Cmd+,`, live font mode + size + line height + content width, safe JSON persistence in the platform config directory

## Solution layout

```
src/
├── MarkMello.Domain          // models, enums, no dependencies
├── MarkMello.Application     // use cases, contracts (IDocumentLoader, IFilePicker, ...)
├── MarkMello.Infrastructure  // file IO, settings, metrics, command-line activation
├── MarkMello.Presentation    // App, MainWindow, ViewModels, themes, services
└── MarkMello.Desktop         // entry point, DI composition root
```

## Prerequisites

- Windows 10/11 (primary target), macOS or Linux also supported
- .NET SDK 9.0+ (`dotnet --version` → 9.0.x)
- PowerShell 5.1 / 7+

## Build & run (PowerShell)

```powershell
# из корня репозитория
dotnet restore .\MarkMello.sln
dotnet build .\MarkMello.sln -c Debug
dotnet run --project .\src\MarkMello.Desktop\MarkMello.Desktop.csproj
```

## Run with a file (command-line activation)

```powershell
dotnet run --project .\src\MarkMello.Desktop\MarkMello.Desktop.csproj -- .\sample.md
```

или с собранным бинарём:

```powershell
.\src\MarkMello.Desktop\bin\Debug\net9.0\MarkMello.exe .\sample.md
```

## Verification checklist (viewer baseline)

- [x] Solution собирается без warnings as errors
- [x] `dotnet run` без аргументов открывает Welcome state
- [x] `dotnet run -- path\to\file.md` открывает файл сразу
- [x] `Ctrl+O` показывает file picker
- [x] Drag & drop `.md` файла в окно открывает его
- [x] Несуществующий путь → LoadError state, окно не падает
- [x] `Esc` сбрасывает LoadError → возвращает в Welcome или Viewing
- [x] `F5` / `Ctrl+R` перечитывает текущий файл
- [x] Hover над окном проявляет top bar и status bar
- [x] Theme toggle (◐ кнопка) использует системную тему на первом запуске, затем быстро переключает Light / Dark
- [x] `Ctrl+,` / `Cmd+,` или кнопка `Aa` открывает settings panel
- [x] Font mode / size / line height / content width применяются live и восстанавливаются при следующем запуске
- [x] В консоли видны 3 stage'а: `AppBootstrap`, `FirstWindow`, `ReadableDocument`
- [x] **Editor-зависимостей в графе DI нет** (constitution §4)

## Что в консоли при запуске с файлом

```
[startup] AppBootstrap            X.X ms
[startup] FirstWindow            XX.X ms
[startup] ReadableDocument       XX.X ms
```

## Полезные команды

```powershell
# clean build
dotnet clean .\MarkMello.sln
Get-ChildItem -Recurse -Include bin,obj | Remove-Item -Recurse -Force

# release build
dotnet build .\MarkMello.sln -c Release

# self-contained publish (Windows x64)
dotnet publish .\src\MarkMello.Desktop\MarkMello.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o .\publish\win-x64
```

## Roadmap

См. `implementation-plan.md`. Текущий следующий шаг: продолжать **M4** —
дополировать settings UX и theme/preferences flow, затем переходить к **M5**.

## Architecture references

- `vision.md` — что мы строим
- `constitution.md` — 12 immutable принципов
- `architecture.md` — слои, stage'ы запуска, error handling
- `avalonia-design-translation.md` — перевод React-прототипа в Avalonia
- `implementation-plan.md` — milestones M0 → M6
