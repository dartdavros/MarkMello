using MarkMello.Domain;

namespace MarkMello.Application.Abstractions;

public interface IPdfExporter
{
    Task ExportAsync(PdfExportRequest request, CancellationToken cancellationToken = default);
}

public sealed record PdfExportRequest(
    string Path,
    string Title,
    RenderedMarkdownDocument Document);
