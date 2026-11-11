using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

public sealed partial class ShellViewModelTests
{
    [Fact]
    public void ToggleOutlineFlipsVisibilityAndKeepsSide()
    {
        var shell = CreateHarness().ViewModel;

        shell.ToggleOutlineCommand.Execute(null);

        Assert.True(shell.OutlinePreferences.IsVisible);
        Assert.Equal(OutlinePanelSide.Left, shell.OutlinePreferences.Side);

        shell.ToggleOutlineCommand.Execute(null);

        Assert.False(shell.OutlinePreferences.IsVisible);
    }

    [Fact]
    public async Task OutlineSideDecidesWhichColumnGetsContentAndMinimap()
    {
        var harness = CreateHarness();
        var shell = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "outline.md");
        harness.Loader.Sources[path] = CreateSource(path, "# Title\n\n## Section\n\ntext");
        await shell.OpenPathAsync(path);
        Assert.True(shell.IsViewer);
        shell.RenderedDocument = WithHeadings();

        shell.OutlinePreferences = OutlinePreferences.Default with { IsVisible = true, Side = OutlinePanelSide.Right };

        Assert.True(shell.HidesMinimapForOutline);
        Assert.NotNull(shell.OutlineRightContent);
        Assert.Null(shell.OutlineLeftContent);
        Assert.Equal(2, shell.Outline!.Items.Count);

        shell.OutlinePreferences = shell.OutlinePreferences with { Side = OutlinePanelSide.Left };

