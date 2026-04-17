using MarkMello.Domain;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Загружает markdown-документ с диска. Реализация — в Infrastructure.
/// </summary>
public interface IDocumentLoader
{
    /// <summary>
    /// Читает файл по указанному пути и возвращает его как <see cref="MarkdownSource"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">Файл не существует.</exception>
    /// <exception cref="UnauthorizedAccessException">Нет доступа к файлу.</exception>
    /// <exception cref="IOException">Ошибка чтения.</exception>
    Task<MarkdownSource> LoadAsync(string path, CancellationToken cancellationToken = default);
}
