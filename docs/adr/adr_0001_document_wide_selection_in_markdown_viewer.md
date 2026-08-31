# ADR-0001: Document-wide selection in Markdown viewer

## Status

Accepted

## Date

2026-04-18

## Context

В текущей реализации viewer в `MarkMello.Presentation/Views/MarkdownDocumentView.cs` документ собирается как набор независимых Avalonia-контролов:

- корневой `StackPanel` `_root`
- headings и paragraphs рендерятся отдельными `SelectableTextBlock`
- code block содержит отдельный `SelectableTextBlock`
- table cells содержат отдельные `SelectableTextBlock`
- lists и quotes собираются из вложенных контейнеров и новых текстовых контролов
- links вставляются через `InlineUIContainer` + `HyperlinkButton`

Следствие: пользователь не может выделить текст документа как единый непрерывный поток. Выделение разрывается на границах отдельных контролов, потому что selection принадлежит каждому `SelectableTextBlock` локально, а не всему документу.

Это противоречит ожидаемому UX markdown-viewer:

- документ должен выделяться как веб-страница
- копирование должно работать непрерывно через заголовки, абзацы, списки, цитаты, код и таблицы
- форматирование при этом не должно деградировать

Проблема архитектурная, а не косметическая:

- semantic markdown model уже существует и в целом подходит: `RenderedMarkdownDocument`, `MarkdownBlock`, `MarkdownInline`
- слой рендера не имеет общей координатной системы текста документа
- viewer не имеет document-level selection model
- интерактивные inline-элементы, особенно `HyperlinkButton`, дополнительно разрывают единый текстовый поток

## Decision

В viewer вводится **document-wide selection architecture**.

Selection больше не должен жить внутри отдельных `SelectableTextBlock`. Владельцем selection становится весь `MarkdownDocumentView` как единая document surface.

### Главный принцип

Документ остаётся визуально блочным, но логически становится единым непрерывным текстовым потоком с общей адресацией символов.

### Мы принимаем следующие решения

#### 1. Ввести document text map

Добавить промежуточную модель, которая строится поверх `RenderedMarkdownDocument` и хранит:

- канонический plain text документа для copy/select
- глобальные диапазоны символов для каждого block fragment
- глобальные диапазоны символов для inline fragment
- mapping `globalOffset <-> visual fragment/local offset`
- правила вставки переводов строк между блоками
- правила текстового представления списков, таблиц и code block

Рабочее имя модели:

- `MarkdownDocumentTextMap`
- или `MarkdownFlowDocument`

#### 2. Перенести selection state на уровень `MarkdownDocumentView`

`MarkdownDocumentView` должен хранить и управлять:

- `SelectionAnchor`
- `SelectionStart`
- `SelectionEnd`
- `HasSelection`
- `SelectedText`
- `SelectAll()`
- `ClearSelection()`
- pointer drag selection
- keyboard copy/select-all integration

#### 3. Перевести renderer с control-per-block selection на fragment-based layout

Каждый block renderer должен стать фрагментом единого документа, а не отдельным миром со своей selection.

Нужен контракт наподобие:

```text
IMarkdownLayoutFragment
- int StartOffset
- int EndOffset
- int? HitTest(Point point)
- IReadOnlyList<Rect> GetSelectionRects(int selectionStart, int selectionEnd)
- void Render(DrawingContext context, SelectionRange selection)
```

Это позволит:

- сохранить отдельное визуальное форматирование для block types
- иметь единую selection model на весь документ
- подсвечивать выделение сразу в нескольких фрагментах
- копировать единый текст без разрывов

#### 4. Сохранить existing semantic markdown model как parse layer

`RenderedMarkdownDocument` остаётся parse/render input model.

Новая selection-aware модель строится поверх неё и не заменяет её.

Это важно, чтобы не ломать:

- markdown parsing pipeline
- application use cases
- domain contracts, уже используемые в viewer mode

#### 5. Изменить представление ссылки в inline model

Текущий `MarkdownLinkInline` хранит готовый `Text`, что уже теряет часть inline-структуры.

Нужно перейти к модели вида:

```text
MarkdownLinkInline(IReadOnlyList<MarkdownInline> Inlines, string Url, string? Title)
```

Это даст:

- корректное участие текста ссылки в global text map
- единое поведение selection внутри ссылки
- больше контроля над отрисовкой link text без `InlineUIContainer`

#### 6. Отказаться от `HyperlinkButton` внутри inline text path как от базового механизма ссылок

Для document-wide selection ссылки должны рендериться как текстовые фрагменты с link styling и hit-testing, а не как вложенные отдельные controls.

Переход к fragment-based links обязателен, потому что `InlineUIContainer` + `HyperlinkButton` ломают непрерывность текста и усложняют pointer behavior.

## Decision drivers

### Почему это решение выбрано

- устраняет корневую причину бага, а не симптом
- сохраняет native Avalonia path
- сохраняет viewer-first архитектуру
- не превращает viewer в pseudo-editor
- позволяет сохранить block formatting
- позволяет контролировать copy semantics
- создаёт основу для качественного поведения ссылок, code block и таблиц

### Почему не выбраны упрощённые варианты

#### Вариант A: оставить текущую архитектуру и смириться

Отклонён.

Причина: UX viewer остаётся дефектным на базовом пользовательском действии — выделении и копировании текста.

#### Вариант B: заменить всё на один большой `SelectableTextBlock`

