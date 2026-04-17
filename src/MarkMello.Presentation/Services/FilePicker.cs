using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Presentation.Services;

/// <summary>
/// Реализация <see cref="IFilePicker"/> через Avalonia 12 StorageProvider.
/// TopLevel получаем через accessor, чтобы не привязываться к статическому Application.Current
/// в момент конструирования (DI собирается раньше, чем создаётся MainWindow).
/// </summary>
public sealed class FilePicker : IFilePicker
{
    private readonly Func<TopLevel?> _topLevelAccessor;

    public FilePicker(Func<TopLevel?> topLevelAccessor)
    {
        ArgumentNullException.ThrowIfNull(topLevelAccessor);
        _topLevelAccessor = topLevelAccessor;
    }

    public async Task<string?> PickMarkdownFileAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = _topLevelAccessor();
        if (topLevel?.StorageProvider is not { CanOpen: true } provider)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Open Markdown file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Markdown documents")
                {
                    Patterns = SupportedDocumentTypes.Extensions
                        .Select(static extension => $"*{extension}")
                        .ToArray()
                }
            }
        };

        var files = await provider.OpenFilePickerAsync(options).ConfigureAwait(true);
        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }
}
