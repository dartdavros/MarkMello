namespace MarkMello.Presentation.Views;

/// <summary>
/// Навигация оглавления, реализуемая активным контентом (reader или editor) —
/// по тому же принципу, что <see cref="IFindHost"/>. Строки — zero-based строки исходника.
/// </summary>
public interface IOutlineNavigationHost
{
    /// <summary>Строка исходника текущего места: верх видимой области или строка курсора.</summary>
    int? CurrentSourceLine { get; }

    event EventHandler? CurrentSourceLineChanged;

    bool TryNavigateToSourceLine(int sourceLine);
}
