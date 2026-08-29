using Avalonia.Controls;
using Avalonia.Input;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Полоса вкладок. Активация и средний клик обрабатываются здесь, потому что
/// это жесты, а не команды: XAML-кнопка на всю вкладку сломала бы крестик внутри неё.
/// </summary>
public partial class TabStripView : UserControl
{
    public TabStripView()
    {
        InitializeComponent();
    }

    private void OnStripSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Ширина полосы — единственное, что решает, сколько вкладок помещается.
            viewModel.OpenDocuments.AvailableWidth = e.NewSize.Width;
        }
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: DocumentTabViewModel tab }
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsMiddleButtonPressed)
        {
            viewModel.OpenDocuments.CloseCommand.Execute(tab);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            viewModel.OpenDocuments.ActivateCommand.Execute(tab);
        }
    }
}
