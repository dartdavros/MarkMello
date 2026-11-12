# Handoff: math formula rendering (in progress)

## Goal

Полноценный рендер формул в MarkMello (`$...$` инлайн и `$$...$$` блок), in-process,
без WebView/JS/сети. Владелец: нативный рендер, при ошибке LaTeX показывать исходник,
только режим чтения/превью.

## Done

- Диагноз: Markdig создаёт `MathInline`/`MathBlock`, но конвейер их выбрасывал
  (нет доменных типов и обработки). См. ADR `docs/adr/adr_0009_math_formula_rendering.md`.
- Domain: `MarkdownMathInline(Content, IsDisplay)`, `MarkdownMathBlock(Source)`.
- Парсер: `MarkdigMarkdownDocumentRenderer.Inlines.cs` (MathInline) и
  `.Blocks.cs` (MathBlock — **до** `FencedCodeBlock`, он наследуется от него).
- Движок: `Sylinko.CSharpMath.Avalonia 12.0.0` (net10.0, Avalonia 12, MIT),
  подключён в `MarkMello.Presentation.csproj`.
- `MarkdownMathRenderer` — Measure/Draw + потокобезопасный кэш замеров.
- Инлайн: `MarkdownMathTextRun` (DrawableTextRun) на 1-символьном placeholder’е;
  math-сегмент в `MarkdownDisplayLayoutModel` (исправлена обрезка `end` по `math.Range.Start`).
- Блок: `MarkdownMathBlockVisual` (`MarkdownDocumentView.Math.cs`).
- Plain-text прокинут во все потребители (text map, outline, slugger, comparer, clipboard).
- `sample.md`, README credits, ADR-0009.

## Verified

- Сборка solution: 0 errors, 0 warnings.
- Полный прогон `MarkMello.Presentation.Tests`: 567 зелёных + 11 новых math-тестов.
- Layout-probe: блочная панель y=63 h=72, следующий текст y=153 → пересечения нет.
- Визуально моделью (bynara/muse-spark-1.3-contributor): инлайн-формулы рисуются
  (E=mc², n, p, дробь, √, Σ, ∫), матрица рисуется.

## Remaining defects

1. **Блок «налезает» на следующий абзац** по скриншотам, при верной геометрии layout.
   Гипотезы: (а) отрисовка `MarkdownMathBlockVisual.Render` не учитывает смещение
   панели; (б) артефакт `PrintWindow` (захват устаревшего буфера). Проверять на живом
   окне обычным скриншотом экрана, не `PrintWindow`.
2. **Baseline инлайн-формулы** чуть занижен (run box ниже строки).
3. **Fallback для невалидного LaTeX** в некоторых путях показывает глиф вместо
   исходника: проверить, что `DrawSourceFallback` реально вызывается
   (движок ставит `ErrorMessage`, но `Measure` может вернуть валидный rect).

## Loose ends

- `tests/MarkMello.Presentation.Tests/MathVisualSaveTests.cs` — временный тест,
  рендерит `math-final.png`; удалить или переделать в стабильный тест.
- `math-visual-check.md` в корне — тестовый документ, удалить перед коммитом.
- `dist-exe/` — собранный exe; не коммитить (проверить `.gitignore`).
- Коммит не делался. Формат: `:sparkles: feat(math): render LaTeX formulas in-place`.

## How to work

1. `C:\Progra~1\dotnet\dotnet.exe build MarkMello.sln -v q --nologo`
2. Тесты только по изменённому:
   `C:\Progra~1\dotnet\dotnet.exe test tests\MarkMello.Presentation.Tests\MarkMello.Presentation.Tests.csproj --filter "FullyQualifiedName~MarkdownMath" -v q --nologo`
3. Публикация exe:
   `C:\Progra~1\dotnet\dotnet.exe publish src\MarkMello.Desktop\MarkMello.Desktop.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\dist-exe -v q --nologo`
4. Скриншот окна → показать модели с image input:
   `subagent(agent="general", model="bynara/muse-spark-1.3-contributor", prompt="Read image at <path> and describe...")`
