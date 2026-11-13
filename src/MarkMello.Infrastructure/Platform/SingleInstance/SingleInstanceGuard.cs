using System.IO.Pipes;
using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Platform.SingleInstance;

/// <summary>
/// Один экземпляр приложения на пользователя: файлы из повторных запусков
/// открываются вкладками в уже запущенном окне, а не в новом.
///
/// Первый экземпляр владеет именованным mutex и слушает канал. Повторный запуск
/// mutex не получает, пересылает аргументы по каналу и завершается. Если первый
/// экземпляр не отвечает (завис или уже закрывается), повторный запускается
/// самостоятельно: файл не должен потеряться.
///
/// На macOS не используется: Finder сам отправляет файл запущенному приложению
/// Apple Event'ом.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly string? _name;
    private readonly Mutex? _mutex;
    private PipeInstanceActivation? _activation;
    private bool _ownsMutex;

    private SingleInstanceGuard(string? name, Mutex? mutex, bool ownsMutex)
    {
        _name = name;
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    /// <summary>
    /// <paramref name="enabled"/> = false оставляет поведение «каждый запуск — своё окно»:
    /// так работают замеры старта, которым нужен честный холодный запуск.
    /// </summary>
    public static SingleInstanceGuard Acquire(bool enabled)
    {
        if (!enabled || OperatingSystem.IsMacOS())
        {
            return new SingleInstanceGuard(null, null, ownsMutex: false);
        }

        var name = SingleInstanceChannel.GetName();
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        return new SingleInstanceGuard(name, mutex, createdNew);
    }

    /// <summary>
    /// Пересылает аргументы первому экземпляру. true — пересылка удалась,
    /// и этот процесс должен завершиться.
    /// </summary>
    public bool TryForwardToPrimary(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (_name is null || _ownsMutex)
        {
            return false;
        }

        if (TrySend(_name, SingleInstanceChannel.ResolvePaths(args)))
        {
            return true;
        }

        // Первый экземпляр не ответил. Если он успел завершиться, этот процесс
        // становится первым — иначе следующие запуски снова ждали бы таймаут.
        _ownsMutex = TryTakeOwnership(_mutex!);
        return false;
    }

    /// <summary>
    /// Источник сигналов для DI: у первого экземпляра — канал, у остальных — пустышка.
    /// </summary>
    public IInstanceActivation CreateActivation()
    {
        if (_name is null || !_ownsMutex)
        {
            return NullInstanceActivation.Instance;
        }

        if (_activation is null)
        {
            _activation = new PipeInstanceActivation(_name);
            _activation.StartListening();
        }

        return _activation;
    }

    public void Dispose()
    {
        _activation?.Dispose();

        if (_mutex is null)
        {
            return;
        }

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    internal static bool TrySend(string name, IReadOnlyList<string> paths)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(ConnectTimeout);

            if (OperatingSystem.IsWindows())
            {
                WindowsForegroundRights.GrantTo(pipe);
            }

            SingleInstanceChannel.Write(pipe, paths);
            return true;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryTakeOwnership(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // Прежний владелец завершился, не освободив mutex: владение всё равно перешло к нам.
            return true;
        }
    }
}
