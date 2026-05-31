using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Domain;

namespace MarkMello.Presentation.Localization;

public sealed class LocalizationService : ObservableObject, ILocalizationService
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["WelcomeTagline"] = "A quiet place to read Markdown.",
        ["WelcomeCreateMd"] = "Create MD",
        ["WelcomeOpenFile"] = "Open file...",
        ["WelcomeDropHint"] = "or drop a .md file anywhere",
        ["TitleBarMinimize"] = "Minimize",
        ["TitleBarMaximize"] = "Maximize",
        ["TitleBarRestore"] = "Restore",
        ["TitleBarClose"] = "Close",
        ["AppMenuTooltip"] = "App menu",
        ["ThemeSwitchToDark"] = "Switch to dark theme",
        ["ThemeSwitchToLight"] = "Switch to light theme",
        ["EditToggleTooltip"] = "Toggle edit mode (Ctrl+E)",
        ["ReadingSettingsTooltip"] = "Reading preferences (Ctrl+,)",
        ["OverlayCloseMenu"] = "Close menu",
        ["OverlayBackToMenu"] = "Back to menu",
        ["OverlayCloseSettings"] = "Close settings",
        ["OverlayBackToSettings"] = "Back to settings",
        ["OverlayCloseAbout"] = "Close about",
        ["AppMenuHeader"] = "MENU",
        ["AppMenuOpenFileLabel"] = "Open file",
        ["AppMenuOpenFileHint"] = "Pick a Markdown document",
        ["AppMenuCloseFileLabel"] = "Close file",
        ["AppMenuCloseFileHint"] = "Return to the welcome screen",
        ["AppMenuExportPdfLabel"] = "Export to PDF",
        ["AppMenuExportPdfHint"] = "Save a PDF copy",
        ["AppMenuSettingsLabel"] = "Settings",
        ["AppMenuSettingsHint"] = "Language, updates, about",
        ["MetaCurrent"] = "Current",
        ["MetaOpen"] = "Open",
        ["AppSettingsHeader"] = "SETTINGS",
        ["LanguageLabel"] = "Language",
        ["LanguageHint"] = "Shell and dialogs",
        ["LanguageSystem"] = "System",
        ["LanguageEnglish"] = "English",
        ["LanguageRussian"] = "Russian",
        ["UpdatesLabel"] = "Updates",
        ["UpdatesHint"] = "Manual GitHub release checks",
        ["AboutLabel"] = "About",
        ["AboutHint"] = "Version and product info",
        ["AboutHeader"] = "ABOUT",
        ["AboutVersionLabel"] = "Version",
        ["AboutVersionHint"] = "Current product build",
        ["AboutLicenseLabel"] = "License",
        ["AboutLicenseHint"] = "Project license",
        ["AboutCreditsLabel"] = "Credits",
        ["AboutCreatedByPrefix"] = "Created by ",
        ["AboutCreditsPeriod"] = ".",
        ["ReadingHeader"] = "READING",
        ["ReadingFontLabel"] = "Font",
        ["ReadingFontHint"] = "Document typeface",
        ["ReadingFontSerif"] = "Serif",
        ["ReadingFontSans"] = "Sans",
        ["ReadingFontMono"] = "Mono",
        ["ReadingSizeLabel"] = "Size",
        ["ReadingSizeHint"] = "Base font size",
        ["ReadingLineHeightLabel"] = "Line height",
        ["ReadingLineHeightHint"] = "Reading comfort",
        ["ReadingWidthLabel"] = "Width",
        ["ReadingWidthHint"] = "Measure of a line",
        ["ReadingWidthNarrow"] = "Narrow",
        ["ReadingWidthMedium"] = "Medium",
        ["ReadingWidthWide"] = "Wide",
        ["ReadingMinimapLabel"] = "Minimap",
        ["ReadingMinimapHint"] = "Document overview",
        ["ReadingMinimapAuto"] = "Auto",
        ["ReadingMinimapOn"] = "On",
        ["ReadingMinimapOff"] = "Off",
        ["StatusWordCount"] = "Words: {0:N0}",
        ["StatusReadTime"] = "Read time: {0} min",
        ["StatusOpen"] = "open",
        ["StatusPrefs"] = "prefs",
        ["DragDropHint"] = "Drop your Markdown file to open",
        ["DirtyPromptCancel"] = "Cancel",
        ["DirtyPromptDiscard"] = "Discard",
        ["DirtyPromptSave"] = "Save",
        ["LoadErrorOpenAnotherFile"] = "Open another file",
        ["LoadErrorTryAgain"] = "Try again",
        ["LoadErrorPress"] = "Press ",
        ["LoadErrorToDismiss"] = " to dismiss",
        ["EditorBoldTooltip"] = "Bold",
        ["EditorItalicTooltip"] = "Italic",
        ["EditorCodeTooltip"] = "Code",
        ["EditorLinkTooltip"] = "Link",
        ["EditorListTooltip"] = "List",
        ["EditorQuoteTooltip"] = "Quote",
        ["EditorSourceLabel"] = "SOURCE",
        ["ModeReading"] = "Reading",
        ["ModeEdit"] = "Edit",
        ["ModeReadShortcut"] = "read",
        ["ModeEditShortcut"] = "edit",
        ["UpdateCheckNow"] = "Check now",
        ["UpdateChecking"] = "Checking...",
        ["UpdateDownload"] = "Download update",
        ["UpdateDownloading"] = "Downloading...",
        ["UpdateOpenDownloaded"] = "Open update",
        ["UpdateLaunchInstaller"] = "Launch installer",
        ["UpdateOpenDmg"] = "Open DMG",
        ["UpdateRevealAppImage"] = "Reveal AppImage",
        ["UpdateBadgeManual"] = "Manual",
        ["UpdateBadgeAvailable"] = "Available",
        ["UpdateBadgeReady"] = "Ready",
        ["UpdateBadgeChecking"] = "Checking",
        ["UpdateBadgeDownloading"] = "Downloading",
        ["UpdateDefaultTitle"] = "Updates",
        ["UpdateDefaultMessage"] = "Manual GitHub release checks keep the startup path offline.",
        ["UpdateCheckingTitle"] = "Checking GitHub Releases",
        ["UpdateCheckingMessage"] = "Looking for a newer packaged build for this device.",
        ["UpdateUnavailableTitle"] = "Updates unavailable",
        ["UpdateUnavailableMessage"] = "This build has no GitHub Releases source configured yet.",
        ["UpdateUnsupportedPlatformTitle"] = "No packaged update for this runtime",
        ["UpdateUnsupportedPlatformMessage"] = "{0} {1} is not in the current release matrix.",
        ["UpdateUpToDateTitle"] = "You're up to date",
        ["UpdateUpToDateMessage"] = "Current build {0} already matches the latest published release ({1}).",
        ["UpdateAvailableTitle"] = "Update {0} available",
        ["UpdateAvailableMessage"] = "{0} is ready for {1} {2}.",
        ["UpdateCheckFailedTitle"] = "Couldn't check for updates",
        ["UpdateDownloadTitle"] = "Downloading {0}",
        ["UpdateDownloadMessage"] = "Saving {0} from GitHub Releases.",
        ["UpdateReadyTitle"] = "Update ready",
        ["UpdateReadyLaunchInstaller"] = "{0} downloaded. Launch the installer to continue the native Windows upgrade flow.",
        ["UpdateReadyOpenDmg"] = "{0} downloaded. Open the DMG to continue with the native macOS install flow.",
        ["UpdateReadyRevealAppImage"] = "{0} downloaded. Reveal the AppImage, then replace your previous binary when you're ready.",
        ["UpdateReadyGeneric"] = "{0} downloaded.",
        ["UpdateDownloadFailedTitle"] = "Download failed",
        ["UpdateNativeFlowStartedTitle"] = "Native update flow started",
        ["UpdateNativeFlowStartedLaunchInstaller"] = "Installer launched. Follow the native upgrade flow.",
        ["UpdateNativeFlowStartedOpenDmg"] = "DMG opened. Continue with the native macOS install flow.",
        ["UpdateNativeFlowStartedRevealAppImage"] = "The AppImage was revealed in your file manager.",
        ["UpdateOpenDownloadedFailedTitle"] = "Couldn't open the downloaded update",
        ["ErrorFileNotFoundTitle"] = "Couldn't find that file",
        ["ErrorAccessDeniedTitle"] = "Access denied",
        ["ErrorReadFailureTitle"] = "Couldn't read the file",
        ["ErrorUnsupportedTypeTitle"] = "Unsupported file type",
        ["ErrorSupportedExtensions"] = "{0}{1}{1}Supported extensions: {2}",
        ["DirtyPromptTitle"] = "Unsaved changes",
        ["DirtyPromptOpenFile"] = "Save your changes before opening another document?",
        ["DirtyPromptCreateNewDocument"] = "Save your changes before creating a new document?",
        ["DirtyPromptCloseFile"] = "Save your changes before closing the current document?",
        ["DirtyPromptReload"] = "Save your changes before reloading the current document?",
        ["DirtyPromptLeaveEditMode"] = "Save your changes before returning to reading mode?",
        ["DirtyPromptCloseWindow"] = "Save your changes before closing MarkMello?",
        ["DirtyPromptContinue"] = "Save your changes before continuing?",
        ["SaveInvalidPath"] = "Couldn't save to this path: {0}",
        ["SaveAccessDenied"] = "Access denied: {0}",
        ["SaveWriteFailure"] = "Couldn't save the document: {0}",
        ["SaveGenericFailure"] = "Couldn't save the document.",
        ["ExportPdfInvalidPath"] = "Couldn't export PDF to this path: {0}",
        ["ExportPdfAccessDenied"] = "Access denied: {0}",
        ["ExportPdfWriteFailure"] = "Couldn't export PDF: {0}",
        ["ExportPdfGenericFailure"] = "Couldn't export PDF.",
        ["ExportPdfSuccessTitle"] = "Export complete",
        ["ExportPdfSuccessMessage"] = "File {0} was exported to PDF successfully.",
        ["ExportPdfSuccessOk"] = "OK",
        ["OpenDialogTitle"] = "Open Markdown file",
        ["SaveDialogTitle"] = "Save Markdown file",
        ["ExportPdfDialogTitle"] = "Export PDF file",
        ["MarkdownDocuments"] = "Markdown documents",
        ["PdfDocuments"] = "PDF documents",
        ["UntitledFileName"] = "Untitled.md"
    };

    private static readonly Dictionary<string, string> Russian = new(StringComparer.Ordinal)
    {
        ["WelcomeTagline"] = "Тихое место для чтения Markdown.",
        ["WelcomeCreateMd"] = "Создать MD",
        ["WelcomeOpenFile"] = "Открыть файл...",
        ["WelcomeDropHint"] = "или перетащите сюда .md файл",
        ["TitleBarMinimize"] = "Свернуть",
        ["TitleBarMaximize"] = "Развернуть",
        ["TitleBarRestore"] = "Восстановить",
        ["TitleBarClose"] = "Закрыть",
        ["AppMenuTooltip"] = "Меню приложения",
        ["ThemeSwitchToDark"] = "Переключить на тёмную тему",
        ["ThemeSwitchToLight"] = "Переключить на светлую тему",
        ["EditToggleTooltip"] = "Переключить режим редактирования (Ctrl+E)",
        ["ReadingSettingsTooltip"] = "Параметры чтения (Ctrl+,)",
        ["OverlayCloseMenu"] = "Закрыть меню",
        ["OverlayBackToMenu"] = "Назад в меню",
        ["OverlayCloseSettings"] = "Закрыть настройки",
        ["OverlayBackToSettings"] = "Назад к настройкам",
        ["OverlayCloseAbout"] = "Закрыть раздел «О приложении»",
        ["AppMenuHeader"] = "МЕНЮ",
        ["AppMenuOpenFileLabel"] = "Открыть файл",
        ["AppMenuOpenFileHint"] = "Выбрать Markdown-документ",
        ["AppMenuCloseFileLabel"] = "Закрыть файл",
        ["AppMenuCloseFileHint"] = "Вернуться на экран приветствия",
        ["AppMenuExportPdfLabel"] = "Экспорт в PDF",
        ["AppMenuExportPdfHint"] = "Сохранить PDF-копию",
        ["AppMenuSettingsLabel"] = "Настройки",
        ["AppMenuSettingsHint"] = "Язык, обновления, сведения",
        ["MetaCurrent"] = "Текущий",
        ["MetaOpen"] = "Открыть",
        ["AppSettingsHeader"] = "НАСТРОЙКИ",
        ["LanguageLabel"] = "Язык",
        ["LanguageHint"] = "Оболочка и диалоги",
        ["LanguageSystem"] = "Системный",
        ["LanguageEnglish"] = "Английский",
        ["LanguageRussian"] = "Русский",
        ["UpdatesLabel"] = "Обновления",
        ["UpdatesHint"] = "Ручная проверка GitHub Releases",
        ["AboutLabel"] = "О приложении",
        ["AboutHint"] = "Версия и сведения о продукте",
        ["AboutHeader"] = "О ПРИЛОЖЕНИИ",
        ["AboutVersionLabel"] = "Версия",
        ["AboutVersionHint"] = "Текущая сборка продукта",
        ["AboutLicenseLabel"] = "Лицензия",
        ["AboutLicenseHint"] = "Лицензия проекта",
        ["AboutCreditsLabel"] = "Авторы",
        ["AboutCreatedByPrefix"] = "Создано ",
        ["AboutCreditsPeriod"] = ".",
        ["ReadingHeader"] = "ЧТЕНИЕ",
        ["ReadingFontLabel"] = "Шрифт",
        ["ReadingFontHint"] = "Гарнитура документа",
        ["ReadingFontSerif"] = "С засечками",
        ["ReadingFontSans"] = "Без засечек",
        ["ReadingFontMono"] = "Моно",
        ["ReadingSizeLabel"] = "Размер",
        ["ReadingSizeHint"] = "Базовый размер шрифта",
        ["ReadingLineHeightLabel"] = "Интерлиньяж",
        ["ReadingLineHeightHint"] = "Комфорт чтения",
        ["ReadingWidthLabel"] = "Ширина",
        ["ReadingWidthHint"] = "Длина строки",
        ["ReadingWidthNarrow"] = "Узкая",
        ["ReadingWidthMedium"] = "Средняя",
        ["ReadingWidthWide"] = "Широкая",
        ["ReadingMinimapLabel"] = "Миникарта",
        ["ReadingMinimapHint"] = "Обзор документа",
        ["ReadingMinimapAuto"] = "Авто",
        ["ReadingMinimapOn"] = "Вкл",
        ["ReadingMinimapOff"] = "Выкл",
        ["StatusWordCount"] = "Слов: {0:N0}",
        ["StatusReadTime"] = "Чтение: {0} мин",
        ["StatusOpen"] = "открыть",
        ["StatusPrefs"] = "настройки",
        ["DragDropHint"] = "Перетащите Markdown-файл, чтобы открыть его",
        ["DirtyPromptCancel"] = "Отмена",
        ["DirtyPromptDiscard"] = "Не сохранять",
        ["DirtyPromptSave"] = "Сохранить",
        ["LoadErrorOpenAnotherFile"] = "Открыть другой файл",
        ["LoadErrorTryAgain"] = "Повторить",
        ["LoadErrorPress"] = "Нажмите ",
        ["LoadErrorToDismiss"] = " чтобы закрыть",
        ["EditorBoldTooltip"] = "Жирный",
        ["EditorItalicTooltip"] = "Курсив",
        ["EditorCodeTooltip"] = "Код",
        ["EditorLinkTooltip"] = "Ссылка",
        ["EditorListTooltip"] = "Список",
        ["EditorQuoteTooltip"] = "Цитата",
        ["EditorSourceLabel"] = "ИСХОДНИК",
        ["ModeReading"] = "Чтение",
        ["ModeEdit"] = "Редактирование",
        ["ModeReadShortcut"] = "читать",
        ["ModeEditShortcut"] = "править",
        ["UpdateCheckNow"] = "Проверить",
        ["UpdateChecking"] = "Проверка...",
        ["UpdateDownload"] = "Скачать обновление",
        ["UpdateDownloading"] = "Загрузка...",
        ["UpdateOpenDownloaded"] = "Открыть обновление",
        ["UpdateLaunchInstaller"] = "Запустить установщик",
        ["UpdateOpenDmg"] = "Открыть DMG",
        ["UpdateRevealAppImage"] = "Показать AppImage",
        ["UpdateBadgeManual"] = "Вручную",
        ["UpdateBadgeAvailable"] = "Доступно",
        ["UpdateBadgeReady"] = "Готово",
        ["UpdateBadgeChecking"] = "Проверка",
        ["UpdateBadgeDownloading"] = "Загрузка",
        ["UpdateDefaultTitle"] = "Обновления",
        ["UpdateDefaultMessage"] = "Ручная проверка GitHub Releases не нагружает старт приложения сетью.",
        ["UpdateCheckingTitle"] = "Проверка GitHub Releases",
        ["UpdateCheckingMessage"] = "Ищем более новую сборку для этого устройства.",
        ["UpdateUnavailableTitle"] = "Обновления недоступны",
        ["UpdateUnavailableMessage"] = "Для этой сборки пока не настроен источник GitHub Releases.",
        ["UpdateUnsupportedPlatformTitle"] = "Для этой среды нет пакетного обновления",
        ["UpdateUnsupportedPlatformMessage"] = "{0} {1} отсутствует в текущей матрице релизов.",
        ["UpdateUpToDateTitle"] = "У вас актуальная версия",
        ["UpdateUpToDateMessage"] = "Текущая сборка {0} уже совпадает с последним опубликованным релизом ({1}).",
        ["UpdateAvailableTitle"] = "Доступно обновление {0}",
        ["UpdateAvailableMessage"] = "{0} готов для {1} {2}.",
        ["UpdateCheckFailedTitle"] = "Не удалось проверить обновления",
        ["UpdateDownloadTitle"] = "Загрузка {0}",
        ["UpdateDownloadMessage"] = "Сохраняем {0} из GitHub Releases.",
        ["UpdateReadyTitle"] = "Обновление готово",
        ["UpdateReadyLaunchInstaller"] = "{0} загружен. Запустите установщик, чтобы продолжить нативное обновление Windows.",
        ["UpdateReadyOpenDmg"] = "{0} загружен. Откройте DMG, чтобы продолжить нативную установку на macOS.",
        ["UpdateReadyRevealAppImage"] = "{0} загружен. Покажите AppImage и замените предыдущий бинарник, когда будете готовы.",
        ["UpdateReadyGeneric"] = "{0} загружен.",
        ["UpdateDownloadFailedTitle"] = "Ошибка загрузки",
        ["UpdateNativeFlowStartedTitle"] = "Запущен нативный сценарий обновления",
        ["UpdateNativeFlowStartedLaunchInstaller"] = "Установщик запущен. Продолжайте обновление через нативный сценарий.",
        ["UpdateNativeFlowStartedOpenDmg"] = "DMG открыт. Продолжайте установку через нативный сценарий macOS.",
        ["UpdateNativeFlowStartedRevealAppImage"] = "AppImage показан в файловом менеджере.",
        ["UpdateOpenDownloadedFailedTitle"] = "Не удалось открыть загруженное обновление",
        ["ErrorFileNotFoundTitle"] = "Не удалось найти файл",
        ["ErrorAccessDeniedTitle"] = "Доступ запрещён",
        ["ErrorReadFailureTitle"] = "Не удалось прочитать файл",
        ["ErrorUnsupportedTypeTitle"] = "Неподдерживаемый тип файла",
        ["ErrorSupportedExtensions"] = "{0}{1}{1}Поддерживаемые расширения: {2}",
        ["DirtyPromptTitle"] = "Есть несохранённые изменения",
        ["DirtyPromptOpenFile"] = "Сохранить изменения перед открытием другого документа?",
        ["DirtyPromptCreateNewDocument"] = "Сохранить изменения перед созданием нового документа?",
        ["DirtyPromptCloseFile"] = "Сохранить изменения перед закрытием текущего документа?",
        ["DirtyPromptReload"] = "Сохранить изменения перед перезагрузкой текущего документа?",
        ["DirtyPromptLeaveEditMode"] = "Сохранить изменения перед возвратом в режим чтения?",
        ["DirtyPromptCloseWindow"] = "Сохранить изменения перед закрытием MarkMello?",
        ["DirtyPromptContinue"] = "Сохранить изменения перед продолжением?",
        ["SaveInvalidPath"] = "Не удалось сохранить по этому пути: {0}",
        ["SaveAccessDenied"] = "Доступ запрещён: {0}",
        ["SaveWriteFailure"] = "Не удалось сохранить документ: {0}",
        ["SaveGenericFailure"] = "Не удалось сохранить документ.",
        ["ExportPdfInvalidPath"] = "Не удалось экспортировать PDF по этому пути: {0}",
        ["ExportPdfAccessDenied"] = "Доступ запрещён: {0}",
        ["ExportPdfWriteFailure"] = "Не удалось экспортировать PDF: {0}",
        ["ExportPdfGenericFailure"] = "Не удалось экспортировать PDF.",
        ["ExportPdfSuccessTitle"] = "Экспорт выполнен",
        ["ExportPdfSuccessMessage"] = "Экспорт файла {0} в формат PDF выполнен успешно.",
        ["ExportPdfSuccessOk"] = "OK",
        ["OpenDialogTitle"] = "Открыть Markdown-файл",
        ["SaveDialogTitle"] = "Сохранить Markdown-файл",
        ["ExportPdfDialogTitle"] = "Экспорт PDF-файла",
        ["MarkdownDocuments"] = "Markdown-документы",
        ["PdfDocuments"] = "PDF-документы",
        ["UntitledFileName"] = "Безымянный.md"
    };

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    private AppLanguage _selectedLanguage;
    private AppLanguage _effectiveLanguage;
    private CultureInfo _culture = EnglishCulture;

    public LocalizationService()
        : this(AppLanguage.System)
    {
    }

    public LocalizationService(AppLanguage initialLanguage)
    {
        SetLanguage(initialLanguage);
    }

    public AppLanguage SelectedLanguage => _selectedLanguage;

    public AppLanguage EffectiveLanguage => _effectiveLanguage;

    public CultureInfo Culture => _culture;

    public string this[string key] => ResolveString(key);

    public string Format(string key, params object?[] args)
        => string.Format(_culture, ResolveString(key), args);

    public void SetLanguage(AppLanguage language)
    {
        var normalized = NormalizeLanguage(language);
        var effective = ResolveEffectiveLanguage(normalized);
        var culture = ResolveCulture(effective);

        var selectedChanged = _selectedLanguage != normalized;
        var effectiveChanged = _effectiveLanguage != effective;
        var cultureChanged = !_culture.Equals(culture);
        if (!selectedChanged && !effectiveChanged && !cultureChanged)
        {
            return;
        }

        _selectedLanguage = normalized;
        _effectiveLanguage = effective;
        _culture = culture;

        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(EffectiveLanguage));
        OnPropertyChanged(nameof(Culture));
        NotifyLocalizedTextChanged();
    }

    private void NotifyLocalizedTextChanged()
    {
        // Avalonia indexer bindings may subscribe to either the CLR indexer
        // property name (Item) or the common WPF-style indexer marker (Item[]).
        // Raising both keeps every active shell/view binding refreshed when the
        // language changes. The empty name is the standard full-refresh signal.
        OnPropertyChanged("Item");
        OnPropertyChanged("Item[]");
        OnPropertyChanged(string.Empty);
    }

    private string ResolveString(string key)
    {
        var primary = _effectiveLanguage == AppLanguage.Russian ? Russian : English;
        if (primary.TryGetValue(key, out var value))
        {
            return value;
        }

        if (English.TryGetValue(key, out value))
        {
            return value;
        }

        return $"[[{key}]]";
    }

    private static AppLanguage NormalizeLanguage(AppLanguage language)
        => language switch
        {
            AppLanguage.English => AppLanguage.English,
            AppLanguage.Russian => AppLanguage.Russian,
            _ => AppLanguage.System
        };

    private static AppLanguage ResolveEffectiveLanguage(AppLanguage selectedLanguage)
    {
        if (selectedLanguage is AppLanguage.English or AppLanguage.Russian)
        {
            return selectedLanguage;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Russian
            : AppLanguage.English;
    }

    private static CultureInfo ResolveCulture(AppLanguage language)
        => language == AppLanguage.Russian ? RussianCulture : EnglishCulture;
}
