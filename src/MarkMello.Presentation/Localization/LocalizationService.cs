using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Domain;

namespace MarkMello.Presentation.Localization;

public sealed partial class LocalizationService : ObservableObject, ILocalizationService
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
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
        ["AppMenuSettingsLabel"] = "Settings",
        ["AppMenuSettingsHint"] = "Language, updates, about",
        ["MetaCurrent"] = "Current",
        ["MetaOpen"] = "Open",
        ["AppSettingsHeader"] = "SETTINGS",
        ["AlwaysOpenDocumentsInEditModeLabel"] = "Open documents in edit mode",
        ["AlwaysOpenDocumentsInEditModeHint"] = "Skip reading mode when opening a file",
        ["LanguageLabel"] = "Language",
        ["LanguageHint"] = "Shell and dialogs",
        ["LanguageSystem"] = "System",
        ["LanguageEnglish"] = "English",
        ["LanguageRussian"] = "Russian",
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
        ["WindowBorderLabel"] = "Window border",
        ["WindowBorderHint"] = "Outline the window edge",
        ["WindowBorderAuto"] = "Auto",
        ["WindowBorderOn"] = "On",
        ["WindowBorderOff"] = "Off",
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
        ["EditorProtectedImageDataMessage"] = "Embedded image data can only be edited as a whole line.",
        ["ContextCopy"] = "Copy",
        ["ContextSelectAll"] = "Select all",
        ["ContextCopyLink"] = "Copy link",
        ["ContextCopyLinks"] = "Copy links",
        ["ContextCopyTelegramMarkdown"] = "Copy selection as Telegram Markdown",
        ["CodeCopyTooltip"] = "Copy code",
        ["EditorSourceLabel"] = "SOURCE",
        ["ModeReading"] = "Reading",
        ["ModeEdit"] = "Edit",
        ["ModeReadShortcut"] = "read",
        ["ModeEditShortcut"] = "edit",
        ["FindPlaceholder"] = "Find in document",
        ["FindPreviousTooltip"] = "Previous match (Shift+Enter)",
        ["FindNextTooltip"] = "Next match (Enter)",
        ["FindCloseTooltip"] = "Close search (Esc)",
        ["FindResultCount"] = "{0} of {1}",
        ["FindNoResults"] = "No results",
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
        ["OpenDialogTitle"] = "Open Markdown file",
        ["OpenFolderDialogTitle"] = "Open folder",
        ["ExternalChangeTitle"] = "The file changed on disk",
        ["ExternalChangeReload"] = "Reload",
        ["ExternalChangeKeep"] = "Keep my edits",
        ["TabDeletedSuffix"] = "(deleted)",
        ["SidebarCollapse"] = "Collapse sidebar",
        ["AppMenuToggleSidebarLabel"] = "File Tree",
        ["AppMenuToggleSidebarHintShow"] = "Show the tree of the open folder",
        ["AppMenuToggleSidebarHintHide"] = "Hide the tree, keep the folder open",
        ["SidebarFooterDocuments"] = "Documents: {0}",
        ["SidebarFooterDirty"] = "Unsaved: {0}",
        ["SidebarNewFile"] = "New File",
        ["SidebarNewFolder"] = "New Folder",
        ["TreeRename"] = "Rename",
        ["TreeDuplicate"] = "Duplicate",
        ["TreeDelete"] = "Delete",
        ["TreeOpenInNewTab"] = "Open in New Tab",
        ["TreeRevealInExplorerWindows"] = "Show in Explorer",
        ["TreeRevealInExplorerMacOS"] = "Show in Finder",
        ["TreeRevealInExplorerLinux"] = "Show in File Manager",
        ["TreeNameTaken"] = "A file with this name already exists",
        ["TreeFolderNameTaken"] = "A folder with this name already exists",
        ["TreeInvalidChars"] = """These characters aren't allowed: \ / : * ? " < > |""",
        ["TreeReservedName"] = "This name is reserved by the system",
        ["TreeOperationFailed"] = "The operation failed",
        ["DeleteFileTitle"] = "Delete \"{0}\"?",
        ["DeleteFileBody"] = "The file will be moved to the recycle bin. Its open tab will close.",
        ["DeleteFolderTitle"] = "Delete folder \"{0}\"?",
        ["DeleteFolderBody"] = "The folder will be moved to the recycle bin.",
        ["DeleteFolderNonEmptyTitle"] = "Delete folder \"{0}\" and everything in it?",
        ["DeleteFolderNonEmptyBody"] = "The folder has {1} items. Everything will be moved to the recycle bin. Open tabs from this folder will close.",
        ["DeletePermanentBody"] = "The recycle bin is not available here. The item will be deleted permanently and cannot be restored.",
        ["DeleteConfirm"] = "Delete",
        ["DeleteCancel"] = "Cancel",
        ["FileOpErrorTitle"] = "Couldn't delete \"{0}\"",
        ["FileOpErrorClose"] = "Close",
        ["SidebarSearchPlaceholder"] = "Search files",
        ["SidebarSearchReset"] = "Esc to clear search",
        ["SidebarSearchEmpty"] = "No matches in this folder",
        ["SidebarSearchMatches"] = "MATCHES",
        ["SidebarSearchTruncated"] = "Showing the first matches only. Narrow your query.",
        ["TabsOverflow"] = "{0} more",
        ["TabsOverflowHeader"] = "OPEN TABS",
        ["TabsCloseOthers"] = "Close Others",
        ["TabClose"] = "Close tab",
        ["EmptySurfaceTitle"] = "No document selected",
        ["EmptySurfaceHint"] = "Pick a file on the left to open it in a tab.",
        ["StatusCloseTab"] = "close tab",
        ["StatusSwitchTabs"] = "switch tabs",
        ["AppMenuOpenFolderLabel"] = "Open Folder",
        ["AppMenuOpenFolderHint"] = "Show the file tree on the left",
        ["AppMenuCloseFolderLabel"] = "Close Folder",
        ["AppMenuCloseFolderHint"] = "Back to single-file viewing",
        ["SidebarTooltip"] = "Files in this folder",
        ["TreeNodeMissing"] = "Folder is gone",
        ["TreeNodeAccessDenied"] = "Access denied",
        ["TreeNodeReadError"] = "Couldn't read this folder",
        ["FolderErrorNotFoundTitle"] = "Couldn't find that folder",
        ["FolderErrorNotFoundDetails"] = "{0}",
        ["FolderErrorAccessDeniedTitle"] = "Access denied",
        ["FolderErrorAccessDeniedDetails"] = "{0}",
        ["FolderErrorReadTitle"] = "Couldn't read that folder",
        ["FolderErrorReadDetails"] = "{0}",
        ["SaveDialogTitle"] = "Save Markdown file",
        ["MarkdownDocuments"] = "Markdown documents",
        ["UntitledFileName"] = "Untitled.md"
    };

    private static readonly Dictionary<string, string> Russian = new(StringComparer.Ordinal)
    {
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
        ["AppMenuSettingsLabel"] = "Настройки",
        ["AppMenuSettingsHint"] = "Язык, обновления, сведения",
        ["MetaCurrent"] = "Текущий",
        ["MetaOpen"] = "Открыть",
        ["AppSettingsHeader"] = "НАСТРОЙКИ",
        ["AlwaysOpenDocumentsInEditModeLabel"] = "Открывать документы в режиме редактирования",
        ["AlwaysOpenDocumentsInEditModeHint"] = "Не переходить в режим чтения при открытии файла",
        ["LanguageLabel"] = "Язык",
        ["LanguageHint"] = "Оболочка и диалоги",
        ["LanguageSystem"] = "Системный",
        ["LanguageEnglish"] = "Английский",
        ["LanguageRussian"] = "Русский",
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
        ["WindowBorderLabel"] = "Рамка окна",
        ["WindowBorderHint"] = "Контур по краю окна",
        ["WindowBorderAuto"] = "Авто",
        ["WindowBorderOn"] = "Вкл",
        ["WindowBorderOff"] = "Выкл",
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
        ["EditorProtectedImageDataMessage"] = "Встроенные данные изображения можно редактировать только целой строкой.",
        ["ContextCopy"] = "Копировать",
        ["ContextSelectAll"] = "Выделить всё",
        ["ContextCopyLink"] = "Копировать ссылку",
        ["ContextCopyLinks"] = "Копировать ссылки",
        ["ContextCopyTelegramMarkdown"] = "Копировать выделение как Markdown для Telegram",
        ["CodeCopyTooltip"] = "Скопировать код",
        ["EditorSourceLabel"] = "ИСХОДНИК",
        ["ModeReading"] = "Чтение",
        ["ModeEdit"] = "Редактирование",
        ["ModeReadShortcut"] = "читать",
        ["ModeEditShortcut"] = "править",
        ["FindPlaceholder"] = "Поиск в документе",
        ["FindPreviousTooltip"] = "Предыдущее совпадение (Shift+Enter)",
        ["FindNextTooltip"] = "Следующее совпадение (Enter)",
        ["FindCloseTooltip"] = "Закрыть поиск (Esc)",
        ["FindResultCount"] = "{0} из {1}",
        ["FindNoResults"] = "Ничего не найдено",
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
        ["OpenDialogTitle"] = "Открыть Markdown-файл",
        ["OpenFolderDialogTitle"] = "Открыть папку",
        ["ExternalChangeTitle"] = "Файл изменён на диске",
        ["ExternalChangeReload"] = "Перезагрузить",
        ["ExternalChangeKeep"] = "Оставить мои правки",
        ["TabDeletedSuffix"] = "(удалён)",
        ["SidebarCollapse"] = "Свернуть сайдбар",
        ["AppMenuToggleSidebarLabel"] = "Дерево файлов",
        ["AppMenuToggleSidebarHintShow"] = "Показать дерево открытой папки",
        ["AppMenuToggleSidebarHintHide"] = "Скрыть дерево, папка останется открытой",
        ["SidebarFooterDocuments"] = "Документов: {0}",
        ["SidebarFooterDirty"] = "Изменено: {0}",
        ["SidebarNewFile"] = "Новый файл",
        ["SidebarNewFolder"] = "Новая папка",
        ["TreeRename"] = "Переименовать",
        ["TreeDuplicate"] = "Дублировать",
        ["TreeDelete"] = "Удалить",
        ["TreeOpenInNewTab"] = "Открыть в новой вкладке",
        ["TreeRevealInExplorerWindows"] = "Показать в проводнике",
        ["TreeRevealInExplorerMacOS"] = "Показать в Finder",
        ["TreeRevealInExplorerLinux"] = "Показать в файловом менеджере",
        ["TreeNameTaken"] = "Файл с таким именем уже есть",
        ["TreeFolderNameTaken"] = "Папка с таким именем уже есть",
        ["TreeInvalidChars"] = """Нельзя использовать: \ / : * ? " < > |""",
        ["TreeReservedName"] = "Это имя занято системой",
        ["TreeOperationFailed"] = "Операция не удалась",
        ["DeleteFileTitle"] = "Удалить «{0}»?",
        ["DeleteFileBody"] = "Файл будет перемещён в корзину. Открытая вкладка этого файла закроется.",
        ["DeleteFolderTitle"] = "Удалить папку «{0}»?",
        ["DeleteFolderBody"] = "Папка будет перемещена в корзину.",
        ["DeleteFolderNonEmptyTitle"] = "Удалить папку «{0}» и всё её содержимое?",
        ["DeleteFolderNonEmptyBody"] = "В папке {1} элементов. Всё будет перемещено в корзину. Открытые вкладки из этой папки закроются.",
        ["DeletePermanentBody"] = "Корзина здесь недоступна. Элемент будет удалён безвозвратно, восстановить его будет нельзя.",
        ["DeleteConfirm"] = "Удалить",
        ["DeleteCancel"] = "Отмена",
        ["FileOpErrorTitle"] = "Не удалось удалить «{0}»",
        ["FileOpErrorClose"] = "Закрыть",
        ["SidebarSearchPlaceholder"] = "Поиск по файлам",
        ["SidebarSearchReset"] = "Esc — сбросить поиск",
        ["SidebarSearchEmpty"] = "Ничего не найдено в этой папке",
        ["SidebarSearchMatches"] = "СОВПАДЕНИЯ",
        ["SidebarSearchTruncated"] = "Показаны только первые совпадения. Уточните запрос.",
        ["TabsOverflow"] = "ещё {0}",
        ["TabsOverflowHeader"] = "ОТКРЫТЫЕ ВКЛАДКИ",
        ["TabsCloseOthers"] = "Закрыть все, кроме активной",
        ["TabClose"] = "Закрыть вкладку",
        ["EmptySurfaceTitle"] = "Документ не выбран",
        ["EmptySurfaceHint"] = "Выберите файл в списке слева, чтобы открыть его во вкладке.",
        ["StatusCloseTab"] = "закрыть вкладку",
        ["StatusSwitchTabs"] = "между вкладками",
        ["AppMenuOpenFolderLabel"] = "Открыть папку",
        ["AppMenuOpenFolderHint"] = "Показать структуру файлов слева",
        ["AppMenuCloseFolderLabel"] = "Закрыть папку",
        ["AppMenuCloseFolderHint"] = "Вернуться к обычному просмотру",
        ["SidebarTooltip"] = "Файлы этой папки",
        ["TreeNodeMissing"] = "Папка исчезла",
        ["TreeNodeAccessDenied"] = "Доступ запрещён",
        ["TreeNodeReadError"] = "Не удалось прочитать папку",
        ["FolderErrorNotFoundTitle"] = "Не удалось найти папку",
        ["FolderErrorNotFoundDetails"] = "{0}",
        ["FolderErrorAccessDeniedTitle"] = "Доступ запрещён",
        ["FolderErrorAccessDeniedDetails"] = "{0}",
        ["FolderErrorReadTitle"] = "Не удалось прочитать папку",
        ["FolderErrorReadDetails"] = "{0}",
        ["SaveDialogTitle"] = "Сохранить Markdown-файл",
        ["MarkdownDocuments"] = "Markdown-документы",
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
