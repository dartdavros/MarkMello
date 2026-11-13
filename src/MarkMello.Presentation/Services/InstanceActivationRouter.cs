using Avalonia.Threading;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Presentation.Services;

/// <summary>
/// Единая точка для «открой это в уже запущенном приложении»: повторный запуск
/// (Windows, Linux) и Apple Event (macOS). Документы уходят вкладками в окно,
/// с которым пользователь работал последним, и это окно выводится вперёд.
///
/// Раньше на сигнал подписывался каждый shell, и со вторым окном файл
/// открывался в обоих сразу.
/// </summary>
public sealed class InstanceActivationRouter : IDisposable
{
    private readonly ICommandLineActivation _commandLine;
    private readonly IInstanceActivation _instances;
    private readonly IWindowLauncher _windows;
    private readonly Func<string, bool> _directoryExists;
    private bool _isListening;

    /// <summary>
    /// Хвост очереди активаций. Активации идут строго по одной: иначе два одновременных
    /// запуска с папками оба увидели бы окно без папки и подменили бы её друг другу.
    /// </summary>
    private Task _queueTail = Task.CompletedTask;

    public InstanceActivationRouter(
        ICommandLineActivation commandLine,
        IInstanceActivation instances,
        IWindowLauncher windows,
        Func<string, bool>? directoryExists = null)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(windows);

        _commandLine = commandLine;
        _instances = instances;
        _windows = windows;
        _directoryExists = directoryExists ?? Directory.Exists;

        _commandLine.FileActivated += OnFileActivated;
    }

    /// <summary>
    /// Включает приём повторных запусков. Вызывается после показа первого окна:
    /// до этого открывать файлы некуда, и канал держит их в очереди.
    /// </summary>
    public void StartListening()
    {
        if (_isListening)
        {
            return;
        }

        _isListening = true;
        _instances.Activated += OnInstanceActivated;
    }

    /// <summary>
    /// Открывает пути в окне-получателе: каталог — как папку, файл — вкладкой.
    /// Пустой список только выводит окно вперёд.
    /// </summary>
    public async Task RouteAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var previous = _queueTail;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queueTail = done.Task;
        try
        {
            await previous.ConfigureAwait(true);
            await RouteCoreAsync(paths).ConfigureAwait(true);
        }
        finally
        {
            done.SetResult();
        }
    }

    private async Task RouteCoreAsync(IReadOnlyList<string> paths)
    {
        if (_windows.BringActivationTargetToFront() is not { } target)
        {
            return;
        }

        foreach (var path in paths)
        {
            if (_directoryExists(path))
            {
                // Папка подчиняется тем же правилам, что и «Открыть папку»: занятое окно
                // отдаёт её новому окну или окну, где она уже открыта (ADR-0007 Rule 11).
                // Открытие дожидаемся — следующий файл из неё должен найти её окно.
                await target.OpenFolderPathAsync(path).ConfigureAwait(true);
            }
            else if (SupportedDocumentTypes.IsSupportedPath(path))
            {
                // Файл из папки, открытой в другом окне («B B/file.md»), идёт в окно этой папки.
                var shell = _windows.FocusWindowForDocument(path) ?? target;
                await shell.OpenOrActivatePathAsync(path).ConfigureAwait(true);
            }
        }
    }

    public void Dispose()
    {
        _commandLine.FileActivated -= OnFileActivated;
        _instances.Activated -= OnInstanceActivated;
    }

    /// <summary>Apple Event приходит уже на UI-потоке.</summary>
    private async void OnFileActivated(object? sender, FileActivationEventArgs e)
        => await RouteQuietlyAsync([e.Path]).ConfigureAwait(true);

    /// <summary>Канал повторных запусков поднимает событие на фоновом потоке.</summary>
    private void OnInstanceActivated(object? sender, InstanceActivatedEventArgs e)
        => Dispatcher.UIThread.Post(async () => await RouteQuietlyAsync(e.Paths).ConfigureAwait(true));

    private async Task RouteQuietlyAsync(IReadOnlyList<string> paths)
    {
        try
        {
            await RouteAsync(paths).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Ошибки открытия показывает сам shell через свои состояния;
            // обработчик события не должен ронять цикл диспетчера.
        }
    }
}
