using MarkMello.Domain.Workspace;

namespace MarkMello.Infrastructure.Workspace;

/// <summary>
/// Дебаунс сырых событий <see cref="FileSystemWatcher"/>.
///
/// Сырые события приходят пачками: одно сохранение файла редактором даёт три-четыре
/// уведомления, а распаковка архива — сотни. Поэтому события копятся 250 мс и уходят
/// подписчику одним списком; без этого всё перечитывалось бы на каждый чих.
/// </summary>
internal sealed class ChangeBatcher : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(250);

    private readonly Lock _gate = new();
    private readonly List<WorkspaceChange> _pending = [];
    private readonly Action<IReadOnlyList<WorkspaceChange>> _flush;
    private Timer? _debounce;

    public ChangeBatcher(Action<IReadOnlyList<WorkspaceChange>> flush)
    {
        ArgumentNullException.ThrowIfNull(flush);
        _flush = flush;
    }

    public void Add(WorkspaceChange change)
    {
        lock (_gate)
        {
            _pending.Add(change);
            _debounce ??= new Timer(Flush, state: null, DebounceInterval, Timeout.InfiniteTimeSpan);
            _debounce.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Сбрасывает накопленное без отправки: наблюдение остановлено или переключено.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _pending.Clear();
            _debounce?.Dispose();
            _debounce = null;
        }
    }

    public void Dispose() => Clear();

    private void Flush(object? state)
    {
        List<WorkspaceChange> batch;

        lock (_gate)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            batch = [.. _pending];
            _pending.Clear();
        }

        _flush(batch);
    }
}
