using MarkMello.Application.Abstractions;
using MarkMello.Application.Updates;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;
using MarkMello.Domain.Workspace;
using MarkMello.Presentation.Editing;

namespace MarkMello.Presentation.Tests;

internal sealed class RecordingDocumentSaver : IDocumentSaver
{
    public List<(string Path, string Content)> Saves { get; } = [];

    public Exception? NextException { get; set; }

    public Task SaveAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        if (NextException is Exception exception)
        {
            NextException = null;
            return Task.FromException(exception);
        }

        Saves.Add((path, content));
        return Task.CompletedTask;
    }
}

internal sealed class StubDocumentLoader : IDocumentLoader
{
    public Dictionary<string, MarkdownSource> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Exception? NextException { get; set; }

    public Task<MarkdownSource> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (NextException is Exception exception)
        {
            NextException = null;
            return Task.FromException<MarkdownSource>(exception);
        }

        if (Sources.TryGetValue(path, out var source))
        {
            return Task.FromResult(source);
        }

        return Task.FromException<MarkdownSource>(new FileNotFoundException("Document was not found.", path));
    }
}

internal sealed class StubFilePicker : IFilePicker
{
    public string? OpenPath { get; set; }

    public string? SavePath { get; set; }

    public List<string> SuggestedSaveFileNames { get; } = [];

    public string? OpenFolderPath { get; set; }

    public int PickFolderCallCount { get; private set; }

    public Task<string?> PickMarkdownFileAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(OpenPath);

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        PickFolderCallCount++;
        return Task.FromResult(OpenFolderPath);
    }

    public Task<string?> PickSaveMarkdownFileAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        SuggestedSaveFileNames.Add(suggestedFileName);
        return Task.FromResult(SavePath);
    }
}

internal sealed class StubCommandLineActivation : ICommandLineActivation
{
    public string? ActivationPath { get; set; }

    public string? GetActivationFilePath() => ActivationPath;

    public event EventHandler<FileActivationEventArgs>? FileActivated;

    /// <summary>
    /// Simulates a runtime «open this file» signal (e.g. the macOS
    /// AppleEvent that fires while the app is already running).
    /// </summary>
    public void RaiseFileActivated(string path)
        => FileActivated?.Invoke(this, new FileActivationEventArgs(path));
}

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public ReadingPreferences Preferences { get; set; } = ReadingPreferences.Default;

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public AppLanguage Language { get; set; } = AppLanguage.English;

    public WindowPlacement? WindowPlacement { get; set; }

    public WindowBorderMode WindowBorderMode { get; set; } = WindowBorderMode.Auto;

    public double SidebarWidth { get; set; } = WorkspaceSidebarWidth.Default;

    public ValueTask<ReadingPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Preferences);

    public ValueTask<double> LoadSidebarWidthAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(SidebarWidth);

    public ValueTask SaveSidebarWidthAsync(double width, CancellationToken cancellationToken = default)
    {
        SidebarWidth = WorkspaceSidebarWidth.Normalize(width);
        return ValueTask.CompletedTask;
    }

    public ValueTask SavePreferencesAsync(ReadingPreferences preferences, CancellationToken cancellationToken = default)
    {
        Preferences = preferences;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ThemeMode> LoadThemeAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Theme);

    public ValueTask SaveThemeAsync(ThemeMode theme, CancellationToken cancellationToken = default)
    {
        Theme = theme;
        return ValueTask.CompletedTask;
    }

    public ValueTask<WindowBorderMode> LoadWindowBorderModeAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(WindowBorderMode);

    public ValueTask SaveWindowBorderModeAsync(WindowBorderMode mode, CancellationToken cancellationToken = default)
    {
        WindowBorderMode = mode;
        return ValueTask.CompletedTask;
    }

    public ValueTask<AppLanguage> LoadLanguageAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Language);

    public ValueTask SaveLanguageAsync(AppLanguage language, CancellationToken cancellationToken = default)
    {
        Language = language;
        return ValueTask.CompletedTask;
    }

    public ValueTask<WindowPlacement?> LoadWindowPlacementAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(WindowPlacement);

    public ValueTask SaveWindowPlacementAsync(
        WindowPlacement? placement,
        CancellationToken cancellationToken = default)
    {
        WindowPlacement = MarkMello.Domain.WindowPlacement.Normalize(placement);
        return ValueTask.CompletedTask;
    }
}