Отклонён.

Причина:

- не покрывает сложные block types
- ломает quote/code/table layout
- не даёт полноценного контроля над rich formatting
- не решает корректно link behavior

#### Вариант C: использовать `TextBox`/`TextPresenter` как read-only rich viewer

Отклонён.

Причина:

- тянет editor semantics в viewer
- ухудшает document-first UX
- слабо подходит для block-level markdown presentation

#### Вариант D: WebView / HTML viewer

Отклонён.

Причина:

- нарушает выбранный native Avalonia course
- ухудшает архитектурную чистоту viewer
- противоречит product direction

#### Вариант E: временно использовать сторонний markdown control как core path

Отклонён как базовое решение.

Причина:

- уменьшает контроль над selection/copy/layout behavior
- создаёт риск dead-end для viewer core
- может использоваться только как временный fallback, но не как целевая архитектура

## Consequences

## Positive

- пользователь сможет выделять текст непрерывно через весь документ
- форматирование документа сохранится на block-level
- поведение копирования станет предсказуемым
- links смогут жить в едином текстовом потоке без разрыва selection
- viewer получит правильную архитектурную основу для дальнейшего развития

## Negative

- решение требует заметной переработки `MarkdownDocumentView`
- возрастёт сложность presentation-layer renderer
- потребуется собственный layout/hit-test слой
- понадобится отдельная стратегия тестирования selection geometry и copy semantics

## Neutral

- markdown parser и application use cases можно сохранить почти без изменений
- большая часть работы сосредоточится в `Presentation` и частично в `Domain`

## Scope of changes

## Domain

### Add

- `MarkdownDocumentTextMap` / `MarkdownFlowDocument`
- selection-related value objects, например `TextRange`
- нормализаторы текстового представления block types для copy/select

### Change

- `MarkdownLinkInline` -> хранить дочерние inline nodes вместо плоского `Text`

## Presentation

### Replace gradually

- `MarkdownDocumentView` из control-composer в document surface renderer

### Add

- layout fragments
- selection state/controller
- hit testing for fragments
- custom rendering of selection highlight
- keyboard shortcuts integration for select-all/copy
- pointer drag selection behavior

### Remove from core selection path

- зависимость от `SelectableTextBlock` как основного носителя selection
- `InlineUIContainer + HyperlinkButton` для обычных inline links

## Infrastructure / Application

Изменения не требуются либо минимальны, пока сохраняется текущий parse pipeline.

## Implementation plan

### Phase 1 — Introduce logical text map

Сделать модель глобального текста документа без изменения текущего UX.

Результат:

- можно вычислить `SelectedText` по глобальным offsets
- появляется единая адресация документа

### Phase 2 — Convert simple blocks

Перевести на новую архитектуру:

- headings
- paragraphs
- quote paragraph content
- list paragraph content

Результат:

- непрерывное выделение начинает работать на основном тексте

### Phase 3 — Convert interactive and special blocks

Перевести:

- links
- code blocks
- tables
- nested list/quote cases

Результат:

- весь типичный markdown попадает в единый selection path

### Phase 4 — Interaction polish

Добавить и стабилизировать:

- `Ctrl/Cmd+A`
- `Ctrl/Cmd+C`
- double click word select
- triple click paragraph select, если будет уместно
- link activation only when selection collapsed
- drag threshold between click and selection

### Phase 5 — Tests

Добавить тесты на:

- global offsets
- copy text normalization
- selection across block boundaries
- selection across links
- selection inside code block
- selection through tables and lists

## Work breakdown

### Slice S1 — foundation

- новый flow model
- new link inline contract
- adapter from `RenderedMarkdownDocument`

### Slice S2 — selection engine

- selection range state
- pointer anchor/extend logic
- selected text extraction

### Slice S3 — fragment renderer

- paragraph fragment
- heading fragment
- quote fragment
- list fragment

### Slice S4 — advanced fragments

- link fragment
- code fragment
- table fragment

### Slice S5 — UX polish and hardening

- keyboard
- copy
- accessibility
- regression tests

## Risks and mitigations

### Risk: implementation becomes too editor-like

Mitigation:

- не использовать editor controls как viewer core
- оставить selection read-only и document-centric

### Risk: hit-testing complexity explodes on tables and nested blocks

Mitigation:

- сначала внедрить linear text fragments
- table/code вынести в отдельную фазу
- использовать общий offset contract для всех fragment types

### Risk: link click conflicts with drag selection

Mitigation:

- ссылка активируется только при collapsed selection и после pointer release
- drag threshold обязателен

### Risk: copy output differs from what user sees

Mitigation:

- канонический text map должен быть проектным source of truth для copy semantics
- текстовая нормализация документируется и тестируется

## Acceptance criteria

Решение считается внедрённым успешно, если:

- текст можно выделить непрерывно сверху вниз через несколько block types
- выделение не обрывается на границах paragraph/heading/list/quote/code/table
- копирование даёт предсказуемый и целостный текст
- ссылки остаются интерактивными, но не ломают selection
- визуальное форматирование документа не деградирует
- viewer mode остаётся лёгким и не превращается в editor surface

## Final statement

Для MarkMello правильным направлением является не попытка "склеить" существующие `SelectableTextBlock`, а переход к document-wide selection architecture с собственным fragment-based native renderer поверх текущей semantic markdown model.

Именно это решение исправляет баг качественно и без отказа от viewer-first / native Avalonia курса.

