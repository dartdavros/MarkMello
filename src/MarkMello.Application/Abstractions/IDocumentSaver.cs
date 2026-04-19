namespace MarkMello.Application.Abstractions;

/// <summary>
/// Сохраняет markdown-документ на диск. Реализация — в Infrastructure.
/// </summary>
public interface IDocumentSaver
{
    /// <summary>
    /// Записывает содержимое документа по указанному пути.
    /// </summary>
    /// <exception cref="ArgumentException">Путь пустой или некорректный.</exception>
    /// <exception cref="UnauthorizedAccessException">Нет доступа к директории или файлу.</exception>
    /// <exception cref="IOException">Ошибка записи.</exception>
    Task SaveAsync(string path, string content, CancellationToken cancellationToken = default);
}
