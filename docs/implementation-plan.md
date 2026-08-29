# MarkMello — next implementation plan

## Target

Собрать первый production-oriented Avalonia baseline, который уже соответствует vision и не ломает viewer-first fast path.

## Current repository state (2026-04-19)

- `M0` реализован: solution structure, runtime contracts, app startup pipeline и startup metrics уже в репозитории
- `M1` реализован: shell, custom/native chrome, welcome state, top/status chrome, progress indicator и centered reading surface уже есть
- `M2` реализован: open file, command-line open, drag & drop, reload и error states уже работают
- `M3` реализован: native markdown viewer уже покрывает headings / paragraphs / lists / quotes / hr / code / links / tables, а также принятый сверхплановый image path и document-wide selection
- `M4` считается закрытым в принятой интерпретации из `ADR-0002`: reading preferences сохраняются, а theme persistence работает как `Light`/`Dark` с system fallback при отсутствии явной сохранённой темы
- core-часть `M5` уже присутствует: explicit edit mode, split layout, editor pane, grid splitter, dirty state, `Save` / `Save As`, lightweight formatting toolbar
- ближайший реальный шаг разработки: `M6` в принятой интерпретации из `ADR-0003` — сначала product-shell additions (`Create MD`, `Close file`, app menu, app settings), затем platform polish и packaging

## Recommended sequence

Следующий этап разработки после текущего baseline:

1. `Create MD` на welcome screen как явный secondary authoring entry
2. unsaved draft model + первый `Save` через существующий `Save As`
3. `Close file` command с возвратом в `Welcome`
4. top-left app menu button + menu shell (`Open file`, `Close file`, `Settings`)
5. app settings subview в том же popover
6. `About`
7. manual update check через GitHub releases
8. localization foundation для shell strings + language switch
9. platform polish, file associations / activation
10. packaging for Windows/macOS/Linux

Вне текущего `M6` scope:

- syntax highlighting в source editor
- standalone milestone для code block polish
- recent files
- Native AOT evaluation

## M0 — Solution skeleton and runtime contracts

Цель:
подготовить каркас, который не придётся ломать при добавлении editor mode.

### Deliverables
- solution structure by layers
- app startup pipeline
- settings contracts
- document loading contracts
- platform abstraction contracts
- performance measurement hooks

### Suggested projects
- `MarkMello.Desktop` — Avalonia startup, windows, platform boot
- `MarkMello.Application` — use cases
- `MarkMello.Domain` — models and rules
- `MarkMello.Infrastructure` — file IO, settings, markdown integration
- `MarkMello.Presentation` — views, resources, view models

Если хочется проще на старте, можно временно сделать 3 проекта:
- `MarkMello.App`
- `MarkMello.Core`
- `MarkMello.Infrastructure`

Но UI и editor code всё равно нужно разделять по папкам и зависимостям.

### Done when
- приложение стартует пустым shell
- тема применима
- базовые сервисы зарегистрированы
- есть таймеры для startup metrics

## M1 — Viewer shell

Цель:
получить быстрый нативный shell с welcome/viewer состояниями.

### Deliverables
- custom window shell
- custom title bar
- welcome state
- top bar
- status bar
- progress indicator
- reading surface with centered column
- light / dark theme with system-following fallback when no explicit saved choice exists

### Important rules
- без editor initialization
- без markdown complexity beyond placeholder rendering
- без recent files persistence, если это мешает fast path

### Done when
- окно выглядит как целевой продукт
- темы переключаются без артефактов
- shell остаётся минимальным и спокойным

## M2 — File-first open path

Цель:
реализовать основной продуктовый сценарий.

### Deliverables
- open file command
- open from command line path
- drag & drop file open
- file activation support
- basic error states
- reload current file command

### Rules
- окно открывается даже если файл не найден
- ошибки отображаются внутри продукта, а не через падение
- file open не зависит от editor mode

### Done when
- `.md` можно открыть всеми основными путями
- неверный путь не ломает app
- first readable document достигается быстро и предсказуемо

## M3 — Native markdown viewer

Цель:
сделать настоящий viewer, а не заглушку.

### Deliverables
- markdown parse pipeline
- rendered document model
- native control renderer
- stable typography mapping
- headings / paragraphs / lists / quotes / hr / code / links / tables
- reading preferences binding

### Important decision
В M3 нужно окончательно выбрать renderer path:
- либо собственный native renderer
- либо временный markdown control, если нужен быстрый MVP

Для product-fit MarkMello предпочтительнее собственный native renderer.

### Done when
- типичный markdown читается устойчиво
- документ выглядит спокойно и предсказуемо
- тема и typographic settings реально влияют на рендер

## M4 — Settings and reading preferences

