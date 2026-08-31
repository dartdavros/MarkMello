namespace MarkMello.Domain.Workspace;

/// <summary>
/// Ширина сайдбара: значение по умолчанию и допустимый диапазон из макета.
/// Нормализация нужна, потому что ширина сохраняется в настройках и может
/// прийти испорченной из файла или из старой версии.
/// </summary>
public static class WorkspaceSidebarWidth
{
    public const double Default = 260;
    public const double Minimum = 220;
    public const double Maximum = 340;

    public static double Normalize(double? width)
    {
        if (width is not { } value || double.IsNaN(value) || double.IsInfinity(value))
        {
            return Default;
        }

        return Math.Clamp(value, Minimum, Maximum);
    }
}
