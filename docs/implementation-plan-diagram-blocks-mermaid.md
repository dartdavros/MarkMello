# MarkMello — план реализации diagram blocks и Mermaid rendering

## Статус
Утвержден

## Дата

2026-05-10

## Связанный ADR

`ADR-MarkMello-Diagram-Blocks-And-Mermaid-Rendering.md`

## Цель

Реализовать в MarkMello первоклассную поддержку диаграмм в Markdown-документах через общую модель `DiagramBlock`, где Mermaid является первым поддержанным dialect.

Фича считается реализованной только когда Mermaid fence в документе рендерится как настоящая диаграмма в viewer mode, а не как обычный code block, placeholder или optional-интеграция.

## Текущие точки интеграции

В текущем репозитории расширяются следующие части:

- `src/MarkMello.Domain/RenderedMarkdownDocument.cs`
  - текущая иерархия `MarkdownBlock`
  - существующие `MarkdownCodeBlock` и `MarkdownImageBlock`
- `src/MarkMello.Infrastructure/Markdown/MarkdigMarkdownDocumentRenderer.cs`
  - обработка `FencedCodeBlock`
  - извлечение source code из fenced/indented code block
  - сохранение `MarkdownSourceSpan`
- `src/MarkMello.Application/Abstractions/`
  - контракты renderer/service должны жить вне Presentation
- `src/MarkMello.Infrastructure/DependencyInjection.cs`
  - регистрация Naiad-backed Mermaid renderer
  - проверка композиции supported renderers
- `src/MarkMello.Presentation/Views/MarkdownDocumentView.cs`
  - branch для нового `MarkdownDiagramBlock`
  - визуальный блок диаграммы
  - error block для невалидного source/runtime-ошибки конкретной диаграммы
- `src/MarkMello.Presentation/Views/Markdown/Svg/AotSafeSvgImage.cs`
  - отображение SVG, который генерирует выбранный Mermaid renderer
- `sample.md`
  - реальные примеры Mermaid-диаграмм
- `README.md`
- `README.en.md`
  - нижний блок acknowledgements/credits для Naiad и Mermaid

## Жёсткие правила реализации

- Mermaid не реализуется как улучшенный code block.
- Mermaid не реализуется через WebView.
- Mermaid не реализуется через Node/npm/Puppeteer/Chromium.
- Mermaid не реализуется через сетевой сервис.
- Mermaid не реализуется через внешний процесс.
- Mermaid не зависит от editor subsystem.
- У Mermaid renderer нет пользовательского состояния `renderer unavailable`.
- Если Mermaid заявлен как поддержанный dialect, Naiad-backed renderer является обязательной runtime-зависимостью приложения.
- Отсутствие renderer для поддержанного dialect является ошибкой композиции приложения и должно ловиться тестами.
- Fallback допускается только для ошибки конкретной диаграммы: невалидный Mermaid source, исключение renderer, ошибка SVG-отрисовки.
- PlantUML закладывается в архитектурную модель, но не объявляется поддержанным dialect до выбора и утверждения собственного renderer backend.

## M0 — Проверка Naiad как обязательного Mermaid backend

Статус: выполнено 2026-05-17. См. `tests/m0-naiad-spike.md`.

Цель:
подтвердить, что Naiad можно использовать как штатный bundled renderer для Mermaid без нарушения конституции MarkMello.

### Результаты

- добавлен минимальный технический spike в рамках репозитория или тестового проекта
- проверен NuGet package `Naiad` с фиксированной версией
- подтверждён способ вызова renderer из .NET-кода
- получен SVG для минимальной Mermaid-диаграммы
- проверено, что renderer не требует WebView, Node, браузер, сеть или внешний процесс
- зафиксированы реальные API Naiad и ограничения, найденные при проверке

### Правила

- spike не должен превращаться в отдельную альтернативную архитектуру
- если Naiad не проходит базовую проверку, реализация останавливается
- нельзя заменять Naiad на внешний optional renderer без нового ADR
- нельзя добавлять временную пользовательскую ветку `Mermaid source as code block` как нормальный successful path

### Готово когда