Цель:
закрыть основной reading UX.

### Deliverables
- settings popup/panel
- font mode: serif / sans / mono
- font size
- line height
- content width
- theme persistence: saved `Light` / `Dark`, system fallback when no explicit saved choice exists
- safe settings load on startup

### Done when
- настройки применяются live
- настройки сохраняются и восстанавливаются
- битые settings не ломают запуск

## M5 — Lazy edit mode

Цель:
ввести edit mode без налогов на viewer path.

### Deliverables
- explicit enter edit mode action
- split layout
- editor pane
- grid splitter
- dirty state
- save / save as flow
- lightweight formatting toolbar

### Rules
- editor subtree не создаётся на cold start viewer mode
- editor services не инициализируются до входа в Edit
- выход из Edit не должен ломать preview

### Done when
- viewer path не деградировал
- edit mode ощущается как secondary capability
- dirty state и save flow предсказуемы

## M6 — App shell, secondary authoring entry points, platform delivery

Цель:
расширить secondary authoring path и app-level shell так, чтобы продукт стал удобнее как desktop tool, но не потерял viewer-first fast path.

### Deliverables
- `Create MD` action на welcome screen
- unsaved draft model without fake file identity
- `Close file` action
- top-left app menu button
- app menu popover shell
- app settings subview
- `About`
- manual GitHub-based update check
- shell localization foundation
- language switch
- platform polish
- file associations / activation
- packaging for Windows/macOS/Linux

### Rules
- `Create MD` остаётся явной secondary action и не переводит продукт в editor-first positioning
- новый draft не должен притворяться уже существующим файлом на диске
- `Close file` возвращает пользователя в `Welcome`, а dirty-state resolution переиспользует уже существующий save/discard/cancel flow
- reading settings и app settings остаются разделены и визуально, и концептуально
- update check не участвует в startup fast path и выполняется только по явному действию пользователя
- language switch добавляется только после выделения локализуемого shell/resource слоя
- platform polish и packaging не должны встраивать тяжёлую или сетевую логику в путь `open file -> read`

### Done when
- `Create MD` открывает чистый draft и сразу вводит в edit mode без деградации cold start viewer mode
- app menu и app settings выглядят как спокойное продолжение существующего shell, а не как новый центр интерфейса
- `About`, updates и language choice живут в app-level settings, а не смешиваются с reading preferences
- packaged app сохраняет file-first open path и предсказуемое desktop behavior

## First implementation slice

Если начинать прямо сейчас, я бы делал **ровно такой первый slice**:

1. `App` + `MainWindow`
2. resources for theme/colors/typography
3. `MainWindowViewModel`
4. custom title bar
5. centered document host
6. welcome state
7. open file command
8. open via command line arg
9. drag & drop open
10. placeholder document renderer
11. startup metrics logging

Это даст уже реальный продуктовый skeleton.

## Concrete UI backlog from current design

### Must build first
- title bar
- window shell
- welcome screen
- document host
- top actions
- status bar
- settings popup shell

### Must build second
- markdown block views
- reading progress
- file errors
- theme persistence
- typography settings

### Build later
- new document
- app menu and app settings
- about / updates / language choice
- platform polish and packaging

## Performance gates

На каждом milestone проверять:
- startup time
- time to first window
- time to readable document
- memory before opening file
- memory after opening file
- memory after entering edit mode

Если новая функция ухудшает fast path — откатывать или выносить из startup.

## Risks to control early

### 1. Overbuilding the shell
Риск: сделать слишком много chrome и потерять document-first UX.

Контроль:
- каждое overlay/toolbar решение проверять против vision

### 2. Web thinking in desktop app
Риск: пытаться воспроизводить браузерные механики вместо нативных desktop паттернов.

Контроль:
- строить через Avalonia controls/resources/templates

### 3. Editor leakage into viewer
Риск: editor subsystem начнёт влиять на startup.

Контроль:
- отдельные services, lazy activation, отсутствие editor visual tree на старте

### 4. Markdown renderer dead end
Риск: выбрать слишком удобный временный путь, который потом мешает качественному viewer UX.

Контроль:
- заранее отделить parse layer от render layer

## Recommendation for the next real step

Следующий шаг разработки:

**войти в M6 напрямую через product-shell slice из `ADR-0003`**.

То есть начать в таком порядке:
- `Create MD`
- unsaved draft semantics + first-save behavior
- `Close file`
- app menu shell
- app settings subview
- `About`
- manual update check
- localization foundation + language switch

После этого:
- platform polish
- file associations / activation
- packaging

Из ближайшего активного scope исключены:
- syntax highlighting в source editor
- отдельный code block polish milestone
- recent files
- Native AOT evaluation
