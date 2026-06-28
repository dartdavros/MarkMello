![MarkMello](assets/cover.png)

# MarkMello

[English](README.en.md)

**MarkMello — приложение для быстрого открытия и чтения Markdown-файлов с дополнительным режимом редактирования.**

## Что умеет MarkMello

MarkMello позволяет:

- быстро открывать Markdown-файлы в режиме просмотра;
- настраивать удобный режим чтения: тему, размер шрифта, высоту строки и ширину области документа;
- при необходимости переходить в режим редактирования и вносить правки в файл.

## Чем отличается от обычных Markdown-редакторов

MarkMello сначала открывает файл для чтения.

Редактирование не является основным режимом запуска: оно включается вручную, когда нужно внести правки.

## Установка

Скачайте актуальную сборку из раздела [Releases](../../releases/latest).

### Windows

1. Скачайте `MarkMello-setup-win-x64.exe` или `MarkMello-setup-win-arm64.exe`, в зависимости от архитектуры компьютера.
2. Запустите установщик.
3. Откройте MarkMello из меню Start или откройте `.md` файл через MarkMello.

### macOS

1. Скачайте `MarkMello-macos-arm64.dmg` для Apple Silicon или `MarkMello-macos-x64.dmg` для Intel Mac.
2. Откройте DMG.
3. Перетащите `MarkMello.app` в `Applications`.
4. Запустите приложение из `Applications`.

### Linux

Если к release приложен Linux AppImage, запустите его так:

```bash
chmod +x MarkMello-linux-x86_64.AppImage
./MarkMello-linux-x86_64.AppImage
```

Если для нужного release нет Linux asset, соберите приложение из исходников.

## Временные сборки без подписи разработчика

Текущие публичные сборки MarkMello временно распространяются без подписи разработчика. Из-за этого Windows или macOS могут показать предупреждение при первом запуске.

Это временное ограничение distribution pipeline. Подпись разработчика и нормальная notarization/signing-цепочка будут добавлены в будущем.

### Windows: обход SmartScreen

Если Windows показывает предупреждение SmartScreen:

1. Нажмите `Подробнее`.
2. Нажмите `Выполнить в любом случае`.

Если Windows пометила скачанный файл как заблокированный:

1. Откройте свойства установочного файла.
2. Включите `Разблокировать`, если такой пункт доступен.
3. Примените изменения и запустите установщик снова.

### macOS: обход Gatekeeper

Если macOS сообщает, что приложение повреждено, не может быть проверено или не может быть открыто из-за неизвестного разработчика:

1. Откройте `Системные настройки`.
2. Перейдите в `Конфиденциальность и безопасность`.
3. Найдите сообщение о заблокированном `MarkMello`.
4. Нажмите `Открыть всё равно`.
5. Подтвердите запуск.

Если нужно разово снять quarantine-флаг вручную:

```bash
xattr -dr com.apple.quarantine /Applications/MarkMello.app
open /Applications/MarkMello.app
```

## Сборка из исходников

Требуется .NET SDK 9.

```bash
dotnet restore ./MarkMello.sln
dotnet build ./MarkMello.sln
```

Запуск проекта:

```bash
dotnet run --project ./src/MarkMello.Desktop/MarkMello.Desktop.csproj
```

Открытие файла из командной строки:

```bash
dotnet run --project ./src/MarkMello.Desktop/MarkMello.Desktop.csproj -- ./sample.md
```

## Горячие клавиши

| Действие | Windows / Linux | macOS |
| --- | --- | --- |
| Открыть файл | `Ctrl+O` | `Cmd+O` |
| Переключить режим редактирования | `Ctrl+E` | `Cmd+E` |
| Сохранить | `Ctrl+S` | `Cmd+S` |
| Сохранить как | `Ctrl+Shift+S` | `Cmd+Shift+S` |

## Идеи и предложения

Есть идея или предложение? Напишите в [GitHub Discussions: Ideas](https://github.com/dartdavros/MarkMello/discussions/categories/ideas).

## Лицензия

Проект распространяется по лицензии GPL-3.0.

См. файл [LICENSE](LICENSE).

## Благодарности

Поддержка диаграмм в MarkMello основана на open-source проектах:

- [Naiad](https://github.com/NaiadDiagrams/Naiad) — .NET-библиотека, рендерящая Mermaid-диаграммы в SVG in-process, без браузера и внешних рантаймов. MIT License.
- [Mermaid](https://github.com/mermaid-js/mermaid) — синтаксис и спецификация диаграмм.
