using MarkMello.Domain.Diagnostics;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Сервис измерения таймингов запуска. Вызовы <see cref="Mark"/>
/// фиксируют момент достижения соответствующего <see cref="StartupStage"/>.
/// Реализация должна быть thread-safe и максимально дешёвой по аллокациям.
/// </summary>
public interface IStartupMetrics
{
    /// <summary>Зафиксировать достижение этапа.</summary>
    void Mark(StartupStage stage);

    /// <summary>Снимок всех зафиксированных таймингов.</summary>
    StartupSnapshot Snapshot();
}
