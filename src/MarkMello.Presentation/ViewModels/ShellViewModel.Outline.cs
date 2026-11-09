using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Панель оглавления (ADR-0008). Пока панель выключена, <see cref="Outline"/> не создаётся
/// и документ не обходится (Rule 1).
/// </summary>
public partial class ShellViewModel
{
    private OutlineViewModel? _outline;
    private EditorSessionViewModel? _outlineObservedEditor;

    [ObservableProperty]
    private OutlinePreferences _outlinePreferences = OutlinePreferences.Default;

    public OutlineViewModel? Outline => ShowsOutline ? _outline ??= new OutlineViewModel() : null;

    public bool ShowsOutline => OutlinePreferences.IsVisible && IsViewer;

    public object? OutlineLeftContent => ShowsOutline && OutlinePreferences.Side == OutlinePanelSide.Left ? Outline : null;

    public object? OutlineRightContent => ShowsOutline && OutlinePreferences.Side == OutlinePanelSide.Right ? Outline : null;

    public bool HidesMinimapForOutline => ShowsOutline && OutlinePreferences.Side == OutlinePanelSide.Right;

    /// <summary>Кнопка живёт в плавающей группе; в edit mode группа скрыта автором, там остаётся горячая клавиша.</summary>
    public bool ShowsFloatingOutlineButton => IsViewer && !IsEditMode;

    /// <summary>Плавающая группа кнопок видна, если нужна хотя бы одна из её кнопок.</summary>
    public bool ShowsFloatingChromeGroup => ShowsFloatingAppMenuButton || ShowsFloatingOutlineButton;

    public bool IsOutlineToggleChecked => OutlinePreferences.IsVisible;

    public string OutlineToggleTooltip => _localization["OutlineToggleTooltip"];

    [RelayCommand]
    private void ToggleOutline()
        => ApplyOutlinePreferences(OutlinePreferences with { IsVisible = !OutlinePreferences.IsVisible });

    public void SetOutlineWidth(double width)
        => ApplyOutlinePreferences(OutlinePreferences with { Width = OutlinePreferences.NormalizeWidth(width) });

    internal async Task LoadOutlinePreferencesAsync()
        => OutlinePreferences = OutlinePreferences.Normalize(
            await _settings.LoadOutlinePreferencesAsync().ConfigureAwait(true));

    private void InitializeOutline() => PropertyChanged += OnOutlineSourcePropertyChanged;

    // ShowsFloatingChromeGroup складывается из двух свойств, которые поднимаются в разных местах;
    // слушаем их здесь, а не размазываем NotifyPropertyChangedFor по чужим файлам.
    private void RaiseFloatingChromeGroupIfNeeded(string? propertyName)
    {
        if (propertyName is nameof(ShowsFloatingAppMenuButton) or nameof(ShowsFloatingOutlineButton))
        {
            OnPropertyChanged(nameof(ShowsFloatingChromeGroup));
        }
    }

    private void ApplyOutlinePreferences(OutlinePreferences preferences)
    {
        var normalized = OutlinePreferences.Normalize(preferences);
        if (normalized == OutlinePreferences)
        {
            return;
        }

        OutlinePreferences = normalized;
        _ = Task.Run(async () =>
        {
            try
            {
                await _settings.SaveOutlinePreferencesAsync(normalized).ConfigureAwait(false);
            }
            catch
            {
                // Как и настройки чтения: сохранение best-effort, панель работает и без него.
            }
        });
    }

    partial void OnOutlinePreferencesChanged(OutlinePreferences? oldValue, OutlinePreferences newValue)
    {
        // Layout-свойства пересобирают минимапу во ViewerView: поднимаем их только при смене
        // видимости/стороны, а не на каждый шаг размера шрифта или интервала.
        if (oldValue is null || oldValue.IsVisible != newValue.IsVisible || oldValue.Side != newValue.Side)
        {
            RaiseOutlineLayoutProperties();
        }

        RefreshOutlineSettingsProperties();

        if (_outline is null)
        {
            RefreshOutlineSource();
            return;
        }

        // Пока панель скрыта, RefreshOutlineSource ничего не делает, поэтому при повторном показе
        // дерево нужно пересобрать: документ или вкладка могли смениться.
        if (oldValue?.IsVisible != newValue.IsVisible || oldValue?.MaxLevel != newValue.MaxLevel)
        {
            RefreshOutlineSource();
        }
        else
        {
            _outline.ApplyTypography(newValue);
        }
    }

    private void OnOutlineSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseFloatingChromeGroupIfNeeded(e.PropertyName);

        switch (e.PropertyName)
        {
            case nameof(IsViewer):
                RaiseOutlineLayoutProperties();
                RefreshOutlineSource();
                break;
            case nameof(IsEditMode):
            case nameof(EditorSession):
                OnPropertyChanged(nameof(ShowsFloatingOutlineButton));
                ObserveEditorForOutline();
                RefreshOutlineSource();
                break;
            case nameof(RenderedDocument):
                RefreshOutlineSource();
                break;
        }
    }

    private void ObserveEditorForOutline()
    {
        var editor = IsEditMode ? EditorSession : null;
        if (ReferenceEquals(editor, _outlineObservedEditor))
        {
            return;
        }

        if (_outlineObservedEditor is not null)
        {
            _outlineObservedEditor.PropertyChanged -= OnOutlineEditorPropertyChanged;
        }

        _outlineObservedEditor = editor;
        if (editor is not null)
        {
            editor.PropertyChanged += OnOutlineEditorPropertyChanged;
        }
    }

    private void OnOutlineEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorSessionViewModel.RenderedPreview))
        {
            RefreshOutlineSource();
        }
    }

    private void RefreshOutlineSource()
    {
        if (Outline is not { } outline)
        {
            return;
        }

        var document = IsEditMode && EditorSession is not null ? EditorSession.RenderedPreview : RenderedDocument;
        outline.Update(document, OutlinePreferences, OpenDocuments.ActiveTab);
    }

    private void RaiseOutlineLayoutProperties()
    {
        OnPropertyChanged(nameof(ShowsOutline));
        OnPropertyChanged(nameof(ShowsFloatingOutlineButton));
        OnPropertyChanged(nameof(Outline));
        OnPropertyChanged(nameof(OutlineLeftContent));
        OnPropertyChanged(nameof(OutlineRightContent));
        OnPropertyChanged(nameof(HidesMinimapForOutline));
        OnPropertyChanged(nameof(IsOutlineToggleChecked));
    }
}
