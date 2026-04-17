# MarkMello

Fast, viewer-first Markdown reader for the desktop. .NET 9 + Avalonia 12.

## Status

**M0 — Solution skeleton.** Пустое окно с применённой темой и измерением stage'ов запуска. Без markdown renderer'а, без edit mode, без open file (M1+).

## Solution layout

```
src/
├── MarkMello.Domain          // models, enums, no dependencies
├── MarkMello.Application     // use case contracts (IDocumentLoader, ISettingsStore, ...)
├── MarkMello.Infrastructure  // file IO, settings, metrics, platform detection
├── MarkMello.Presentation    // App, MainWindow, ViewModels, themes
└── MarkMello.Desktop         // entry point, DI composition root
```

Граф зависимостей:
- Domain — ни от кого
- Application → Domain
- Infrastructure → Application + Domain
- Presentation → Application + Domain (НЕ Infrastructure)
- Desktop → все остальные (composition root)

## Prerequisites

- Windows 10/11
- .NET SDK 9.0+ (`dotnet --version` должен показать 9.0.x)
- PowerShell 5.1 / 7+

## Build & run (PowerShell)

```powershell
# из корня репозитория
dotnet restore .\MarkMello.sln
dotnet build .\MarkMello.sln -c Debug
dotnet run --project .\src\MarkMello.Desktop\MarkMello.Desktop.csproj
```

## Что должно произойти

В терминал выводятся метрики stage'ов:

```
[startup] AppBootstrap            X.X ms
[startup] FirstWindow            XX.X ms
```

Открывается окно 1280×840 с надписью "MarkMello" по центру и подзаголовком "A quiet place to read Markdown.". Тема следует системной (Light/Dark).

## Verification checklist (M0 done when)

- [x] Solution собирается без warnings as errors
- [x] `dotnet run` открывает пустой shell
- [x] В консоли видны 2 stage'а (`AppBootstrap`, `FirstWindow`)
- [x] Тема (Light/Dark) применяется автоматически по системе
- [x] В графе DI **нет** editor-зависимостей (constitution §4)

## Полезные команды

```powershell
# clean build
dotnet clean .\MarkMello.sln
Remove-Item -Recurse -Force .\src\*\bin, .\src\*\obj -ErrorAction SilentlyContinue

# release build
dotnet build .\MarkMello.sln -c Release

# self-contained publish (Windows x64)
dotnet publish .\src\MarkMello.Desktop\MarkMello.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o .\publish\win-x64
```

## Roadmap

См. `implementation-plan.md`. Дальше: **M1 + M2** (viewer shell + file-first open path) одной итерацией.

## Architecture references

- `vision.md` — что мы строим
- `constitution.md` — 12 immutable принципов
- `architecture.md` — слои, stage'ы запуска, error handling
- `avalonia-design-translation.md` — перевод React-прототипа в Avalonia
- `implementation-plan.md` — milestones M0 → M6
