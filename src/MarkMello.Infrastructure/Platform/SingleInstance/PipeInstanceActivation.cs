using System.IO.Pipes;
using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Platform.SingleInstance;

/// <summary>
/// Серверная сторона канала в первом экземпляре: принимает по одному сообщению
/// за соединение и поднимает <see cref="Activated"/>. Канал доступен только
/// процессам того же пользователя (<see cref="PipeOptions.CurrentUserOnly"/>).
///
/// Слушать начинает сразу, ещё до старта Avalonia: при выделении нескольких файлов
/// в проводнике все процессы стартуют одновременно, и вторичные не должны ждать,
/// пока первый покажет окно. Сообщения до появления подписчика копятся в очереди.
/// </summary>
internal sealed class PipeInstanceActivation : IInstanceActivation, IDisposable
{
    private const int MaxMessageBytes = 1024 * 1024;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly string _name;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private readonly Queue<InstanceActivatedEventArgs> _pending = new();
    private EventHandler<InstanceActivatedEventArgs>? _activated;

    public PipeInstanceActivation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    public event EventHandler<InstanceActivatedEventArgs>? Activated
    {
        add
        {
            InstanceActivatedEventArgs[] pending;
            lock (_gate)
            {
                _activated += value;
                pending = [.. _pending];
                _pending.Clear();
            }

            foreach (var args in pending)
            {
                value?.Invoke(this, args);
            }
        }
        remove
        {
            lock (_gate)
            {
                _activated -= value;
            }
        }
    }

    /// <summary>Сколько сигналов ждут подписчика. Для тестов.</summary>
    internal int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    public void StartListening() => _ = Task.Run(() => ListenAsync(_stop.Token));

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _name,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                var paths = await ReadMessageAsync(server, cancellationToken).ConfigureAwait(false);
                if (paths is not null)
                {
                    Publish(new InstanceActivatedEventArgs(paths));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
                // Клиент оборвал соединение или не уложился в таймаут — ждём следующего.
                // Пауза не даёт циклу крутиться вхолостую, если ошибка повторяется.
                await DelayQuietlyAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                // Имя занято процессом другого пользователя: канал недоступен,
                // приложение продолжает работать как отдельный экземпляр.
                return;
            }
        }
    }

    private void Publish(InstanceActivatedEventArgs args)
    {
        EventHandler<InstanceActivatedEventArgs>? handler;
        lock (_gate)
        {
            handler = _activated;
            if (handler is null)
            {
                _pending.Enqueue(args);
                return;
            }
        }

        handler(this, args);
    }

    private static async Task<IReadOnlyList<string>?> ReadMessageAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        // Клиент того же пользователя мог зависнуть: чтение ограничено и по времени,
        // и по размеру, чтобы следующий запуск не остался без ответа.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);

        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await pipe.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxMessageBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return SingleInstanceChannel.TryRead(buffer);
    }

    private static async Task DelayQuietlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Остановка во время паузы — цикл завершится на проверке токена.
        }
    }
}
