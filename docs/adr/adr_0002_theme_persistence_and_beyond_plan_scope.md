# ADR-0002: Theme persistence model and accepted beyond-plan scope

## Status

Accepted

## Date

2026-04-19

## Context

В `implementation-plan.md` для M4 упомянуты theme settings и persistence.

В предыдущем анализе репозитория отсутствие отдельного полноценного пользовательского режима `System / Light / Dark` было отмечено как потенциально незакрытый пункт плана.

19 апреля 2026 было дано уточнение по продуктовой интерпретации этого поведения:

- текущая модель theme behavior остаётся как есть
- при открытии/старте применяется одна из двух явно сохранённых тем (`Light` или `Dark`), если такая тема уже сохранена
- если явная сохранённая тема отсутствует, приложение должно следовать системной теме
- отдельный пользовательский третий persistent-mode `System` добавлять не требуется

Также в репозитории уже появилась часть функциональности, которая не была явно описана в исходных milestone deliverables и должна быть зафиксирована не как случайный scope drift, а как принятая часть baseline.

## Decision

### 1. Theme persistence model is frozen as two explicit themes plus system fallback

Принимается следующая модель:

- пользовательская persistable theme choice ограничивается двумя явными значениями: `Light` и `Dark`
- если одна из этих тем ранее сохранена, именно она применяется при следующем старте/открытии
- если явная тема ещё не сохранена, приложение использует системную тему
- `System` в этой модели является fallback/bootstrap behavior, а не отдельным пользовательским persistent choice
- отдельный UI для явного выбора `System` не требуется
- текущее quick-toggle поведение `Light <-> Dark` остаётся целевым

### 2. Plan interpretation is updated

Пункт плана про тему должен далее интерпретироваться так:

- приложение корректно стартует в системной теме, если у пользователя ещё нет явного сохранённого выбора
- приложение умеет сохранять и восстанавливать одну из двух явных тем: `Light` или `Dark`
- отсутствие отдельного tri-state selector `System / Light / Dark` не считается backlog gap и не требует дополнительной реализации

Следствие:

- замечание о необходимости "доделать полноценный пользовательский theme mode System / Light / Dark" считается снятым

### 3. Accepted beyond-plan scope already implemented in repository

Следующие возможности считаются принятым scope сверх исходного milestone-плана и не должны рассматриваться как отклонение, требующее отката.

#### 3.1 Document-wide selection architecture in the native viewer

Зафиксирована и принята архитектура document-wide selection, в которой:

- selection принадлежит всему `MarkdownDocumentView`, а не отдельным текстовым контролам
- используется канонический text map документа для global offsets и copy semantics
- поддерживается непрерывное выделение и копирование через несколько block types
- поддерживаются keyboard shortcuts для `Select All` и `Copy`
- реализованы word/block selection gestures и policy для безопасной активации ссылок только при collapsed selection

Этот scope уже отдельно поддержан в `ADR-0001`, а данным ADR дополнительно фиксируется как принятая функциональность сверх исходного milestone backlog.

#### 3.2 Advanced image support in the markdown pipeline and viewer

Сверх явно перечисленных deliverables M3 уже реализован расширенный image path:

- block-level и inline image representation в document model
- извлечение изображений из типичных HTML markdown-cases (`<img>`, `<picture>` и подобные)
- resolver для local/file/data/http(s) image sources
- size and MIME guards, чтобы image loading не ломал viewer fast path
- отдельный native image-flow fragment в viewer

Итог:

- типичные README- и badge-heavy документы поддерживаются заметно лучше, чем это требовал минимальный план

#### 3.3 Additional viewer polish beyond explicit milestone wording

Также принят как сверхплановый polish следующий уже реализованный scope:

- ускоренное wheel scrolling для reading scenario
- hover-reveal поведение chrome-элементов shell
- дополнительные детали native rendering behavior для links, code, tables и image placeholders

## Consequences

## Positive

- roadmap interpretation становится однозначной: отдельный user-facing `System` mode не нужен
- упрощается settings UX и persistence model
- уже реализованные сверхплановые viewer-capabilities считаются легитимной частью продукта
- будущие ревью плана не должны считать отсутствие tri-state theme picker дефектом

## Neutral

- internal enum/state может продолжать использовать `System` как fallback/initial state
- существующий theme storage и current quick-toggle flow не требуют концептуальной переработки

## Negative

- пользователь не получает явную команду вернуться в persistent `System` mode после выбора `Light` или `Dark`
- документация и будущие плановые заметки должны аккуратно различать `system fallback` и `explicit saved theme`

## Planning impact

После принятия этого ADR:

- пункт про отдельный полноценный `System / Light / Dark` пользовательский режим исключается из списка оставшихся задач
- всё, что уже реализовано сверх исходного milestone-плана и перечислено выше, считается принятым baseline, а не временным экспериментом
- дальнейшее планирование должно идти уже от фактического состояния репозитория, где M0-M3 закрыты, M4 в основном закрыт, а часть M5 уже присутствует

## Final statement

Для MarkMello правильной интерпретацией theme behavior является не полноценный tri-state persistent theme selector, а модель:

- `Light` или `Dark`, если пользователь уже сделал явный выбор
- системная тема, если явного сохранённого выбора ещё нет

Одновременно с этим проект официально принимает уже реализованный document-wide selection path, advanced image support и дополнительный viewer polish как допустимый и полезный scope сверх исходного implementation plan.
