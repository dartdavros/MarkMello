# ADR-0006: Optional folder browsing mode

## Status

Superseded by [ADR-0007](adr_0007_folder_workspace_with_document_tabs.md) — Folder workspace with document tabs (2026-08-29).

Решение действовало с 2026-07-07 по 2026-08-29. Заменено целиком: ADR-0007 вводит вкладки, файловые операции, поиск по именам и watcher, которые здесь объявлены non-goals. Правила viewer-first fast path перенесены в ADR-0007 без ослабления.

## Date

2026-07-07

## Context

MarkMello зафиксирован как viewer-first desktop Markdown-приложение.

Базовый сценарий продукта остаётся простым:

```text
open markdown file -> readable document
```

Действующие продуктовые и архитектурные ограничения:

- MarkMello в первую очередь является viewer'ом, а не editor'ом;
- скорость открытия одного Markdown-файла важнее количества функций;
- document surface важнее интерфейсной оболочки;
- editor subsystem загружается лениво и не участвует в viewer fast path;
- приложение остаётся local-first и file-first;
- fast path не должен включать второстепенные панели, тяжёлые фоновые процессы или индексирование коллекций файлов;
- продукт не должен постепенно превращаться в IDE, docs workspace, note-taking suite или multi-pane knowledge management system.

При этом существует валидный пользовательский сценарий, который не равен workspace-модели:

- пользователь открывает локальную папку с Markdown-документами;
- слева видит дерево директорий и Markdown-файлов;
- выбирает нужный `.md` файл;
- выбранный файл открывается в текущем viewer surface;
- навигация по папке нужна только для удобного перехода между локальными документами.

Такой сценарий полезен для чтения:

- папок с документацией;
- наборов README/notes;
- локальных статей;
- Markdown-файлов внутри обычного проекта.

Главный риск: если реализовать это как `multi-file workspace`, продукт начнёт смещаться в сторону IDE/project explorer и нарушит текущую Vision.

Поэтому функциональность должна быть зафиксирована не как мультифайловый workspace, а как ограниченный **optional folder browsing mode**.

## Decision

MarkMello получает опциональный режим просмотра локальной папки: **Folder Browsing Mode**.

Это вторичный сценарий поверх file-first viewer, а не новая основная модель приложения.

## Rules

### 1. `Open File` remains the primary scenario

Открытие одного Markdown-файла остаётся главным и самым дешёвым путём исполнения.

Правила:

- запуск приложения с file path из CLI/file association не создаёт folder session;
- `Open File` не создаёт sidebar;
- `Open File` не инициализирует folder tree services;
- `Open File` не выполняет обход директорий;
- поведение single-file viewer mode не меняется.

### 2. `Open Folder` is an explicit secondary action

Открытие папки возможно только по явному действию пользователя.

Допустимые entry points:

- app menu command `Open folder`;
- secondary action на welcome screen, если она визуально не конкурирует с `Open file`.

Недопустимо:

- делать folder mode режимом по умолчанию;
- заменять welcome screen project/workspace launcher'ом;
- автоматически превращать parent directory открытого файла в folder session.

### 3. Folder sidebar exists only in folder mode

Левый sidebar с деревом директорий и Markdown-файлов появляется только после успешного `Open Folder`.

Правила:

- sidebar не монтируется в initial visual tree single-file режима;
- sidebar не создаётся при старте приложения с файлом;
- sidebar не должен становиться постоянной навигационной панелью продукта;
- sidebar должен быть скрываемым или закрываемым вместе с folder session;
- document surface остаётся главным визуальным центром.

### 4. No tabs

В MarkMello не вводятся верхние вкладки документов.

Решение:

- в один момент активен один открытый документ;
- выбор другого файла в дереве заменяет текущий активный документ;
- dirty-state resolution для edit mode переиспользует существующий save/discard/cancel flow;
- multi-document editing и tabbed UI остаются вне scope.

Причина:

- tabs создают ожидание multi-file editor/workspace;
- tabs увеличивают состояние приложения;
- tabs конкурируют с принципом document-first minimal UI.

### 5. Folder tree is lazy

Folder tree не должен рекурсивно обходить всю папку при открытии.

Минимальные правила lazy loading:

- при `Open Folder` загружается только корневой уровень выбранной директории;
- содержимое поддиректории читается только при её раскрытии пользователем;
- Markdown-файлы не читаются и не парсятся до выбора конкретного файла;
- неизвестные или неподдерживаемые файлы не передаются в markdown pipeline;
- повторное раскрытие уже загруженной директории может использовать in-memory cache в рамках текущей folder session.

Недопустимо в первой версии:

- full recursive scan;
- построение индекса;
- предварительный markdown parse всех файлов;
- предварительный подсчёт outline/word count по всей папке;
- фоновые обходы дерева после открытия папки.

