using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Панель оглавления. DataContext — ShellViewModel: заголовок и текст пустого состояния живут в нём.
/// </summary>
public partial class OutlinePanelView : UserControl
{
    public OutlinePanelView()
    {
        InitializeComponent();

        // TreeViewItem помечает нажатие обработанным ради выделения, поэтому Tapped
        // до подписи не доходит: слушаем отпускание кнопки вместе с обработанными событиями.
        OutlineTree.AddHandler(
            PointerReleasedEvent,
            OnTreePointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Шеврон только раскрывает и сворачивает ветку, переход он не запускает.
        if (e.InitialPressMouseButton != MouseButton.Left
            || e.Source is not Visual source
            || source.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null)
        {
            return;
        }

        if (source.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is OutlineItemViewModel item)
        {
            Navigate(item);
        }
    }

    internal void Navigate(OutlineItemViewModel item)
    {
        if (DataContext is ShellViewModel { Outline: { } outline })
        {
            outline.NavigateCommand.Execute(item);
        }
    }
}
