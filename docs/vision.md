# MarkMello Vision

## Product vision

**MarkMello** — это быстрый cross-platform desktop Markdown viewer с опциональным режимом редактирования.

Продукт предназначен для сценария, в котором пользователь уже имеет `.md` файл и хочет открыть его сразу, без запуска тяжёлой IDE, workspace-среды или многофункционального редактора.

Основной опыт использования — **мгновенное открытие документа и комфортное чтение**.

Редактирование является вторичной возможностью и не должно ухудшать скорость, простоту и лёгкость режима просмотра.

## Product promise

MarkMello открывает Markdown-файлы быстро, показывает содержимое чисто и спокойно, и остаётся лёгким инструментом даже при наличии editor mode.

## Core value

Главная ценность продукта — не количество функций, а качество первого действия:

**пользователь открывает `.md` и почти сразу читает документ.**

## Positioning

MarkMello — это не markdown IDE, не knowledge-base платформа и не note-taking suite.

Это desktop viewer-first приложение для Markdown с аккуратным встроенным edit mode.

## UX goal

По умолчанию пользователь должен видеть не “приложение”, а **документ**.

Интерфейс обязан быть вторичным по отношению к содержимому.

## Success criteria

Успех MarkMello определяется следующими признаками:

- файл открывается быстро и предсказуемо
- документ читается без визуального шума
- приложение не навязывает редактирование
- editor mode включается явно и не влияет на cold start viewer mode
- поведение остаётся простым и понятным на Windows, macOS и Linux

## Product boundaries

### In scope
- быстрое открытие `.md`
- чистый режим чтения
- тема: system / light / dark
- настройка типографики чтения
- edit mode по явному действию
- split view для редактирования
- базовые editor-assist функции
- устойчивый рендер типичного Markdown

### Out of scope by default
- workspace-модель
- project tree как основная концепция
- встроенная sync/cloud-платформа
- database-first хранение заметок
- plugin marketplace
- heavy extensibility platform
- IDE-grade authoring environment
- multi-pane knowledge management system