        Assert.False(shell.HidesMinimapForOutline);
        Assert.NotNull(shell.OutlineLeftContent);
        Assert.Null(shell.OutlineRightContent);
    }

    [Fact]
    public async Task HiddenOutlineIsNotCreatedWhileViewingDocument()
    {
        var harness = CreateHarness();
        var shell = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "hidden-outline.md");
        harness.Loader.Sources[path] = CreateSource(path, "# Title\n\ntext");

        await shell.OpenPathAsync(path);
        shell.RenderedDocument = WithHeadings();

        Assert.True(shell.IsViewer);
        Assert.False(shell.OutlinePreferences.IsVisible);
        Assert.Null(shell.Outline);
        Assert.Null(shell.OutlineLeftContent);
        Assert.Null(shell.OutlineRightContent);
        Assert.False(shell.HidesMinimapForOutline);
    }

    [Fact]
    public async Task FloatingChromeGroupFollowsMenuAndOutlineButtons()
    {
        var harness = CreateHarness();
        var shell = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "chrome-group.md");
        harness.Loader.Sources[path] = CreateSource(path, "# Title" + Environment.NewLine + Environment.NewLine + "text");
        var raised = new List<string?>();
        shell.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await shell.OpenPathAsync(path);

        Assert.True(shell.ShowsFloatingAppMenuButton);
        Assert.True(shell.ShowsFloatingOutlineButton);
        Assert.True(shell.ShowsFloatingChromeGroup);
        Assert.Contains(nameof(ShellViewModel.ShowsFloatingChromeGroup), raised);

        raised.Clear();
        shell.ToggleEditModeCommand.Execute(null);

        Assert.False(shell.ShowsFloatingAppMenuButton);
        Assert.False(shell.ShowsFloatingOutlineButton);
        Assert.False(shell.ShowsFloatingChromeGroup);
        Assert.Contains(nameof(ShellViewModel.ShowsFloatingChromeGroup), raised);
    }

    [Fact]
    public void SetOutlineWidthNormalizes()
    {
        var shell = CreateHarness().ViewModel;

        shell.SetOutlineWidth(999);

        Assert.Equal(OutlinePreferences.MaxWidth, shell.OutlinePreferences.Width);
    }

    [Fact]
    public void SetOutlineWidthKeepsWidthInsideRange()
    {
        var shell = CreateHarness().ViewModel;

        shell.SetOutlineWidth(500);

        Assert.Equal(500, shell.OutlinePreferences.Width);
    }

    [Fact]
    public async Task ReshowingOutlineRebuildsItFromTheCurrentDocument()
    {
        var harness = CreateHarness();
        var shell = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "reshow-outline.md");
        harness.Loader.Sources[path] = CreateSource(path, "# One\n\n# Two\n\ntext");
        await shell.OpenPathAsync(path);
        shell.RenderedDocument = WithHeadings();
        shell.OutlinePreferences = OutlinePreferences.Default with { IsVisible = true };
        Assert.Equal(["One", "Two"], shell.Outline!.Items.Select(i => i.Text));

        shell.OutlinePreferences = shell.OutlinePreferences with { IsVisible = false };
        shell.RenderedDocument = new RenderedMarkdownDocument(
        [
            new MarkdownHeadingBlock(1, [new MarkdownTextInline("Other")]) { SourceSpan = new MarkdownSourceSpan(0) }
        ]);
        shell.OutlinePreferences = shell.OutlinePreferences with { IsVisible = true };

        Assert.Equal(["Other"], shell.Outline!.Items.Select(i => i.Text));
    }

    [Fact]
    public async Task ReshowingOutlineInEditModeUsesTheEditorPreview()
    {
        var harness = CreateHarness(renderer: new HeadingLineMarkdownRenderer());
        var shell = harness.ViewModel;
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "reshow-outline-edit.md");
        harness.Loader.Sources[path] = CreateSource(path, "# One");
        await shell.OpenPathAsync(path);
        shell.OutlinePreferences = OutlinePreferences.Default with { IsVisible = true };
        shell.ToggleEditModeCommand.Execute(null);
        Assert.Equal(["One"], shell.Outline!.Items.Select(i => i.Text));
        shell.OutlinePreferences = shell.OutlinePreferences with { IsVisible = false };

        shell.EditorSession!.SourceText = "# Edited";
        shell.OutlinePreferences = shell.OutlinePreferences with { IsVisible = true };

        Assert.True(shell.IsEditMode);
        Assert.Equal(["Edited"], shell.Outline!.Items.Select(i => i.Text));
    }

    [Fact]
    public async Task SwitchingTabsSwitchesWhichFoldStateIsUsed()
    {
        var harness = CreateHarness(renderer: new HeadingLineMarkdownRenderer());
        var shell = harness.ViewModel;
        var pathA = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "fold-a.md");
        var pathB = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "fold-b.md");
        harness.Loader.Sources[pathA] = CreateSource(pathA, "# Doc" + "\n" + "## Usage");
        harness.Loader.Sources[pathB] = CreateSource(pathB, "# Doc" + "\n" + "## Usage");
        await shell.OpenPathAsync(pathA);
        var tabA = shell.OpenDocuments.ActiveTab!;
        await shell.OpenPathAsync(pathB);
        var tabB = shell.OpenDocuments.ActiveTab!;
        shell.OutlinePreferences = OutlinePreferences.Default with { IsVisible = true };

        await shell.OpenDocuments.ActivateCommand.ExecuteAsync(tabA);
        shell.Outline!.Items[0].IsExpanded = false;

        await shell.OpenDocuments.ActivateCommand.ExecuteAsync(tabB);
        Assert.True(shell.Outline!.Items[0].IsExpanded);

        await shell.OpenDocuments.ActivateCommand.ExecuteAsync(tabA);
        Assert.False(shell.Outline!.Items[0].IsExpanded);
    }

    private sealed class HeadingLineMarkdownRenderer : IMarkdownDocumentRenderer
    {
        public RenderedMarkdownDocument Render(string markdown) => Render(markdown, null);

        public RenderedMarkdownDocument Render(string markdown, string? baseDirectory)
            => new(markdown.Split('\n')
                .Select((line, index) => (line, index))
                .Where(x => x.line.StartsWith('#'))
                .Select(x => (MarkdownBlock)new MarkdownHeadingBlock(
                    x.line.TakeWhile(c => c == '#').Count(),
                    [new MarkdownTextInline(x.line.TrimStart('#').Trim())])
                {
                    SourceSpan = new MarkdownSourceSpan(x.index)
                })
                .ToArray());
    }

    [Fact]
    public async Task TypographyChangesDoNotRaiseOutlineLayoutProperties()
    {
        var shell = await CreateOutlineTestShellAsync(withHeadings: true, outlineVisible: true);
        var raised = new List<string?>();
        shell.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        string[] layout =
        [
            nameof(ShellViewModel.ShowsOutline), nameof(ShellViewModel.OutlineLeftContent),
            nameof(ShellViewModel.OutlineRightContent), nameof(ShellViewModel.HidesMinimapForOutline)
        ];

        shell.OutlineLevelSettings[1].IncreaseFontSizeCommand.Execute(null);
        shell.IncreaseOutlineLineHeightCommand.Execute(null);

        Assert.DoesNotContain(raised, name => layout.Contains(name));

        shell.IsOutlineRightSelected = true;
        Assert.Contains(nameof(ShellViewModel.HidesMinimapForOutline), raised);

        raised.Clear();
        shell.ToggleOutlineCommand.Execute(null);
        Assert.Contains(nameof(ShellViewModel.ShowsOutline), raised);
        Assert.Contains(nameof(ShellViewModel.HidesMinimapForOutline), raised);
    }

    internal static async Task<ShellViewModel> CreateOutlineTestShellAsync(bool withHeadings, bool outlineVisible)
    {
        var harness = CreateHarness();
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "outline-view.md");
        harness.Loader.Sources[path] = CreateSource(path, "# One\n\n# Two\n\ntext");
        await harness.ViewModel.OpenPathAsync(path);
        harness.ViewModel.RenderedDocument = withHeadings ? WithHeadings() : new RenderedMarkdownDocument([]);
        harness.ViewModel.OutlinePreferences = OutlinePreferences.Default with { IsVisible = outlineVisible };
        return harness.ViewModel;
    }

    private static RenderedMarkdownDocument WithHeadings()
        => new(
        [
            new MarkdownHeadingBlock(1, [new MarkdownTextInline("One")]) { SourceSpan = new MarkdownSourceSpan(0) },
            new MarkdownHeadingBlock(1, [new MarkdownTextInline("Two")]) { SourceSpan = new MarkdownSourceSpan(4) }
        ]);

    [Fact]
    public void LevelSettingsChangeFontSizeAndMaxLevel()
    {
        var shell = CreateHarness().ViewModel;
        var h2 = shell.OutlineLevelSettings[1];
        var before = shell.OutlinePreferences.H2FontSize;

        h2.IncreaseFontSizeCommand.Execute(null);
        shell.OutlineLevelSettings[4].IsMaxLevel = true;

        Assert.Equal(before + 1, shell.OutlinePreferences.H2FontSize);
        Assert.Equal($"{before + 1} px", h2.FontSizeLabel);
        Assert.Equal(5, shell.OutlinePreferences.MaxLevel);
        Assert.True(shell.OutlineLevelSettings[4].IsWithinMaxLevel);
        Assert.False(shell.OutlineLevelSettings[5].IsWithinMaxLevel);
    }

    [Fact]
    public void LineHeightStepsAndSideSelection()
    {
        var shell = CreateHarness().ViewModel;
        var before = shell.OutlinePreferences.LineHeight;

        shell.IncreaseOutlineLineHeightCommand.Execute(null);
        shell.IsOutlineRightSelected = true;

        Assert.Equal(before + OutlinePreferences.LineHeightStep, shell.OutlinePreferences.LineHeight, 3);
        Assert.Equal(OutlinePanelSide.Right, shell.OutlinePreferences.Side);
        Assert.True(shell.IsOutlineRightSelected);
        Assert.False(shell.IsOutlineLeftSelected);
    }
}
