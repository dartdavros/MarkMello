using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Application.UseCases;

public sealed class ExportPdfUseCase
{
    private readonly IPdfExporter _exporter;

    public ExportPdfUseCase(IPdfExporter exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        _exporter = exporter;
    }

    public async Task<ExportPdfResult> ExecuteAsync(
        string path,
        string title,
        RenderedMarkdownDocument document,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null)
        {
            return new ExportPdfResult.InvalidPath(path ?? string.Empty);
        }

        try
        {
            await _exporter
                .ExportAsync(new PdfExportRequest(normalizedPath, title, document), cancellationToken)
                .ConfigureAwait(false);

            return new ExportPdfResult.Success(normalizedPath);
        }
        catch (UnauthorizedAccessException)
        {
            return new ExportPdfResult.AccessDenied(normalizedPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            return new ExportPdfResult.WriteError(normalizedPath, ex.Message);
        }
        catch (IOException ex)
        {
            return new ExportPdfResult.WriteError(normalizedPath, ex.Message);
        }
        catch (ArgumentException)
        {
            return new ExportPdfResult.InvalidPath(path ?? string.Empty);
        }
        catch (NotSupportedException)
        {
            return new ExportPdfResult.InvalidPath(path ?? string.Empty);
        }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var normalized = Path.GetFullPath(path.Trim());
            var extension = Path.GetExtension(normalized);
            if (string.IsNullOrWhiteSpace(extension))
            {
                normalized += ".pdf";
                extension = ".pdf";
            }

            return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : null;
        }
        catch
        {
            return null;
        }
    }
}
