# ADR-0009: Math formula rendering

## Status

Утвержден

## Date

2026-11-12

## Context

Документ из issue-скриншота терял формулы:

- `$E_i$`, `$n$`, `$p$` исчезали из текста, оставляя пустые скобки;
- блочная формула `$$...$$` пропадала целиком.

Диагноз (зафиксирован в памяти проекта): pipeline `MarkdigMarkdownDocumentRenderer`
строится через `UseAdvancedExtensions()`, который включает `UseMathematics()`.
Markdig поэтому корректно создаёт `MathInline`/`MathBlock`, но:

- `AddConvertedInline` не обрабатывал `MathInline` — узел терялся;
- `AddConvertedBlock` не обрабатывал `MathBlock` — блок терялся;
- в domain-модели (`RenderedMarkdownDocument.cs`) не было math-типов.

Итог: формулы исчезали молча. Это и есть корневая причина, а не «некрасивый рендер».

Требования:

- формулы должны отображаться во всех документах `.md`, включая дроби, индексы,
  суммы, интегралы, матрицы, радикалы;
- скорость отображения важна (viewer-first, принцип «Instant open over feature richness»);
- local-first: без сети, WebView, JavaScript runtime и внешних процессов
  (как в ADR-0005 для диаграмм);
- Native AOT направление сохраняется.

## Decision

Вводится first-class поддержка math в существующем markdown pipeline и вводится
встроенный in-process renderer на базе CSharpMath (Avalonia-форк).

### 1. Math не теряется на этапе AST -> domain

Domain получает два типа:

```text
MarkdownMathInline(string Content, bool IsDisplay)   // $...$, $$...$$
MarkdownMathBlock(string Source)                     // блочный $$...$$
```

Правила парсинга:

- `MathInline` -> `MarkdownMathInline`; `DelimiterCount >= 2` -> `IsDisplay = true`;
- `MathBlock` -> `MarkdownMathBlock`;
- `MathBlock` проверяется ДО `FencedCodeBlock` (он наследуется от него);
- пустое содержимое не создаёт math-узел.

### 2. Renderer backend: CSharpMath.Avalonia (Sylinko fork)

Выбран `Sylinko.CSharpMath.Avalonia 12.0.0`:

- это порт iosMath на C#, рендерит LaTeX полностью in-process;
- публикует `net10.0` (совпадает с TFM solution) и зависит от Avalonia 12;
- лицензия MIT;
- даёт `MathPainter` + `AvaloniaCanvas`, измерение (`Measure`) и отрисовку
  в существующий `DrawingContext`, без отдельного окна/композиции.

Отклонённые варианты:

- **KaTeX/MathJax + WebView** — нарушает native/lightweight направление, тянет
  JS runtime, увеличивает cold start.
- **AvaloniaMath (WPF-Math)** — пакет не обновлялся с 2023 (`2.1.0`), требует
  Avalonia `< 12`, несовместим с текущим solution.
- **Внешний сервис/отрисовка на сервере** — нарушает local-first.
- **Показывать TeX как код** — не решает задачу «формулы не показываются».

### 3. Отрисовка нативно, без отдельного tree

- Инлайн: формула занимает один канонический символ в текстовом потоке и
  рисуется кастомным `DrawableTextRun` (`MarkdownMathTextRun`) на месте
  placeholder-глифа. Selection, caret mapping, copy и поиск не меняются.
- Блочная: отдельный центрированный visual (`MarkdownMathBlockVisual`),
  меряется один раз при построении.

### 4. При ошибке — показывать исходник

Требование владельца: при невалидном LaTeX показывать исходный TeX
(моноширинным), а не красную заглушку. Это гарантирует, что контент
документа никогда не теряется.

### 5. Производительность

- рендерер вызывается только для реально найденных формул;
- измерения мемоизируются по `(latex, fontSize, color, isDisplay)`
  (потокобезопасный кэш с ограничением размера);
- документы без формул не платят за math.

## Consequences

Positive:

- формулы, включая сложные (дроби, матрицы, интегралы), отображаются;
- нативный in-process путь, близкий по духу к Mermaid/Naiad;
- нет новых сетевых/браузерных зависимостей.

Negative:

- появляется обязательная NuGet-зависимость CSharpMath (Avalonia-форк,
  поддерживается «usable level»);
- покрытие LaTeX — подмножество, а не полный TeX (некоторые экзотические
  команды могут не поддерживаться и уйдут в fallback);
- размер бинарника растёт (движок + шрифты).

## Scope of changes

- Domain: `MarkdownMathInline`, `MarkdownMathBlock`; text map/plain-text/outline.
- Infrastructure: разбор `MathInline`/`MathBlock` в Markdig-рендерере.
- Presentation: `MarkdownMathRenderer` (движок + кэш), math-спан в
  `MarkdownStyledText`, math-сегмент в layout, `MarkdownMathTextRun`,
  `MarkdownMathBlockVisual`.
- Docs: `sample.md`, README acknowledgements.

## Acceptance criteria

- `$...$` и `$$...$$` отображаются как формулы, а не пропадают;
- сложные формулы (дроби, индексы, суммы, матрицы) рендерятся;
- невалидный LaTeX показывает исходник, не ломая документ;
- copy/поиск/оглавление продолжают работать (TeX попадает в plain-text);
- сборка проходит, тесты на изменённое поведение зелёные.
