namespace MarkMello.Application.Abstractions;

/// <summary>
/// Контракт интеграции с платформой ОС (file associations, command-line activation, system theme и т.п.).
/// В M0 содержит только идентификатор платформы. Наполняется в M2 (file-first open path).
/// </summary>
public interface IPlatformServices
{
    /// <summary>Имя текущей платформы: Windows / macOS / Linux / Unknown.</summary>
    string PlatformName { get; }
}
