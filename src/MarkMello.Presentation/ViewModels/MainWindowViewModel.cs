using CommunityToolkit.Mvvm.ComponentModel;

namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// View model главного окна. В M0 содержит только заголовок и приветственный текст —
/// настоящие state-свойства (NoDocument/Viewing/Editing/...) появятся в M1.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "MarkMello";

    [ObservableProperty]
    private string _greeting = "MarkMello";

    [ObservableProperty]
    private string _tagline = "A quiet place to read Markdown.";
}
