using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Domain;

namespace MarkMello.Presentation.ViewModels;

/// <summary>Строка уровня в блоке «Оглавление»: сегмент глубины и размер шрифта.</summary>
public sealed partial class OutlineLevelSetting : ObservableObject
{
    private readonly Func<OutlinePreferences> _get;
    private readonly Action<OutlinePreferences> _apply;

    public OutlineLevelSetting(int level, Func<OutlinePreferences> get, Action<OutlinePreferences> apply)
    {
        Level = level;
        _get = get;
        _apply = apply;
    }

    public int Level { get; }

    public string LevelLabel => $"H{Level}";

    public int FontSize => _get().GetFontSize(Level);

    public string FontSizeLabel => $"{FontSize} px";

    public bool IsWithinMaxLevel => Level <= _get().MaxLevel;

    public bool IsMaxLevel
    {
        get => _get().MaxLevel == Level;
        set
        {
            if (value)
            {
                _apply(_get() with { MaxLevel = Level });
            }
            else
            {
                // Клик по уже выбранному сегменту снимает галочку у ToggleButton: вернуть её.
                OnPropertyChanged(nameof(IsMaxLevel));
            }
        }
    }

    [RelayCommand]
    private void IncreaseFontSize() => _apply(_get().WithFontSize(Level, FontSize + 1));

    [RelayCommand]
    private void DecreaseFontSize() => _apply(_get().WithFontSize(Level, FontSize - 1));

    public void Refresh()
    {
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(FontSizeLabel));
        OnPropertyChanged(nameof(IsWithinMaxLevel));
        OnPropertyChanged(nameof(IsMaxLevel));
    }
}