### 6. Folder session is transient

Folder mode не создаёт persistent workspace model.

Правила:

- не создаётся `.markmello`, `.workspace`, database или project metadata file;
- состояние открытой папки не становится обязательной частью startup path;
- приложение не восстанавливает folder session автоматически при старте в первой версии;
- recent folders не входят в это решение;
- настройки folder mode, если появятся позже, требуют отдельного решения.

### 7. Folder tree is navigation, not file management

Первая версия folder browsing является только навигацией по локальным Markdown-документам.

In scope:

- показать директории;
- показать поддерживаемые Markdown-файлы;
- раскрыть/свернуть директорию;
- открыть выбранный Markdown-файл;
- подсветить активный файл;
- закрыть folder session.

Out of scope:

- создание файлов из дерева;
- удаление файлов;
- переименование файлов;
- перемещение файлов;
- drag-and-drop reorder;
- контекстные IDE-like actions;
- git status;
- diagnostics по папке;
- project-wide search;
- backlinks;
- workspace outline.

### 8. Opening a document from folder reuses the normal document pipeline

Файл, выбранный в дереве, открывается через тот же application-level document loading flow, что и обычный `Open File`.

Правила:

- не создаётся отдельный folder-specific markdown loader;
- markdown parser/rendering pipeline остаётся общим;
- editor mode не становится зависимостью folder browsing;
- diagram rendering, image rendering, document-wide selection и другие viewer capabilities работают как для обычного открытого файла;
- ошибки чтения конкретного файла отображаются как обычные document error states.

### 9. Folder errors must be local and recoverable

Ошибки folder browsing не должны ломать viewer.

Cases:

- папка не найдена;
- папка недоступна;
- нет прав на поддиректорию;
- файл удалён между построением дерева и открытием;
- файл стал недоступен;
- директория содержит слишком много элементов.

Rules:

- окно приложения продолжает работать;
- ошибка директории отображается локально в соответствующем tree node или folder view;
- ошибка открытия файла отображается в document surface;
- пользователь может выбрать другой файл;
- ошибка folder subsystem не должна ломать текущий single-file viewer path.

### 10. File filtering is conservative

Folder tree показывает только директории и поддерживаемые Markdown-файлы.

Initial supported file extensions:

- `.md`
- `.markdown`

Правила:

- extension matching case-insensitive;
- неподдерживаемые файлы скрываются из tree;
- пустые директории могут отображаться, если они нужны для честной навигации по структуре;
- расширение списка форматов требует отдельного product decision или отдельного ADR, если меняет позиционирование.

### 11. Performance must be measured

Folder browsing не считается безопасным только потому, что он реализован lazy.

Нужно измерять минимум:

- single-file startup time до и после добавления folder mode;
- time to first window в single-file сценарии;
- time to readable document в single-file сценарии;
- `Open Folder` activation time;
- root folder load time;
- expanded directory load time;
- memory usage в single-file viewer mode;
- memory usage после открытия folder mode;
- memory usage после раскрытия типичной и крупной директории.

Критическое правило:

- если folder mode ухудшает single-file fast path, реализация считается архитектурно неверной независимо от удобства новой функции.

## Architecture impact

### Domain

Можно добавить UI-agnostic модели folder navigation:

```text
FolderSession
FolderTreeNode
FolderEntry
FolderEntryKind
FolderNodeLoadState
```

Требования:

- модели не должны зависеть от Avalonia;
- модели не должны зависеть от editor subsystem;
- модели не должны превращаться в project/workspace domain.

### Application

Можно добавить use cases:

```text
OpenFolderUseCase
CloseFolderUseCase
LoadFolderNodeUseCase
OpenDocumentFromFolderUseCase
```

Требования:

- `OpenDocumentFromFolderUseCase` должен переиспользовать существующий document open flow;
- dirty-state resolution должен быть общим с `Open File`, `Close file` и edit mode;
- use cases не должны выполнять recursive scan.

### Infrastructure

Можно добавить filesystem adapter для folder tree:

```text
IFolderPicker
IFolderTreeReader
FileSystemFolderTreeReader
```

Требования:

- чтение директории должно быть cancelable, если пользователь закрывает folder session или сворачивает/переключает UI;
- adapter должен корректно обрабатывать permission errors;
- adapter не должен запускать file watcher в первой версии;
- adapter не должен выполнять indexing.

### Presentation

Можно добавить условные presentation components:

```text
FolderSidebarView
FolderTreeItemViewModel
FolderSessionViewModel
```

Требования:

- компоненты создаются только в folder mode;
- sidebar не должен быть частью single-file initial visual tree;
- sidebar должен визуально подчиняться document surface;
- active document indication не должна превращаться в tab model.

