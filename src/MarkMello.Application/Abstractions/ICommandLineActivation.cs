namespace MarkMello.Application.Abstractions;

/// <summary>
/// Определяет, был ли запуск приложения активирован с путём к файлу
/// (через ассоциацию или command-line аргумент).
/// </summary>
public interface ICommandLineActivation
{
    /// <summary>
    /// Возвращает абсолютный путь к файлу-активатору, либо null если запуск был «пустой».
    /// Файл должен реально существовать и иметь известное расширение (.md/.markdown/.txt).
    /// </summary>
    string? GetActivationFilePath();
}
