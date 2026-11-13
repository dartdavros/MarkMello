namespace MarkMello.Presentation.ViewModels;

/// <summary>
/// Открытие файла по сигналу извне: повторный запуск приложения или Apple Event.
/// </summary>
public partial class ShellViewModel
{
    /// <summary>
    /// Уже открытый файл не перечитывается с диска: во вкладке могут быть несохранённые
    /// правки, а перезагрузка сбросила бы editor-сессию. Достаточно переключиться на неё.
    /// </summary>
    public async Task OpenOrActivatePathAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (OpenDocuments.FindByPath(path) is { } tab)
        {
            await ActivateTabAsync(tab).ConfigureAwait(true);
            return;
        }

        await OpenPathAsync(path).ConfigureAwait(true);
    }
}
