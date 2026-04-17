namespace MarkMello.Domain.Diagnostics;

/// <summary>
/// Этапы инициализации приложения, описанные в architecture.md (Progressive initialization).
/// Используются для измерения производительности fast path.
/// </summary>
public enum StartupStage
{
    /// <summary>Stage 1: запуск приложения, разбор аргументов, минимальный runtime.</summary>
    AppBootstrap = 1,

    /// <summary>Stage 2: главное окно создано и показано.</summary>
    FirstWindow = 2,

    /// <summary>Stage 3: документ прочитан, преобразован и отображён.</summary>
    ReadableDocument = 3,

    /// <summary>Stage 4: вторичные controls и команды активны.</summary>
    SecondaryFeatures = 4,

    /// <summary>Stage 5: editor subsystem загружен (по явному действию пользователя).</summary>
    EditorActivation = 5
}
