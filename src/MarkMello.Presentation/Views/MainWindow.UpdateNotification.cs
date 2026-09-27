using System.Diagnostics.CodeAnalysis;

namespace MarkMello.Presentation.Views;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Avalonia windows release the cancellation source in OnClosed.")]
public partial class MainWindow
{
    private readonly CancellationTokenSource _updateNotificationLifetime = new();

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        // Start the delay when the first window opens, outside startup initialization.
        _ = _viewModel.CheckForUpdatesAfterStartupAsync(_updateNotificationLifetime.Token);
        await _startupInitializationTask.ConfigureAwait(true);
        await CompleteStartupSmokeTestAsync().ConfigureAwait(true);
    }

    private void StopUpdateNotification()
    {
        _updateNotificationLifetime.Cancel();
        _updateNotificationLifetime.Dispose();
        Opened -= OnWindowOpened;
    }
}
