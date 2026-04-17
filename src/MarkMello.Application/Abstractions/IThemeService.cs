using MarkMello.Domain;

namespace MarkMello.Application.Abstractions;

/// <summary>
/// Применение пользовательской темы к UI framework. Реализация в Presentation
/// (зависит от Avalonia.Application). VM не должна знать про Avalonia напрямую.
/// </summary>
public interface IThemeService
{
    /// <summary>Применить тему к Application.RequestedThemeVariant.</summary>
    void Apply(ThemeMode mode);

    /// <summary>
    /// Возвращает фактически активную light/dark тему после применения RequestedThemeVariant.
    /// Для ThemeMode.System это должен быть уже резолвленный effective variant.
    /// </summary>
    ThemeMode GetEffectiveTheme();
}
