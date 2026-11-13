using CommunityToolkit.Mvvm.Input;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

public partial class ShellViewModel
{
    private IReadOnlyList<OutlineLevelSetting>? _outlineLevelSettings;

    public IReadOnlyList<OutlineLevelSetting> OutlineLevelSettings => _outlineLevelSettings ??=
        Enumerable.Range(OutlinePreferences.MinLevel, OutlinePreferences.MaxLevelLimit - OutlinePreferences.MinLevel + 1)
            .Select(level => new OutlineLevelSetting(level, () => OutlinePreferences, ApplyOutlinePreferences))
            .ToArray();

    public string OutlineHeader => _localization["OutlineHeader"];
    public string OutlineEmpty => _localization["OutlineEmpty"];
    public string OutlineSettingsHeader => _localization["OutlineSettingsHeader"];
    public string OutlineSideLabel => _localization["OutlineSideLabel"];
    public string OutlineSideHint => _localization["OutlineSideHint"];
    public string OutlineSideLeft => _localization["OutlineSideLeft"];
    public string OutlineSideRight => _localization["OutlineSideRight"];
    public string OutlineMaxLevelLabel => _localization["OutlineMaxLevelLabel"];
    public string OutlineMaxLevelHint => _localization["OutlineMaxLevelHint"];
    public string OutlineLineHeightLabel => _localization["OutlineLineHeightLabel"];
    public string OutlineLineHeightHint => _localization["OutlineLineHeightHint"];
    public string OutlineLevelSizesLabel => _localization["OutlineLevelSizesLabel"];
    public string OutlineLevelSizesHint => _localization["OutlineLevelSizesHint"];

    public string OutlineLineHeightValueLabel => OutlinePreferences.LineHeight.ToString("0.0", _localization.Culture);

    public bool IsOutlineLeftSelected
    {
        get => OutlinePreferences.Side == OutlinePanelSide.Left;
        set
        {
            if (value)
            {
                ApplyOutlinePreferences(OutlinePreferences with { Side = OutlinePanelSide.Left });
            }
            else
            {
                // Клик по уже выбранному сегменту снимает галочку у ToggleButton: вернуть её.
                OnPropertyChanged(nameof(IsOutlineLeftSelected));
            }
        }
    }

    public bool IsOutlineRightSelected
    {
        get => OutlinePreferences.Side == OutlinePanelSide.Right;
        set
        {
            if (value)
            {
                ApplyOutlinePreferences(OutlinePreferences with { Side = OutlinePanelSide.Right });
            }
            else
            {
                // Клик по уже выбранному сегменту снимает галочку у ToggleButton: вернуть её.
                OnPropertyChanged(nameof(IsOutlineRightSelected));
            }
        }
    }

    [RelayCommand]
    private void IncreaseOutlineLineHeight()
        => ApplyOutlinePreferences(OutlinePreferences with
        {
            LineHeight = OutlinePreferences.LineHeight + OutlinePreferences.LineHeightStep
        });

    [RelayCommand]
    private void DecreaseOutlineLineHeight()
        => ApplyOutlinePreferences(OutlinePreferences with
        {
            LineHeight = OutlinePreferences.LineHeight - OutlinePreferences.LineHeightStep
        });

    private void RefreshOutlineSettingsProperties()
    {
        OnPropertyChanged(nameof(OutlineHeader));
        OnPropertyChanged(nameof(OutlineEmpty));
        OnPropertyChanged(nameof(OutlineToggleTooltip));
        OnPropertyChanged(nameof(OutlineSettingsHeader));
        OnPropertyChanged(nameof(OutlineSideLabel));
        OnPropertyChanged(nameof(OutlineSideHint));
        OnPropertyChanged(nameof(OutlineSideLeft));
        OnPropertyChanged(nameof(OutlineSideRight));
        OnPropertyChanged(nameof(OutlineMaxLevelLabel));
        OnPropertyChanged(nameof(OutlineMaxLevelHint));
        OnPropertyChanged(nameof(OutlineLineHeightLabel));
        OnPropertyChanged(nameof(OutlineLineHeightHint));
        OnPropertyChanged(nameof(OutlineLevelSizesLabel));
        OnPropertyChanged(nameof(OutlineLevelSizesHint));
        OnPropertyChanged(nameof(OutlineLineHeightValueLabel));
        OnPropertyChanged(nameof(IsOutlineLeftSelected));
        OnPropertyChanged(nameof(IsOutlineRightSelected));
        if (_outlineLevelSettings is not null)
        {
            foreach (var setting in _outlineLevelSettings)
            {
                setting.Refresh();
            }
        }
    }
}
