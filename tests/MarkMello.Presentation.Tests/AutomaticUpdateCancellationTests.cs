using MarkMello.Infrastructure.Settings;
using MarkMello.Presentation.Services;

namespace MarkMello.Presentation.Tests;

public sealed class AutomaticUpdateCancellationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MarkMello-update-cancellation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DisabledStartupWaitsForExplicitEnableBeforeDiscovery()
    {
        var preferences = new AutomaticUpdatePreferences(_root);
        Assert.True(preferences.SetEnabled(false));
        var service = new StubUpdateService();
        using var check = new DeferredUpdateCheck(service, TimeSpan.FromMilliseconds(20), preferences);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pending = check.WaitAsync(timeout.Token);
        await Task.Delay(60, timeout.Token);
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, service.CheckCount);

        preferences.SetEnabled(true);
        await pending;
        Assert.Equal(1, service.CheckCount);
    }

    [Fact]
    public async Task DisablingPendingDiscoveryCancelsItForAllWindowsAndEnableResumesOnce()
    {
        var preferences = new AutomaticUpdatePreferences(_root);
        var service = new StubUpdateService();
        using var check = new DeferredUpdateCheck(service, TimeSpan.FromMilliseconds(100), preferences);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var first = check.WaitAsync(timeout.Token);
        var second = check.WaitAsync(timeout.Token);

        preferences.SetEnabled(false);
        await Task.Delay(150, timeout.Token);
        Assert.False(first.IsCompleted);
        Assert.Equal(0, service.CheckCount);

        preferences.SetEnabled(true);
        var results = await Task.WhenAll(first, second);
        Assert.Same(results[0], results[1]);
        Assert.Equal(1, service.CheckCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
