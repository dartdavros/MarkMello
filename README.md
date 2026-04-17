# MarkMello

Fast, viewer-first Markdown reader for the desktop. .NET 9 + Avalonia 12.

## Status

**M1 + M2 — Viewer shell + file-first open path.**

- Custom title bar (Windows), native chrome (macOS / Linux)
- States: Welcome / Viewing / LoadError / DragHovering
- Hover-reveal top bar (theme toggle), status bar, reading progress
- Open file: `Ctrl+O`, drag & drop, command-line argument
- Reload: `F5` / `Ctrl+R`
- Theme cycle: System → Light → Dark → System (persisted in-memory; disk persistence — M4)
- Reading surface: centered column + serif typography (raw text placeholder; markdown render — M3)

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

## Verification checklist (M1+M2 done when)

- [x] Solution собирается без warnings as errors
- [x] `dotnet run` без аргументов открывает Welcome state
- [x] `dotnet run -- path\to\file.md` открывает файл сразу
- [x] `Ctrl+O` показывает file picker
- [x] Drag & drop `.md` файла в окно открывает его
- [x] Несуществующий путь → LoadError state, окно не падает
- [x] `Esc` сбрасывает LoadError → возвращает в Welcome или Viewing
- [x] `F5` / `Ctrl+R` перечитывает текущий файл
- [x] Hover над окном проявляет top bar и status bar
- [x] Theme toggle (◐ кнопка) циклит System → Light → Dark
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

См. `implementation-plan.md`. Дальше: **M3** — настоящий markdown renderer
(Markdig parse layer + native Avalonia controls render layer).

## Architecture references

- `vision.md` — что мы строим
- `constitution.md` — 12 immutable принципов
- `architecture.md` — слои, stage'ы запуска, error handling
- `avalonia-design-translation.md` — перевод React-прототипа в Avalonia
- `implementation-plan.md` — milestones M0 → M6
