using MarkMello.Application.Abstractions;
using MarkMello.Application.Updates;
using MarkMello.Domain;
using MarkMello.Domain.Diagnostics;

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

    public Task<string?> PickMarkdownFileAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(OpenPath);

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
}

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public ReadingPreferences Preferences { get; set; } = ReadingPreferences.Default;

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public ValueTask<ReadingPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Preferences);

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
    public RenderedMarkdownDocument Render(string markdown)
        => RenderedMarkdownDocument.PlainText(markdown);

    public RenderedMarkdownDocument Render(string markdown, string? baseDirectory)
    {
        var document = RenderedMarkdownDocument.PlainText(markdown);
        return baseDirectory is null ? document : document with { BaseDirectory = baseDirectory };
    }
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
