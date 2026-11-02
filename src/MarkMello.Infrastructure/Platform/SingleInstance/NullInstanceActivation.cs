using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Platform.SingleInstance;

/// <summary>
/// Источник без сигналов: macOS, режим замеров и запуск, который не стал первым экземпляром.
/// </summary>
public sealed class NullInstanceActivation : IInstanceActivation
{
    public static NullInstanceActivation Instance { get; } = new();

    private NullInstanceActivation()
    {
    }

    public event EventHandler<InstanceActivatedEventArgs>? Activated
    {
        add { }
        remove { }
    }
}
