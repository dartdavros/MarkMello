namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Решение пользователя по несохранённым изменениям перед destructive action.
/// </summary>
public enum DirtyResolutionChoice
{
    Save,
    Discard,
    Cancel
}
