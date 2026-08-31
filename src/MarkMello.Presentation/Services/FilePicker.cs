using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;

namespace MarkMello.Presentation.Services;

/// <summary>
/// Реализация <see cref="IFilePicker"/> через Avalonia 12 StorageProvider.
/// TopLevel получаем через accessor, чтобы не привязываться к статическому Application.Current
/// в момент конструирования (DI собирается раньше, чем создаётся MainWindow).
/// </summary>
public sealed class FilePicker : IFilePicker
{
    private readonly ILocalizationService _localization;
    private readonly Func<TopLevel?> _topLevelAccessor;

    public FilePicker(ILocalizationService localization, Func<TopLevel?> topLevelAccessor)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(topLevelAccessor);
        _localization = localization;
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
            Title = _localization["OpenDialogTitle"],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(_localization["MarkdownDocuments"])
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

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = _topLevelAccessor();
        if (topLevel?.StorageProvider is not { CanPickFolder: true } provider)
        {
            return null;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = _localization["OpenFolderDialogTitle"],
            AllowMultiple = false
        };

        var folders = await provider.OpenFolderPickerAsync(options).ConfigureAwait(true);
        if (folders.Count == 0)
        {
            return null;
        }

        return folders[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveMarkdownFileAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var topLevel = _topLevelAccessor();
        if (topLevel?.StorageProvider is not { CanSave: true } provider)
        {
            return null;
        }

        var normalizedSuggestedName = NormalizeSuggestedFileName(suggestedFileName);
        var options = new FilePickerSaveOptions
        {
            Title = _localization["SaveDialogTitle"],
            SuggestedFileName = normalizedSuggestedName,
            DefaultExtension = Path.GetExtension(normalizedSuggestedName),
            FileTypeChoices = new[]
            {
                new FilePickerFileType(_localization["MarkdownDocuments"])
                {
                    Patterns = SupportedDocumentTypes.Extensions
                        .Select(static extension => $"*{extension}")
                        .ToArray()
                }
            }
        };

        var file = await provider.SaveFilePickerAsync(options).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    private string NormalizeSuggestedFileName(string suggestedFileName)
    {
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            return _localization["UntitledFileName"];
        }

        var trimmed = suggestedFileName.Trim();
        return SupportedDocumentTypes.IsSupportedPath(trimmed)
            ? trimmed
            : $"{trimmed}.md";
    }
}
