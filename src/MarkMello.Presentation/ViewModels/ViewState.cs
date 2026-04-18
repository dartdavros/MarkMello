namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Основные состояния UI. По avalonia-design-translation.md.
/// Settings panel — secondary overlay in M4, editing state machine приходит позже в M5.
/// </summary>
public enum ViewState
{
    NoDocument,
    Viewing,
    LoadError
}
