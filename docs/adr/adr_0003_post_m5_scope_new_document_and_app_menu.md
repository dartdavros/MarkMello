# ADR-0003: Post-M5 scope for new document flow, app menu, and app-level settings

## Status

Accepted

## Date

2026-04-19

## Context

После фактического закрытия `M0-M4`, принятой интерпретации темы из `ADR-0002` и появления core-части `M5` в репозитории возник вопрос, что считать правильным следующим этапом продукта.

Исходный `M6` содержал смесь пунктов разной природы:

- platform polish и packaging
- editor-assist идеи
- визуальный polish viewer
- технические исследования вроде Native AOT

Часть этих пунктов полезна, но не все из них одинаково хорошо поддерживают product vision MarkMello:

- viewer-first desktop UX
- минимальный chrome
- file-first сценарий открытия
- explicit, but secondary, edit mode
- отсутствие лишней логики и сетевой активности на fast path

Одновременно стало ясно, что продукту не хватает не ещё одного editor-assist слоя, а аккуратного app-level shell completion:

- явного сценария `Create MD`
- app menu с базовыми действиями приложения
- отдельного места для app-level settings, не смешанного с reading preferences

## Decision

### 1. Near-term M6 scope is redefined

Ближайший этап после текущего baseline должен фокусироваться не на усилении editor-assist и не на дополнительном viewer polish, а на завершении app shell и desktop delivery path.

Из активного `M6` scope выводятся:

- syntax highlighting в source editor
- отдельный milestone для code block polish
- recent files
- Native AOT evaluation

Эти пункты не запрещены навсегда, но больше не считаются обязательным ближайшим шагом.

### 2. `Create MD` becomes an explicit secondary action

В продукт добавляется отдельный сценарий создания нового Markdown-файла.

Решение:

- на welcome screen слева от `Open file…` появляется outline-кнопка с иконкой `+` и текстом `Создать MD`
- `Open file…` остаётся primary CTA
- `Create MD` трактуется как осознанный вход в authoring path и сразу переводит пользователя в edit mode
- новый документ стартует как unsaved draft, а не как фиктивный file-backed document
- первый `Save` для такого draft идёт через существующий `Save As`

Следствие:

- product positioning остаётся viewer-first
- authoring path становится более полным и честным
- не требуется подделывать путь или идентичность файла до первого реального сохранения

### 3. App menu is added as a minimal top-left shell control

В shell добавляется icon-only кнопка меню в левом верхнем углу на уровне существующих top controls.

Решение:

- кнопка визуально следует тому же language, что и current topbar ghost controls
- по клику открывается левый popover в том же visual стиле, что и текущая reading settings panel
- app menu остаётся transient overlay, а не превращается в постоянный navigation rail, sidebar или desktop-style full menu bar

Первый уровень app menu:

- `Open file`
- `Close file`
- `Settings`

Поведение:

- `Open file` переиспользует существующий file-open flow
- `Close file` закрывает текущий документ, но не приложение
- если у документа есть unsaved changes, `Close file` должен использовать существующий dirty-state resolution flow
- если документ не открыт, `Close file` disabled

### 4. Reading settings and app settings are explicitly separated

Текущий правый popover с reading preferences не меняет своей роли.

Решение:

- правый `Aa`-popover продолжает быть только reading settings
- новый app menu содержит вход в отдельные app settings
- app settings не смешиваются с typography/theme controls для чтения

Состав app settings:

- language
- `Check for updates`
- `About`

UI-переход:

- `Settings` в app menu открывает не новый независимый тип окна, а следующий экран в том же popover flow

### 5. Update checks are manual and GitHub-based

Проверка обновлений принимается как app-level capability, но только в ручном режиме.

Решение:

- update check выполняется только по явному действию пользователя
- источник правды для обновлений — GitHub releases
- при старте приложения и в базовом reading fast path сетевые вызовы не выполняются

Следствие:

- сохраняется simple fast path
- продукт не вводит скрытый network tax в startup
- delivery model остаётся совместимым с будущим packaging решением

### 6. Language switch follows localization foundation, not ad-hoc branching

Выбор языка принимается как нужная app-level функция, но только после выделения локализуемого shell/resource слоя.

Решение:

- сначала локализуются shell strings и app chrome
- только затем добавляется language switch
- ad-hoc `if language == ...` по view models и code-behind считаются неверным путём

Следствие:

- language support не превращается в хаотичную размазку строк
- локализация остаётся ограниченной оболочкой приложения и не усложняет markdown rendering path

### 7. Overlay model should stay simple and mutually exclusive

Для app menu, app settings, about, updates и reading settings рекомендуется единая mutually-exclusive overlay model вместо набора независимых boolean flags.

Предпочтительная интерпретация:

- одновременно открыт только один overlay state
- `Esc`, outside-click и hover/visibility rules обслуживаются общей логикой shell

Это не требует жёсткой привязки к конкретному enum-имени, но фиксирует принцип: overlay system не должна разрастаться в набор несвязанных флагов.

## Ordered implementation sequence

Рекомендуемая последовательность:

1. `Create MD`
2. unsaved draft semantics + first-save behavior
3. `Close file`
4. app menu shell
5. app settings subview
6. `About`
7. manual update check
8. localization foundation + language switch
9. platform polish, file associations / activation
10. packaging

## Consequences

## Positive

- следующий этап разработки становится более product-shaped и меньше тянет продукт в сторону mini-IDE
- создаётся явный и аккуратный сценарий нового документа без ущерба для viewer-first startup
- app-level функции получают собственное место и перестают конкурировать с reading settings
- network activity остаётся вне fast path
- platform polish и packaging сохраняются в плане, но идут после стабилизации product shell

## Neutral

- edit mode по-прежнему остаётся secondary capability, просто у него появляется второй честный вход помимо открытия существующего файла
- часть старых M6 идей может вернуться позже отдельными решениями, если появится реальная продуктовая потребность

## Negative

- language support откладывается до подготовки resource layer, а не реализуется мгновенно
- packaging и platform integration формально смещаются дальше по очереди, потому что сначала доводится app shell
- app shell становится немного богаче, поэтому особенно важно не разрастить его сверх роли secondary chrome

## Planning impact

После принятия этого ADR `implementation-plan.md` должен трактовать `M6` как этап:

- secondary authoring entry points
- minimal app shell completion
- app-level settings
- platform delivery

а не как набор разрозненных editor-assist и polish-задач.

## Final statement

Для MarkMello правильный следующий шаг после фактического baseline `M0-M5` — не наращивание IDE-like функций, а аккуратное завершение viewer-first desktop shell:

- `Create MD` как secondary authoring entry
- app menu и app-level settings как отдельный слой поверх reading UX
- manual GitHub-based updates без network tax на startup
- language switch только после локализационной базы
- затем platform polish и packaging
