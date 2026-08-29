using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Сайдбар открытой папки. Монтируется только когда папка открыта:
/// в single-file режиме контрола нет в визуальном дереве (ADR-0007 Rule 3).
/// </summary>
public partial class WorkspaceSidebarView : UserControl
{
    public WorkspaceSidebarView()
    {
        InitializeComponent();

        // TreeViewItem помечает нажатие обработанным ради выделения, поэтому Tapped
        // до строки не доходит: слушаем отпускание кнопки вместе с обработанными событиями.
        FileTree.AddHandler(
            PointerReleasedEvent,
            OnTreePointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        // Esc внутри TextBox помечается обработанным самим полем.
        SearchInput.AddHandler(
            KeyDownEvent,
            OnSearchKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return;
        }

        // Клик внутри поля инлайн-переименования правит имя, а не открывает документ.
        if (source.FindAncestorOfType<TextBox>(includeSelf: true) is not null)
        {
            return;
        }

        if (source.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext
            is FileTreeNodeViewModel node)
        {
            ActivateFromPointer(e.InitialPressMouseButton, node);
        }
    }

    /// <summary>
    /// Левый клик по строке открывает документ; правый только выделяет её и показывает меню.
    /// Отдельный метод, потому что в headless строки дерева не материализуются
    /// и до события мыши тест дотянуться не может.
    /// </summary>
    internal void ActivateFromPointer(MouseButton button, FileTreeNodeViewModel node)
    {
        if (button == MouseButton.Left && DataContext is ShellViewModel { Workspace: { } workspace })
        {
            workspace.OpenNodeCommand.Execute(node);
        }
    }

    /// <summary>
    /// Клавиатура дерева: Enter открывает строку, F2 переименовывает, Delete удаляет.
    /// `InputGesture` в пунктах контекстного меню — только подпись, обработчика за ней нет.
    /// </summary>
    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel { Workspace: { } workspace }
            || workspace.SelectedNode is not { } node)
        {
            return;
        }

        // Пока идёт ввод имени, клавиши принадлежат полю: иначе Delete из строки ввода
        // ушёл бы в удаление файла.
        if (workspace.IsEditingName)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                workspace.OpenNodeCommand.Execute(node);
                e.Handled = true;
                break;

            case Key.F2:
                workspace.StartRenameCommand.Execute(node);
                e.Handled = true;
                break;

            case Key.Delete:
                workspace.RequestDeleteCommand.Execute(node);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Поле ввода имени появляется вместе со строкой, поэтому фокус ставится здесь.
    /// При переименовании выделено имя без расширения, у нового файла курсор перед `.md`
    /// (макет 09).
    /// </summary>
    private void OnEditNameAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox editor || DataContext is not ShellViewModel { Workspace: { } workspace })
        {
            return;
        }

        editor.AddHandler(
            KeyDownEvent,
            OnEditNameKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        Dispatcher.UIThread.Post(
            () =>
            {
                editor.Focus();

                var name = editor.Text ?? string.Empty;
                if (workspace.EditKind != TreeEditKind.Rename)
                {
                    editor.CaretIndex = 0;
                    return;
                }

                var extension = Path.GetExtension(name).Length;
                editor.SelectionStart = 0;
                editor.SelectionEnd = name.Length - extension;
            },
            DispatcherPriority.Input);
    }

    /// <summary>Esc сбрасывает поиск, не выходя из поля: это самый частый способ вернуться к дереву.</summary>
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape
            || DataContext is not ShellViewModel { Workspace: { } workspace }
            || !workspace.HasSearchQuery)
        {
            return;
        }

        workspace.ClearSearchCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>Enter создаёт или переименовывает, Esc отменяет — как в любом инлайн-редакторе.</summary>
    private void OnEditNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel { Workspace: { } workspace })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                workspace.CommitEditCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                workspace.CancelEditCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Потеря фокуса отменяет ввод — но не тогда, когда поле уже показывает ошибку:
    /// иначе сообщение исчезало бы вместе с введённым именем.
    /// </summary>
    private void OnEditNameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel { Workspace: { HasEditError: false } workspace })
        {
            workspace.CancelEditCommand.Execute(null);
        }
    }
}