- Mermaid source успешно превращается в SVG через Naiad внутри .NET-процесса
- результат проверки зафиксирован в комментарии к реализации или в короткой dev-заметке рядом с тестами
- принято решение продолжать реализацию по текущему ADR либо остановиться и пересмотреть ADR

### Зафиксированные находки M0

- публичный API: `MermaidSharp.Mermaid.Render(string input, RenderOptions options) → string` (синхронный); перегрузки без `RenderOptions` нет, README пакета по этому пункту неточен;
- ассембли-references Naiad: только BCL + `Pidgin 3.5.1`; никаких WebView/Node/Chromium/Puppeteer/Mermaid.ink/Process.Start/HttpClient в бинарнике не найдено;
- Naiad 0.1.0–0.1.2 публикуют только `lib/net10.0/`, других TFM нет → требует runtime upgrade всего solution (см. M0.5).

## M0.5 — Runtime upgrade на net10.0

Статус: следствие M0 и Decision 10 в ADR-0005.

Цель:
перевести solution на `net10.0` как обязательный TFM, потому что Naiad публикует только `net10.0` lib, и подмена backend запрещена без нового ADR.

### Результаты

- `Directory.Build.props` использует `<TargetFramework>net10.0</TargetFramework>`
- `Microsoft.Extensions.DependencyInjection*` PackageReferences подняты до `10.0.0`
- CI workflow `release-windows.yml` использует `dotnet-version: 10.0.x`
- .NET 10 SDK установлен локально для разработки
- `dotnet restore`, `dotnet build`, `dotnet test` проходят на полном solution до начала M1
- ни одна зависимость solution (Avalonia, Markdig, CommunityToolkit.Mvvm и пр.) не оказалась несовместимой с net10

### Правила

- TFM меняется централизованно через `Directory.Build.props`, без per-project override
- если какая-либо зависимость solution окажется несовместимой с net10, это блокер уровня ADR
- net10 является GA-релизом на момент решения; preview SDK не используется
- Native AOT direction сохраняется на net10

### Готово когда

- solution собирается и тесты зелёные на net10
- CI пайплайн использует net10 SDK
- M1 может стартовать без NU1202 при добавлении Naiad

## M1 — Сквозная модель diagram blocks и обязательная renderer composition

Статус: выполнено 2026-05-17.

Цель:
ввести общую модель диаграмм и обязательную композицию renderers без Mermaid-only архитектуры.

### Результаты

- в Domain добавлен общий `MarkdownDiagramBlock`
- добавлен enum/тип dialect, минимум с `Mermaid` и зарезервированным `PlantUml`
- `PlantUml` не объявлен поддержанным dialect в этой реализации
- добавлены generic contracts для diagram rendering
- добавлен `IDiagramRenderer`
- добавлен `IDiagramRenderService`
- добавлены модели request/result для рендера диаграмм
- добавлена проверка: каждый поддержанный dialect имеет ровно один renderer
- отсутствие Mermaid renderer ломает композицию и покрыто тестом
- дублирующий Mermaid renderer ломает композицию и покрыт тестом

### Правила

- Domain не должен зависеть от Naiad, Avalonia, SVG controls или Presentation
- contracts не должны быть Mermaid-specific
- renderer absence не должен попадать в пользовательский UI
- supported dialects list для этой фичи содержит только `Mermaid`
- `plantuml` fence пока остаётся обычным code block, потому что PlantUML renderer ещё не выбран и не утверждён

### Готово когда

- модель диаграмм не требует переделки для будущего PlantUML renderer
- Mermaid renderer является обязательной частью композиции
- тесты подтверждают, что приложение не может быть собрано в некорректной конфигурации supported dialect без renderer

## M2 — Распознавание Mermaid fence в markdown pipeline

Статус: выполнено 2026-05-17.

Цель:
перевести Mermaid fenced code blocks из обычного `MarkdownCodeBlock` в `MarkdownDiagramBlock` без нарушения остальных code blocks.

### Результаты