internal sealed class RecordingThemeService : IThemeService
{
    public ThemeMode AppliedTheme { get; private set; } = ThemeMode.System;

    public ThemeMode EffectiveTheme { get; private set; } = ThemeMode.Light;

    public void Apply(ThemeMode mode)
    {
        AppliedTheme = mode;
        EffectiveTheme = mode == ThemeMode.System ? ThemeMode.Light : mode;
    }

    public ThemeMode GetEffectiveTheme() => EffectiveTheme;
}

internal sealed class RecordingStartupMetrics : IStartupMetrics
{
    public List<StartupStage> Marks { get; } = [];

    public void Mark(StartupStage stage)
    {
        Marks.Add(stage);
    }

    public StartupSnapshot Snapshot()
        => new(Marks
            .Distinct()
            .ToDictionary(static stage => stage, static _ => TimeSpan.Zero));
}

internal sealed class TestMarkdownRenderer : IMarkdownDocumentRenderer
{
    /// <summary>
    /// Number of completed renders. Edit-mode preview must not run one of these
    /// per keystroke — see <c>EditorSessionViewModelTests</c>.
    /// </summary>
    public int RenderCount { get; private set; }

    public RenderedMarkdownDocument Render(string markdown)
    {
        RenderCount++;
        return RenderedMarkdownDocument.PlainText(markdown);
    }

    public RenderedMarkdownDocument Render(string markdown, string? baseDirectory)
    {
        RenderCount++;
        var document = RenderedMarkdownDocument.PlainText(markdown);
        return baseDirectory is null ? document : document with { BaseDirectory = baseDirectory };
    }
}

/// <summary>
/// <see cref="IEditorPreviewScheduler"/> that holds the pending render until the
/// test flushes it, standing in for the debounce timer used in production.
/// </summary>
internal sealed class ManualEditorPreviewScheduler : IEditorPreviewScheduler
{
    private Action? _pending;

    public int ScheduleCount { get; private set; }

    public int CancelCount { get; private set; }

    public bool HasPendingRender => _pending is not null;

    public void Schedule<T>(Func<T> render, Action<T> apply)
    {
        ScheduleCount++;
        _pending = () => apply(render());
    }

    public void Cancel()
    {
        CancelCount++;
        _pending = null;
    }

    public void Flush()
    {
        var pending = _pending;
        _pending = null;
        pending?.Invoke();
    }
}

/// <summary>
/// In-memory <see cref="IDiagramRenderService"/> for tests. Defaults to a
/// Mermaid renderer that echoes the source inside a fake SVG payload, which
/// keeps the production composition rules satisfied (every supported dialect
/// must have a renderer) without dragging Naiad into unit tests.
/// </summary>
internal sealed class FakeDiagramRenderService : IDiagramRenderService
{
    public Func<MarkdownDiagramKind, string, DiagramRenderResult> Handler { get; set; }
        = (_, source) => new DiagramRenderResult.Success($"<svg data-source=\"{source}\" />");

    public bool IsSupported(MarkdownDiagramKind kind)
        => kind == MarkdownDiagramKind.Mermaid;

    public DiagramRenderResult Render(MarkdownDiagramKind kind, string source)
        => Handler(kind, source);
}

internal sealed class StubUpdateService : IUpdateService
{
    public UpdateCheckResult NextCheckResult { get; set; }
        = new UpdateCheckResult.SourceNotConfigured("Update source is not configured.");

    public UpdateDownloadResult NextDownloadResult { get; set; }
        = new UpdateDownloadResult.Failed("No downloaded update configured for this test.");

    public UpdatePrepareResult NextPrepareResult { get; set; }
        = new UpdatePrepareResult.Failed("No native handoff configured for this test.");

    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(NextCheckResult);

    public Task<UpdateDownloadResult> DownloadUpdateAsync(
        AppUpdatePackage package,
        CancellationToken cancellationToken = default)
        => Task.FromResult(NextDownloadResult);

    public Task<UpdatePrepareResult> PrepareDownloadedUpdateAsync(
        AppUpdatePackage package,
        string downloadedFilePath,
        CancellationToken cancellationToken = default)
        => Task.FromResult(NextPrepareResult);
}
