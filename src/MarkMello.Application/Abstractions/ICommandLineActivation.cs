namespace MarkMello.Application.Abstractions;

/// <summary>
/// Определяет, был ли запуск приложения активирован с путём к файлу
/// (через ассоциацию, command-line аргумент или платформенный
/// «открыть файл» сигнал — macOS Apple Event и т.п.).
/// </summary>
public interface ICommandLineActivation
{
    /// <summary>
    /// Возвращает абсолютный путь к файлу-активатору, либо null если запуск был «пустой».
    /// Файл должен реально существовать и иметь известное расширение (.md/.markdown/.txt).
    /// Если до момента вызова поступил runtime-сигнал (macOS AppleEvent на cold-start
    /// приходит после старта Avalonia, но до того как view-model дойдёт сюда),
    /// он возвращается как стартовый путь.
    /// </summary>
    string? GetActivationFilePath();

    /// <summary>
    /// Возвращает абсолютный путь к каталогу-активатору, либо null. Каталог в аргументах
    /// открывается как папка — тот же сценарий, что «Открыть папку», только без picker'а
    /// (ADR-0007 Rule 2).
    /// </summary>
    string? GetActivationFolderPath();

    /// <summary>
    /// Срабатывает, когда ОС просит уже запущенное приложение открыть файл
    /// (на macOS — повторный AppleEvent после первого получения; на Windows
    /// и Linux обычно не вызывается, потому что файл приходит через argv).
    /// Подписчики должны выполнять обработку на UI-потоке.
    /// </summary>
    event EventHandler<FileActivationEventArgs>? FileActivated;
}

/// <summary>
/// Данные runtime-события «открыть файл», поступающие от ОС после старта.
/// </summary>
public sealed class FileActivationEventArgs : EventArgs
{
    public FileActivationEventArgs(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    /// <summary>Абсолютный путь к запрошенному документу.</summary>
    public string Path { get; }
}

/// <summary>
/// Внутренний контракт публикации runtime-сигнала «открыть файл». Реализуется
/// тем же типом, что и <see cref="ICommandLineActivation"/>, но изолируется
/// в отдельном интерфейсе, чтобы платформенный мост (App.axaml.cs) не получал
/// возможность маскировать активационные ошибки через консьюмерский контракт.
/// </summary>
public interface IFileActivationPublisher
{
    /// <summary>
    /// Сообщает о платформенном сигнале «открой этот файл». Если файл не
    /// существует или его расширение не поддержано, реализация обязана
    /// тихо проигнорировать сигнал — это не пользовательская ошибка.
    /// </summary>
    void NotifyFileActivated(string path);
}