- `MarkdigMarkdownDocumentRenderer` распознаёт `mermaid` по первому token info string
- распознавание case-insensitive
- ` ```mermaid ` создаёт `MarkdownDiagramBlock` с `Kind = Mermaid`
- ` ```MERMAID ` создаёт `MarkdownDiagramBlock` с `Kind = Mermaid`
- ` ```mermaid title=... ` создаёт `MarkdownDiagramBlock` с `Kind = Mermaid`
- обычные fenced code blocks остаются `MarkdownCodeBlock`
- indented code blocks остаются `MarkdownCodeBlock`
- `MarkdownSourceSpan` сохраняется для diagram block
- добавлены тесты markdown pipeline

### Правила

- Mermaid source сохраняется без потери форматирования и переносов строк
- parser не должен вызывать renderer
- parser не должен знать про Naiad
- parser не должен распознавать `plantuml` как diagram block до реализации PlantUML renderer

### Готово когда

- Mermaid fence в parsed document представлен как `MarkdownDiagramBlock`
- обычный код не ломается
- source line mapping остаётся пригодным для edit-mode scroll synchronization

## M3 — Naiad-backed Mermaid renderer

Статус: выполнено 2026-05-17.

Цель:
реализовать обязательный Mermaid renderer на Naiad как часть Infrastructure.

### Результаты

- в `MarkMello.Infrastructure.csproj` добавлен `PackageReference` на Naiad с фиксированной версией
- добавлен `MermaidDiagramRenderer : IDiagramRenderer`
- renderer вызывает Naiad in-process
- renderer возвращает SVG при успешном рендере
- renderer возвращает typed failure для ошибки конкретной диаграммы
- renderer не обращается к сети
- renderer не запускает внешние процессы
- renderer не зависит от Presentation и editor mode
- добавлены unit/smoke tests на успешный render и ошибочный Mermaid source

### Правила

- successful path — только SVG, полученный от Naiad
- нельзя подменять успешный render выводом исходника
- exception handling допускается только вокруг ошибки конкретной диаграммы
- ошибки композиции renderer не маскируются как `DiagramRenderFailure`

### Готово когда

- минимальная Mermaid flowchart диаграмма стабильно рендерится в SVG
- невалидная Mermaid диаграмма даёт controlled failure, а не падение документа
- тесты подтверждают отсутствие внешних runtime-зависимостей на уровне выбранного backend behavior

## M4 — Отображение Mermaid SVG в native viewer

Статус: выполнено 2026-05-17. Wiring готов end-to-end; визуальная точность SVG-output зависит от M5 (расширение `AotSafeSvgImage` под `<foreignObject>`/`<style>`).

Цель:
показать результат Mermaid renderer как полноценный visual block внутри `MarkdownDocumentView`.

### Результаты

- `MarkdownDocumentView` получает branch для `MarkdownDiagramBlock`
- diagram block вызывает `IDiagramRenderService`
- успешный Mermaid render отображается как SVG visual
- error result отображается как diagram error block
- error block содержит dialect name, короткое сообщение и исходный source
- visual style соответствует текущему документному UI: спокойно, без лишнего chrome
- блок диаграммы не ломает прокрутку, ширину контента, тему и document rhythm

### Правила

- диаграмма не должна монтировать editor UI
- диаграмма не должна быть WebView
- диаграмма не должна быть кнопкой/iframe/внешним viewer
- успешная Mermaid диаграмма не должна отображаться как code block
- error block — это runtime error state конкретной диаграммы, а не нормальный fallback реализации

### Готово когда

- Mermaid diagram видна в viewer mode как диаграмма
- документ без диаграмм продолжает рендериться как раньше
- документ с диаграммой не ломает first window и базовый file-first open path
- ошибка одной диаграммы не ломает остальной документ

## M5 — SVG compatibility для output Naiad

Цель:
довести SVG rendering до фактической совместимости с SVG, который генерирует Naiad.

### Результаты

- собран набор реальных SVG output от Naiad для representative Mermaid diagrams
- проверен текущий `AotSafeSvgImage`
- добавлена поддержка недостающих SVG elements/attributes, если они требуются для Naiad output
- покрыты тестами SVG cases, которые реально встречаются в Mermaid output
- если конкретный SVG feature невозможно корректно поддержать текущим renderer, фиксируется блокер для ADR, а не создаётся обходной WebView/Node path

### Правила

- расширяется native SVG path, а не добавляется браузерный renderer
- нельзя silently downgrade diagram в source/code view как successful path
- не нужно поддерживать весь SVG standard абстрактно; нужно поддержать subset, реально требуемый Naiad output для выбранных Mermaid diagrams

### Готово когда

- flowchart, sequence diagram и class/state diagram из `sample.md` отображаются корректно
- SVG rendering tests покрывают минимальный фактический subset Mermaid output
- AOT-safe SVG path остаётся контролируемым и не превращается в WebView replacement

## M6 — Selection, copy и поведение diagram block в документе

Цель:
согласовать диаграммы с document-wide selection и copy semantics.

### Результаты

- успешно отрендеренная диаграмма не загрязняет обычный continuous text selection исходником, который визуально не показан как текст
- error block показывает source как читаемый code-style area
- для diagram block предусмотрено действие копирования source, если это уже укладывается в текущую UI-модель без лишнего chrome
- document text map не ломается на diagram block
- edit-mode scroll synchronization сохраняет привязку к source span диаграммы

### Правила

- не добавлять тяжёлое контекстное меню, если в текущей UI-модели для этого нет принятого паттерна
- не смешивать diagram source с обычным текстом документа при successful render
- error block source можно копировать как видимый текст ошибки

### Готово когда

- `Select All` и обычное выделение документа работают предсказуемо рядом с диаграммами
- diagram block не ломает existing text map tests
- source span диаграммы пригоден для синхронизации preview/editor

## M7 — `sample.md`, README acknowledgements и пользовательская проверка

Цель:
добавить демонстрационный материал и обязательные credits без раздувания README.

### Результаты

- в `sample.md` добавлены Mermaid-примеры
- минимум один flowchart example
- минимум один sequence diagram example
- минимум один class или state diagram example
- примеры компактные и подходят для ручной проверки viewer
- в `README.md` добавлен нижний блок credits/acknowledgements
- в `README.en.md` добавлен нижний блок credits/acknowledgements
- credits упоминают Naiad, MIT license и Mermaid project/syntax

### Правила

- `sample.md` не должен превращаться в огромную галерею
- README-блок должен быть внизу и не должен перегружать первое впечатление продукта
- credits должны быть нейтральными и фактическими

### Готово когда

- пользователь может открыть `sample.md` и увидеть реальные Mermaid-диаграммы
- README содержит обязательное упоминание Naiad/Mermaid внизу
- русская и английская версии README синхронизированы по смыслу

## M8 — Build, Native AOT и regression validation

Цель:
проверить, что Mermaid support не нарушил production-oriented путь MarkMello.

### Результаты

- проходит обычный build
- проходят unit tests
- проходит тестовый запуск viewer с `sample.md`
- проверен release build
- проверен Native AOT publish для применимой целевой платформы
- проверены trim/AOT warnings
- измерен impact на документ без диаграмм
- измерен impact на документ с representative Mermaid diagrams

### Правила

- если Native AOT/trim ломается из-за Naiad или SVG path, это блокер ADR/реализации
- нельзя обходить проблему переводом Mermaid в optional external renderer
- нельзя ухудшать документ без диаграмм ради поддержки диаграмм
- любые новые предупреждения publish должны быть разобраны, а не проигнорированы

### Готово когда

- feature проходит build/test/publish gates
- документ без диаграмм не получает тяжёлый runtime path
- документ с Mermaid диаграммами открывается и показывает диаграммы
- runtime failures отдельных диаграмм отображаются как controlled error block

## Невходит в эту реализацию

- PlantUML rendering
- выбор PlantUML backend
- Graphviz rendering
- plugin runtime для diagram dialects
- marketplace/extensions model
- Mermaid live editing toolbar
- интерактивные Mermaid actions, требующие browser/JS runtime
- network rendering
- external process rendering

## Минимальный acceptance checklist

- ` ```mermaid ` распознаётся как diagram block
- обычные code fences не ломаются
- Mermaid renderer является обязательным registered renderer
- Naiad рендерит SVG in-process
- SVG отображается в native viewer
- `sample.md` содержит Mermaid examples
- README credits добавлены внизу
- WebView/Node/network/external process отсутствуют
- editor subsystem не участвует в реализации
- ошибка одной диаграммы не ломает весь документ
- Native AOT/publish path проверен
