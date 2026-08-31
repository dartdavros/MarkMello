# MarkMello Architecture

## Overview

MarkMello — локальное desktop-приложение для быстрого открытия и чтения Markdown-файлов с опциональным режимом редактирования.

Архитектура строится вокруг принципа **viewer-first**:
- базовый сценарий — открыть `.md` файл и почти сразу показать читаемый документ
- режим редактирования вторичен и не должен влиять на критический путь запуска viewer mode

## Architectural goals

- минимальный cold start
- минимальный time-to-first-window
- минимальный time-to-readable-document
- предсказуемый рендер Markdown
- изоляция viewer и editor subsystem
- простая поддерживаемая cross-platform архитектура

## High-level design

Архитектура делится на два основных режима:

### 1. Viewer mode
Основной и приоритетный режим.

Отвечает за:
- открытие файла по ассоциации или через приложение
- чтение содержимого файла
- преобразование Markdown в отображаемую модель
- рендер документа
- применение темы и настроек чтения

Критическое требование:
**viewer mode должен запускаться по минимальному пути и не инициализировать editor-specific зависимости.**

### 2. Editor mode
Вторичный режим, доступный по явному действию пользователя.

Отвечает за:
- split view
- редактирование исходного Markdown
- toolbar форматирования
- подсказки
- подсветку ошибок
- дополнительные editor-assist возможности

Критическое требование:
**editor mode загружается лениво и не участвует в startup path viewer mode.**

## Architectural principles

### Viewer and editor separation
Viewer subsystem и editor subsystem должны быть логически и технически разделены.

Допустимо иметь:
- общую модель документа
- общие контракты настроек
- общие базовые инфраструктурные сервисы

Недопустимо:
- принудительно создавать editor services на старте viewer mode
- связывать рендер документа с обязательным наличием editor engine
- включать split layout в initial visual tree

### File-first runtime
Открытие локального файла является главным сценарием исполнения.

Приложение должно уметь корректно работать при старте:
- с путём к файлу из аргументов командной строки
- из системной ассоциации файлов
- через внутреннюю команду Open File

### Progressive initialization
Инициализация выполняется ступенчато.

#### Stage 1 — App bootstrap
- запуск приложения
- разбор аргументов
- определение пути к файлу
- подготовка минимального runtime-контекста

#### Stage 2 — First window
- создание главного окна
- применение темы
- отображение минимального shell

#### Stage 3 — Readable document
- чтение файла
- преобразование Markdown
- отображение документа

#### Stage 4 — Secondary features
- дополнительные controls
- вспомогательные команды
- необязательные UI-элементы

#### Stage 5 — Editor activation
- загрузка editor subsystem
- переход в split mode
- включение editor-assist функций

## Suggested solution structure

### Presentation layer
Отвечает за Avalonia UI.

Содержит:
- окна
- view
- view model
- команды UI
- theme integration
- typography settings
- split mode orchestration

### Application layer
Отвечает за use cases и orchestration.

Содержит:
- open file use case
- reload file use case
- switch theme use case
- switch to edit mode use case
- update reading preferences use case

### Domain layer
Отвечает за базовые модели и правила.

Содержит:
- document model
- markdown source model
- rendered document model
- reading preferences model
- theme mode model
- validation contracts

### Infrastructure layer
Отвечает за работу с внешней средой.

Содержит:
- file system access
- settings persistence
- markdown parser / renderer integration
- platform integration for file association and OS-specific behavior
- optional telemetry hooks for local performance measurements

## Key subsystems

### Document loading subsystem
Задачи:
- открыть файл
- проверить существование и доступность
- прочитать содержимое
- обработать ошибки чтения
- передать данные в markdown pipeline

Требования:
- минимальные аллокации по возможности
- отсутствие лишнего копирования данных без необходимости
- предсказуемая обработка больших, но типичных markdown-файлов

### Markdown rendering subsystem
Задачи:
- преобразовать Markdown в отображаемую модель
- поддержать типичный и ожидаемый набор Markdown-возможностей
- обеспечить стабильный результат рендера

Требования:
- быстрая работа на типичных документах
- отсутствие editor-specific зависимости
- устойчивость к неполному или частично некорректному markdown

### Reading experience subsystem
Задачи:
- отрисовать документ с комфортной шириной
- применить тему
- применить шрифтовые настройки
- обеспечить хорошую прокрутку и читаемость

Требования:
- контент в центре
- минимальный визуальный шум
- controls не доминируют над документом

### Editor subsystem
Задачи:
- показать исходный Markdown
- синхронизировать изменения с preview
- предоставлять toolbar, подсказки и подсветку ошибок

Требования:
- lazy loading
- независимость от startup path viewer mode
- деградация без влияния на просмотр, если editor-specific функциональность ограничена

### Settings subsystem
Задачи:
- сохранять тему
- сохранять параметры чтения
- загружать пользовательские предпочтения

Требования:
- быстрое чтение при старте
- безопасное поведение при повреждённых настройках
- разумные значения по умолчанию

## Startup performance rules

Следующие правила являются архитектурно обязательными:

- не инициализировать editor engine на старте viewer mode
- не монтировать split layout до входа в Edit
- не запускать тяжёлые фоновые процессы в fast path
- не выполнять индексирование коллекций файлов
- не делать сетевые вызовы в базовом сценарии
- не блокировать first window вторичными настройками и сервисами

## Error handling strategy

Ошибки должны обрабатываться предсказуемо и без разрушения базового UX.

### Cases
- файл не найден
- файл недоступен
- ошибка чтения
- ошибка парсинга markdown
- ошибка загрузки настроек

### Rules
- окно приложения всё равно должно открываться
- ошибка должна отображаться понятным образом
- пользователь должен понимать, что произошло и что можно сделать дальше
- ошибка editor subsystem не должна ломать viewer subsystem

## Cross-platform strategy

### Common core
Единая кодовая база для:
- document model
- application use cases
- markdown pipeline
- settings model
- основного UI

### Platform adaptation
Отдельные адаптеры для:
- file associations
- platform-specific startup behavior
- platform-specific shell integration
- различий в системной теме и поведении окна

## Observability and measurement

Так как производительность — ключевое свойство продукта, архитектура должна позволять измерять:
- startup time
- time to first window
- time to readable document
- editor activation time
- memory usage in viewer mode
- memory usage after entering edit mode

Метрики должны быть пригодны для локального development-профилирования и regression-контроля.

## Non-goals for architecture

Архитектура не должна заранее подготавливать продукт к превращению в:
- IDE
- workspace platform
- облачную систему заметок
- plugin runtime platform
- universal document editor

## Summary

MarkMello должен иметь архитектуру, в которой:
- viewer mode является главным сценарием
- editor mode изолирован и загружается по требованию
- startup path максимально короткий
- UI подчинён документу
- кроссплатформенность достигается без отказа от качественной desktop-интеграции
