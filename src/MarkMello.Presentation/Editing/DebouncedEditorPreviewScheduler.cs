using Avalonia.Threading;

namespace MarkMello.Presentation.Editing;

/// <summary>
/// Боевой планировщик preview: debounce на UI-потоке + рендер на пуле потоков.
///
/// Набор символов подряд порождает один parse после паузы, а не по одному на
/// символ; сам parse не блокирует ввод, потому что выполняется вне UI-потока.
/// Устаревшие результаты отбрасываются по номеру поколения.
/// </summary>
public sealed class DebouncedEditorPreviewScheduler : IEditorPreviewScheduler, IDisposable
{
    /// <summary>
    /// Пауза, после которой запускается рендер. Достаточно коротка, чтобы
    /// preview ощущался живым, и достаточно длинна, чтобы беглый набор текста
    /// сворачивался в один проход.
    /// </summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(180);

    private readonly DispatcherTimer _timer;
    private Action? _pending;
    private long _generation;
    private bool _isDisposed;

    public DebouncedEditorPreviewScheduler()
        : this(DefaultDelay)
    {
    }

    public DebouncedEditorPreviewScheduler(TimeSpan delay)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = delay <= TimeSpan.Zero ? DefaultDelay : delay
        };
        _timer.Tick += OnTick;
    }

    public void Schedule<T>(Func<T> render, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(apply);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var generation = ++_generation;
        _pending = () => StartRender(generation, render, apply);

        _timer.Stop();
        _timer.Start();
    }

    public void Cancel()
    {
        // Поднимаем поколение, чтобы уже стартовавший фоновый рендер не
        // перезаписал состояние, выставленное вызывающей стороной.
        _generation++;
        _pending = null;
        _timer.Stop();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Cancel();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        var pending = _pending;
        _pending = null;
        pending?.Invoke();
    }

    private void StartRender<T>(long generation, Func<T> render, Action<T> apply)
    {
        _ = Task.Run(() =>
        {
            T result;
            try
            {
                result = render();
            }
            catch (Exception)
            {
                // Рендер preview — не транзакция: при сбое сохраняем последний
                // валидный документ, а не роняем сессию редактирования.
                return;
            }

            Dispatcher.UIThread.Post(
                () =>
                {
                    if (generation == _generation)
                    {
                        apply(result);
                    }
                },
                DispatcherPriority.Background);
        });
    }
}
