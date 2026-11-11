namespace MarkMello.Presentation.Localization;

public sealed partial class LocalizationService
{
    static LocalizationService()
    {
        AddWelcomeStrings();
        AddAdvancedReadingStrings();
        AddOutlineStrings();

        foreach (var entry in EnglishUpdates)
        {
            English[entry.Key] = entry.Value;
        }

        foreach (var entry in RussianUpdates)
        {
            Russian[entry.Key] = entry.Value;
        }
    }

    private static readonly Dictionary<string, string> EnglishUpdates = new(StringComparer.Ordinal)
    {
        ["UpdatesLabel"] = "Updates",
        ["UpdatesHint"] = "GitHub release checks",
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
        ["UpdateDefaultMessage"] = "GitHub Releases are checked 30 seconds after launch or manually.",
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
        ["UpdateAction"] = "Update",
    };

    private static readonly Dictionary<string, string> RussianUpdates = new(StringComparer.Ordinal)
    {
        ["UpdatesLabel"] = "Обновления",
        ["UpdatesHint"] = "Проверка GitHub Releases",
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
        ["UpdateDefaultMessage"] = "Проверка GitHub Releases выполняется через 30 секунд после запуска или вручную.",
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
        ["UpdateAction"] = "Обновить",
    };
}
