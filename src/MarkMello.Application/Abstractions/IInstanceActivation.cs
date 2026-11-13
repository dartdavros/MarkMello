namespace MarkMello.Application.Abstractions;

/// <summary>
/// Сигналы от повторных запусков приложения. На Windows и Linux каждый двойной клик
/// по файлу стартует новый процесс: он передаёт свои аргументы уже запущенному
/// и завершается, а запущенный открывает документы во вкладках своего окна.
/// </summary>
public interface IInstanceActivation
{
    /// <summary>
    /// Срабатывает на фоновом потоке: подписчик сам переходит на UI-поток.
    /// Сигналы, пришедшие до первой подписки (окно ещё не показано), не теряются:
    /// они доставляются первому подписчику сразу при подписке.
    /// </summary>
    event EventHandler<InstanceActivatedEventArgs>? Activated;
}

/// <summary>
/// Аргументы повторного запуска, уже приведённые к абсолютным путям.
/// </summary>
public sealed class InstanceActivatedEventArgs : EventArgs
{
    public InstanceActivatedEventArgs(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Paths = paths;
    }

    /// <summary>
    /// Файлы и каталоги из аргументов. Пустой список — запуск без файла:
    /// достаточно вывести окно вперёд.
    /// </summary>
    public IReadOnlyList<string> Paths { get; }
}
