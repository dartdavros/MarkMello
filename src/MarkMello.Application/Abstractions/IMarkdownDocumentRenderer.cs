using MarkMello.Domain;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Преобразует сырой markdown в стабильную block/inline модель для native viewer.
/// </summary>
public interface IMarkdownDocumentRenderer
{
    /// <summary>
    /// Render markdown with no known base directory. Relative image URLs will
    /// not be resolved; use the overload below when a file location is known.
    /// </summary>
    RenderedMarkdownDocument Render(string markdown);

    /// <summary>
    /// Render markdown with a base directory used to resolve relative image
    /// URLs and any future relative resource references. Default implementation
    /// delegates to the parameterless overload and copies the directory into
    /// the resulting document.
    /// </summary>
    RenderedMarkdownDocument Render(string markdown, string? baseDirectory)
    {
        var rendered = Render(markdown);
        return baseDirectory is null
            ? rendered
            : rendered with { BaseDirectory = baseDirectory };
    }
}
