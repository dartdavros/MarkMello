using Avalonia.Controls;
using Avalonia.Input;
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
}
