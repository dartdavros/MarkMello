namespace MarkMello.Presentation.ViewModels;

public partial class ShellViewModel
{
    public bool IsCurrentDocumentSaved => !IsDirty && !string.IsNullOrWhiteSpace(CurrentDocumentPath);
}
