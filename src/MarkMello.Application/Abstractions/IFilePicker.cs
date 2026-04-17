namespace MarkMello.Application.Abstractions;

/// <summary>
/// Системный файловый picker. Реализация — в Presentation, потому что зависит от Avalonia
/// (TopLevel.StorageProvider). VM зовёт через интерфейс — без знания про UI framework.
/// </summary>
public interface IFilePicker
{
    /// <summary>Открыть picker для выбора одного markdown-файла. Возвращает null, если отменено.</summary>
    Task<string?> PickMarkdownFileAsync(CancellationToken cancellationToken = default);
}
