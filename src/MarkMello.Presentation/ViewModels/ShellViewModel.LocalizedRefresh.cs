namespace MarkMello.Presentation.ViewModels;

public partial class ShellViewModel
{
    private void RefreshLocalizedProperties()
    {
        RefreshAdvancedReadingProperties();
        RefreshInterfaceTypographyProperties();
        RefreshOutlineSettingsProperties();
        _languageOptions = CreateLanguageOptions();

        NotifyLocalizedBindingPropertiesChanged();
        EditorSession?.RefreshLocalizedProperties();

        OnPropertyChanged(nameof(EditToggleLabel));
        OnPropertyChanged(nameof(EditShortcutLabel));
        OnPropertyChanged(nameof(NextThemeHint));
        OnPropertyChanged(nameof(FindResultLabel));
        OnPropertyChanged(nameof(UpdateActionHint));
        OnPropertyChanged(nameof(CheckForUpdatesLabel));
        OnPropertyChanged(nameof(DownloadUpdateLabel));
        OnPropertyChanged(nameof(DownloadedUpdateActionLabel));
        OnPropertyChanged(nameof(UpdateStateBadge));
        OnPropertyChanged(nameof(IsSystemLanguageSelected));
        OnPropertyChanged(nameof(IsEnglishLanguageSelected));
        OnPropertyChanged(nameof(IsRussianLanguageSelected));
        OnPropertyChanged(nameof(LanguageOptions));
        OnPropertyChanged(nameof(SelectedLanguageOption));
        OnPropertyChanged(nameof(WordCountStatusLabel));
        OnPropertyChanged(nameof(ReadTimeStatusLabel));
        OnPropertyChanged(nameof(FontSizeLabel));
        OnPropertyChanged(nameof(LineHeightLabel));

        RefreshDirtyPromptTexts();
        RefreshLoadErrorTexts();
        RefreshUpdateStatusTexts();
    }

}
