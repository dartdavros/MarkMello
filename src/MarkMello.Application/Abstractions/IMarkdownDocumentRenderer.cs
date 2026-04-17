using MarkMello.Domain;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Преобразует сырой markdown в стабильную block/inline модель для native viewer.
/// </summary>
public interface IMarkdownDocumentRenderer
{
    RenderedMarkdownDocument Render(string markdown);
}
