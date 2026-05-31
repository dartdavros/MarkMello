namespace MarkMello.Application.UseCases;

public abstract record ExportPdfResult
{
    private ExportPdfResult() { }

    public sealed record Success(string Path) : ExportPdfResult;
    public sealed record InvalidPath(string Path) : ExportPdfResult;
    public sealed record AccessDenied(string Path) : ExportPdfResult;
    public sealed record WriteError(string Path, string Message) : ExportPdfResult;
}
