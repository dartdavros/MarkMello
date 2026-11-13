using System.ComponentModel;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

public partial class MainWindow
{
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ReadingPreferences))
        {
            ApplyInterfaceFontSize();
            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.ShowsSidebar)
            or nameof(ShellViewModel.SidebarWidth))
        {
            SyncSidebarColumn();
            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.OutlineLeftContent)
            or nameof(ShellViewModel.OutlineRightContent)
            or nameof(ShellViewModel.OutlinePreferences))
        {
            SyncOutlineColumns();
            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.ActiveDocumentContent)
            or nameof(ShellViewModel.IsEditMode)
            or nameof(ShellViewModel.IsViewer))
        {
            ScheduleOutlineHostRefresh();
        }

        if (e.PropertyName is nameof(ShellViewModel.ShellOverlay)
            or nameof(ShellViewModel.IsSettingsOpen)
            or nameof(ShellViewModel.IsAppMenuOpen)
            or nameof(ShellViewModel.IsAppSettingsOpen)
            or nameof(ShellViewModel.IsAppAboutOpen)
            or nameof(ShellViewModel.HasOpenOverlay))
        {
            SyncOverlayWindowClasses();
            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.ReadingProgress)
            || e.PropertyName == nameof(ShellViewModel.IsViewer))
        {
            UpdateReadingProgressBarWidth();
            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.IsEditMode))
        {
            InvalidateFindHost();
            UpdateReadingProgressBarWidth();
            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.State))
        {
            InvalidateFindHost();
            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.IsFindBarOpen))
        {
            if (_viewModel.IsFindBarOpen)
            {
                ResolveFindHost()?.ApplyQuery(_viewModel.FindQuery);
            }
            else
            {
                ResolveFindHost()?.ClearFind();
            }

            SyncFindCountersFromHost();
            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.FindQuery))
        {
            if (_viewModel.IsFindBarOpen)
            {
                ResolveFindHost()?.ApplyQuery(_viewModel.FindQuery);
                SyncFindCountersFromHost();
            }

            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.TitleBarMaximize)
            or nameof(ShellViewModel.TitleBarRestore))
        {
            UpdateTitleBarMaximizeVisuals();
        }

        if (e.PropertyName == nameof(ShellViewModel.WindowBorderMode))
        {
            UpdateWindowBorder();
        }
    }

}