## UI principles

### Layout

Целевой layout в folder mode:

```text
+------------------------------------------------+
| minimal shell / app chrome                     |
+------------------+-----------------------------+
| folder sidebar   | centered document surface    |
|                  |                             |
| docs/            | readable Markdown document   |
|   intro.md       |                             |
|   install.md     |                             |
|   guides/        |                             |
+------------------+-----------------------------+
```

### Sidebar behavior

- sidebar должен быть спокойным и узким;
- дерево не должно доминировать над документом;
- активный файл подсвечивается мягко;
- раскрытие директорий должно быть предсказуемым;
- длинные имена файлов должны обрабатываться без разрушения layout;
- keyboard navigation желательна, но не должна блокировать первую версию.

### Document behavior

- выбранный файл отображается в текущем document surface;
- reading preferences применяются как обычно;
- edit mode работает как secondary capability для активного файла;
- reload применим к активному файлу;
- close file внутри folder mode оставляет folder sidebar открытым и переводит document surface в empty state;
- close folder закрывает sidebar и возвращает приложение в обычный welcome/single-file shell state.

## Rejected alternatives

### Alternative A: Multi-file tabs

Отклонено.

Причины:

- создаёт ожидание multi-document editor;
- усложняет dirty-state management;
- увеличивает UI chrome;
- ухудшает document-first positioning.

### Alternative B: Full workspace/project model

Отклонено.

Причины:

- противоречит Vision и Architecture non-goals;
- требует persistent project state;
- тянет product direction в сторону IDE/docs workspace;
- делает folder tree основной концепцией продукта.

### Alternative C: Recursive indexing on folder open

Отклонено.

Причины:

- нарушает fast path для secondary feature;
- плохо масштабируется на больших директориях;
- создаёт скрытую стоимость открытия папки;
- открывает путь к project-wide features, которые не входят в продуктовую границу.

### Alternative D: Auto-open parent folder when opening a file

Отклонено.

Причины:

- single-file сценарий должен оставаться single-file;
- пользователь мог хотеть просто прочитать один документ;
- автоматический sidebar создаёт интерфейсный шум;
- это нарушает принцип явного secondary action.

### Alternative E: Permanent navigation rail/sidebar

Отклонено.

Причины:

- document surface перестаёт быть главным визуальным центром;
- UI начинает выглядеть как workspace shell;
- single-file viewer получает лишний chrome.

## Consequences

### Positive

- MarkMello становится удобнее для чтения локальных наборов Markdown-документов;
- продукт не теряет viewer-first позиционирование;
- single-file fast path остаётся защищённым;
- folder mode можно реализовать как progressive enhancement;
- отсутствие вкладок удерживает продукт от IDE-like поведения;
- lazy tree снижает риск проблем на больших папках.

### Neutral

- folder browsing добавляет новое UI-состояние приложения;
- понадобится отдельная модель transient folder session;
- dirty-state flow должен быть переиспользован при переключении файлов;
- часть UX-решений можно уточнить в implementation plan без изменения этого ADR.

### Negative

- sidebar увеличивает сложность shell state management;
- lazy tree требует аккуратной обработки loading/error/cancel states;
- нужно контролировать, чтобы feature не стала основанием для дальнейшего scope creep;
- потребуется performance regression testing single-file сценария.

## Implementation sequence

Рекомендуемая последовательность реализации:

1. Добавить `IFolderPicker` и platform implementation для выбора директории.
2. Добавить domain/application модели transient folder session.
3. Реализовать lazy root directory load без recursive scan.
4. Добавить conditional folder sidebar, который не создаётся в single-file mode.
5. Подключить открытие Markdown-файла из tree через существующий document open flow.
6. Добавить dirty-state resolution при переключении активного файла из дерева.
7. Добавить close folder / close sidebar behavior.
8. Добавить локальные error states для directory/file access failures.
9. Добавить performance measurements и regression checks для single-file fast path.
10. Только после стабилизации первой версии обсуждать дополнительные возможности отдельными ADR.

## Explicit non-goals

Этот ADR не утверждает:

- tabs;
- project/workspace model;
- recent folders;
- file watcher;
- global folder search;
- backlinks;
- git integration;
- folder-wide outline;
- file management operations;
- project settings;
- plugin model;
- database/cache index;
- cloud sync;
- automatic restore of last opened folder.

## Final statement

Folder browsing принимается как ограниченная secondary capability:

```text
Open local folder -> lazily browse Markdown files -> open one document in the current viewer
```

Это не меняет сущность MarkMello.

MarkMello остаётся быстрым local-first Markdown viewer с optional edit mode, где folder tree является временной навигацией по локальным документам, а не workspace, IDE или note-taking environment.
