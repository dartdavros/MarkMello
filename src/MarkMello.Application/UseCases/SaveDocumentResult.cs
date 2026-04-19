using MarkMello.Domain;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Результат попытки сохранить документ. Типизированный union для явной
/// обработки save flow без исключений в presentation-коде.
/// </summary>
public abstract record SaveDocumentResult
{
    private SaveDocumentResult() { }

    public sealed record Success(MarkdownSource Source) : SaveDocumentResult;
    public sealed record InvalidPath(string Path) : SaveDocumentResult;
    public sealed record AccessDenied(string Path) : SaveDocumentResult;
    public sealed record WriteError(string Path, string Message) : SaveDocumentResult;
}
