using MarkMello.Domain.Workspace;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Слежение за отдельными открытыми файлами, которые не лежат в открытой папке:
/// открытыми двойным кликом, через «Открыть файл» или перетаскиванием. Файлы
/// внутри папки видит <see cref="IWorkspaceWatcher"/>, этот watcher их не трогает.
///
/// События приходят из потока файловой системы — маршалингом в UI занимается подписчик.
/// </summary>
public interface IDocumentWatcher : IDisposable
{
    /// <summary>Пачка изменений после дебаунса: подряд идущие правки схлопнуты.</summary>
    event EventHandler<IReadOnlyList<WorkspaceChange>>? Changed;

    /// <summary>
    /// Заменяет набор отслеживаемых файлов. Пустой набор снимает наблюдение целиком.
    /// </summary>
    void Watch(IReadOnlyCollection<string> filePaths);
}
