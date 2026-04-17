namespace MarkMello.Domain.Diagnostics;

/// <summary>
/// Снимок измеренных таймингов запуска. Каждый <see cref="StartupStage"/>
/// сопоставляется с временем, прошедшим от начала измерения до его отметки.
/// </summary>
public sealed record StartupSnapshot(IReadOnlyDictionary<StartupStage, TimeSpan> StageTimings);
