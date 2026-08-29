using MarkMello.Domain.Workspace;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Слежение за открытой папкой. Создаётся вместе с folder session и уничтожается
/// вместе с ней: в startup path watcher не участвует (ADR-0007 Rule 9).
///
/// События приходят из потока файловой системы — маршалингом в UI занимается подписчик.
/// </summary>
public interface IWorkspaceWatcher : IDisposable
{
    /// <summary>Пачка изменений после дебаунса: подряд идущие правки схлопнуты.</summary>
    event EventHandler<IReadOnlyList<WorkspaceChange>>? Changed;

    /// <summary>Начать следить за папкой. Повторный вызов переключает наблюдение на новый корень.</summary>
    void Start(string rootPath);

    void StopWatching();
}
