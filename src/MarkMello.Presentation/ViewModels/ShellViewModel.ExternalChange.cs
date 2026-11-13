using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Плашка «файл изменён на диске» для вкладки с несохранёнными правками:
/// перечитать файл или оставить свои правки.
/// </summary>
public partial class ShellViewModel
{
    /// <summary>Активная вкладка изменилась на диске, а в ней есть несохранённые правки.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExternalChangeContent))]
    private bool _showsExternalChangeBanner;

    /// <summary>Плашка внешнего изменения появляется по событию, а не с запуска приложения.</summary>
    public object? ExternalChangeContent => ShowsExternalChangeBanner ? this : null;

    public string ExternalChangeTitle => _localization["ExternalChangeTitle"];

    public string ExternalChangeReload => _localization["ExternalChangeReload"];

    public string ExternalChangeKeep => _localization["ExternalChangeKeep"];

    private void MarkExternalChange(DocumentTabViewModel tab)
    {
        tab.HasExternalChange = true;

        if (ReferenceEquals(tab, OpenDocuments.ActiveTab))
        {
            ShowsExternalChangeBanner = true;
        }
    }

    [RelayCommand]
    private async Task ReloadExternalChangeAsync()
    {
        if (OpenDocuments.ActiveTab is not { Path: { } path } tab)
        {
            return;
        }

        tab.HasExternalChange = false;
        ShowsExternalChangeBanner = false;

        var offset = tab.ScrollOffset;
        tab.EditorSession?.Dispose();
        tab.EditorSession = null;
        tab.IsEditMode = false;
        EditorSession = null;
        IsEditMode = false;

        await LoadDocumentAsync(path, preserveEditModeAfterLoad: false).ConfigureAwait(true);
        _pendingScrollOffset = offset;
    }

    /// <summary>«Оставить мои правки»: плашка уходит, файл на диске трогать не надо.</summary>
    [RelayCommand]
    private void KeepExternalChange()
    {
        if (OpenDocuments.ActiveTab is { } tab)
        {
            tab.HasExternalChange = false;
        }

        ShowsExternalChangeBanner = false;
    }

    private void SyncExternalChangeBanner()
        => ShowsExternalChangeBanner = OpenDocuments.ActiveTab?.HasExternalChange == true;
}
