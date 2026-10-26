using MarkMello.Domain;
using MarkMello.Domain.Workspace;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Хранилище пользовательских настроек. В M4 реализуется как маленький JSON-файл
/// в платформенном config-каталоге с безопасным fallback на defaults.
/// </summary>
public interface ISettingsStore
{
    ValueTask<ReadingPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default);
    ValueTask SavePreferencesAsync(ReadingPreferences preferences, CancellationToken cancellationToken = default);

    ValueTask<ThemeMode> LoadThemeAsync(CancellationToken cancellationToken = default);
    ValueTask SaveThemeAsync(ThemeMode theme, CancellationToken cancellationToken = default);

    ValueTask<WindowBorderMode> LoadWindowBorderModeAsync(CancellationToken cancellationToken = default);
    ValueTask SaveWindowBorderModeAsync(WindowBorderMode mode, CancellationToken cancellationToken = default);

    ValueTask<AppLanguage> LoadLanguageAsync(CancellationToken cancellationToken = default);
    ValueTask SaveLanguageAsync(AppLanguage language, CancellationToken cancellationToken = default);

    ValueTask<bool> LoadAlwaysOpenDocumentsInEditModeAsync(CancellationToken cancellationToken = default);
    ValueTask SaveAlwaysOpenDocumentsInEditModeAsync(bool value, CancellationToken cancellationToken = default);

    ValueTask<double> LoadSidebarWidthAsync(CancellationToken cancellationToken = default);
    ValueTask SaveSidebarWidthAsync(double width, CancellationToken cancellationToken = default);

    /// <summary>
    /// Состояние последней сессии окна. Читается только по явному запросу пользователя:
    /// в стартовый путь восстановление не входит.
    /// </summary>
    ValueTask<WorkspaceSessionState> LoadSessionAsync(CancellationToken cancellationToken = default);
    ValueTask SaveSessionAsync(WorkspaceSessionState session, CancellationToken cancellationToken = default);

    ValueTask<WindowPlacement?> LoadWindowPlacementAsync(CancellationToken cancellationToken = default);
    ValueTask SaveWindowPlacementAsync(WindowPlacement? placement, CancellationToken cancellationToken = default);
}
