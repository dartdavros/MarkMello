using System.Collections.Concurrent;
using System.Diagnostics;
using MarkMello.Application.Abstractions;
using MarkMello.Domain.Diagnostics;

namespace MarkMello.Infrastructure.Diagnostics;

/// <summary>
/// Замер таймингов через <see cref="Stopwatch"/>, стартующий в момент создания инстанса.
/// Создавать как можно раньше в <c>Program.Main</c>, до построения DI-контейнера.
/// Дублирует тайминги в stdout, чтобы их было видно даже без подключённого логгера.
/// </summary>
public sealed class StopwatchStartupMetrics : IStartupMetrics
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly ConcurrentDictionary<StartupStage, TimeSpan> _timings = new();

    public void Mark(StartupStage stage)
    {
        var elapsed = _stopwatch.Elapsed;
        _timings[stage] = elapsed;
        Console.WriteLine($"[startup] {stage,-20} {elapsed.TotalMilliseconds,8:F1} ms");
    }

    public StartupSnapshot Snapshot() => new(_timings);
}
