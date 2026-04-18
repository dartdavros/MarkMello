using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Изолирует markdown pipeline от presentation layer и гарантирует безопасный fallback.
/// Parse/render ошибки не должны ломать viewer path.
/// </summary>
public sealed class RenderMarkdownDocumentUseCase
{
    private readonly IMarkdownDocumentRenderer _renderer;

    public RenderMarkdownDocumentUseCase(IMarkdownDocumentRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    public RenderedMarkdownDocument Execute(string markdown)
        => Execute(markdown, baseDirectory: null);

    public RenderedMarkdownDocument Execute(string markdown, string? baseDirectory)
    {
        try
        {
            return _renderer.Render(markdown, baseDirectory);
        }
        catch
        {
            var fallback = RenderedMarkdownDocument.PlainText(markdown);
            return baseDirectory is null ? fallback : fallback with { BaseDirectory = baseDirectory };
        }
    }
}
