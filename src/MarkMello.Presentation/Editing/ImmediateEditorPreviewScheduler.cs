namespace MarkMello.Presentation.Editing;

/// <summary>
/// Синхронный планировщик: рендерит на месте вызова. Значение по умолчанию для
/// unit-тестов и для сценариев без Avalonia dispatcher.
/// </summary>
public sealed class ImmediateEditorPreviewScheduler : IEditorPreviewScheduler
{
    public static ImmediateEditorPreviewScheduler Instance { get; } = new();

    public void Schedule<T>(Func<T> render, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(apply);

        apply(render());
    }

    public void Cancel()
    {
    }
}
