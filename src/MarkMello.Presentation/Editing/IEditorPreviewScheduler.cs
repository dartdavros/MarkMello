namespace MarkMello.Presentation.Editing;

/// <summary>
/// Расписание пересборки preview в edit mode.
///
/// Полный markdown parse стоит десятки-сотни миллисекунд на больших документах,
/// поэтому он не должен выполняться синхронно на каждое нажатие клавиши.
/// Реализация коалесцирует запросы (новый вытесняет отложенный) и уводит
/// сам рендер с UI-потока, возвращая результат обратно на UI-поток.
/// </summary>
public interface IEditorPreviewScheduler
{
    /// <summary>
    /// Запрашивает пересборку preview. <paramref name="render"/> может быть
    /// выполнен на фоновом потоке, <paramref name="apply"/> всегда вызывается
    /// на UI-потоке и только для самого свежего запроса.
    /// </summary>
    void Schedule<T>(Func<T> render, Action<T> apply);

    /// <summary>
    /// Отменяет отложенный запрос. Используется, когда состояние документа
    /// меняется целиком (load/save/discard) и его нужно отрисовать немедленно.
    /// </summary>
    void Cancel();
}
