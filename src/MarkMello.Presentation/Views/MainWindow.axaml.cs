using Avalonia.Controls;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
