using MarkMello.Domain;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Результат попытки открытия документа. Sealed type union — каждый кейс
/// явно обрабатывается через pattern matching, без exceptions в hot path.
/// </summary>
public abstract record OpenDocumentResult
{
    private OpenDocumentResult() { }

    public sealed record Success(MarkdownSource Source) : OpenDocumentResult;
    public sealed record NotFound(string Path) : OpenDocumentResult;
    public sealed record AccessDenied(string Path) : OpenDocumentResult;
    public sealed record ReadError(string Path, string Message) : OpenDocumentResult;
}
