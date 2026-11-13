using MarkMello.Application.Abstractions;
using MarkMello.Application.Updates;

namespace MarkMello.Presentation.Services;

/// <summary>One delayed discovery shared by all windows in this application run.</summary>
public sealed class DeferredUpdateCheck : IDisposable
{
    private readonly IUpdateService _updateService;
    private readonly TimeSpan _delay;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private Task<UpdateCheckResult>? _check;
    private CancellationTokenSource? _pending;
    private TaskCompletionSource _enabled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;

    public IAutomaticUpdatePreferences? Preferences { get; }

    public DeferredUpdateCheck(IUpdateService updateService)
        : this(updateService, TimeSpan.FromSeconds(30))
    {
    }

    public DeferredUpdateCheck(IUpdateService updateService, IAutomaticUpdatePreferences preferences)
        : this(updateService, TimeSpan.FromSeconds(30), preferences)
    {
    }

    internal DeferredUpdateCheck(
        IUpdateService updateService, TimeSpan delay, IAutomaticUpdatePreferences? preferences = null)
    {
        ArgumentNullException.ThrowIfNull(updateService);
        _updateService = updateService;
        _delay = delay;
        Preferences = preferences;
        if (Preferences is not null)
        {
            Preferences.Changed += OnPreferenceChanged;
        }
    }

    public async Task<UpdateCheckResult> WaitAsync(CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, cancellationToken);
        while (true)
        {
            Task<UpdateCheckResult>? check = null;
            Task? enabled = null;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (Preferences?.Enabled == false)
                {
                    enabled = _enabled.Task;
                }
                else
                {
                    if (_check is null)
                    {
                        _pending = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                        _check = CheckAsync(_pending.Token);
                    }
                    check = _check;
                }
            }
            if (enabled is not null)
            {
                await enabled.WaitAsync(lifetime.Token).ConfigureAwait(false);
                continue;
            }
            try { return await check!.WaitAsync(lifetime.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
            {
                // Disabling automatic checks cancels discovery, not the window observer.
                // Re-enabling starts a fresh delay shared by all surviving windows.
            }
        }
    }

    private void OnPreferenceChanged(object? sender, EventArgs args)
    {
        CancellationTokenSource? pending = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            if (Preferences!.Enabled)
            {
                _enabled.TrySetResult();
            }
            else
            {
                pending = _pending;
                _pending = null;
                _check = null;
                if (_enabled.Task.IsCompleted)
                {
                    _enabled = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }
        pending?.Cancel();
        pending?.Dispose();
    }

    private async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await _updateService.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult.Failed(exception.Message);
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? pending;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            pending = _pending;
            _pending = null;
        }
        if (Preferences is not null)
        {
            Preferences.Changed -= OnPreferenceChanged;
        }
        _shutdown.Cancel();
        pending?.Dispose();
        _shutdown.Dispose();
    }
}
