using MarkMello.Application.Abstractions;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class ViewerViewOutlineTests(AvaloniaHeadlessFixture fixture)
{
    [Fact]
    public Task SourceLineIsFreshWhenFirstReadAfterScrollingWithoutSubscribers()
    {
        return fixture.RunAsync(async () =>
        {
            var shell = await ShellViewModelTests.CreateOutlineTestShellAsync(withHeadings: true, outlineVisible: false);
            var blocks = new List<MarkdownBlock>();
            for (var i = 0; i < 40; i++)
            {
                blocks.Add(new MarkdownHeadingBlock(2, [new MarkdownTextInline($"Section {i}")])
                {
                    SourceSpan = new MarkdownSourceSpan(i * 10)
                });
                blocks.Add(new MarkdownParagraphBlock([new MarkdownTextInline("text text text")])
                {
                    SourceSpan = new MarkdownSourceSpan((i * 10) + 2)
                });
            }

            shell.RenderedDocument = new RenderedMarkdownDocument(blocks);
            var window = new MainWindow(
                shell,
                StartupSmokeTestOptions.Disabled,
                new InMemorySettingsStore(),
                new RecordingStartupMetrics());
            window.Show();
            try
            {
                await Task.Yield();
                Layout(window);
                var viewer = window.GetVisualDescendants().OfType<ViewerView>().Single();
                var scroll = viewer.FindControl<ScrollViewer>("DocScroll")!;
                // Headless app has no theme, so ScrollViewer needs a minimal template to scroll.
                scroll.Template = new FuncControlTemplate((_, scope) => new ScrollContentPresenter { Name = "PART_ContentPresenter" }
                    .RegisterInNameScope(scope));
                scroll.ApplyTemplate();
                Layout(window);
                var cache = typeof(ViewerView).GetField(
                    "_outlineCurrentLine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

                for (var attempt = 0; attempt < 40 && scroll.ScrollBarMaximum.Y <= 0; attempt++)
                {
                    await Task.Delay(50);
                    Layout(window);
                }

                Assert.True(
                    scroll.ScrollBarMaximum.Y > 0,
                    $"extent={scroll.Extent} viewport={scroll.Viewport} children={viewer.FindControl<MarkdownDocumentView>("DocumentView")!.Bounds}");
                scroll.Offset = new Avalonia.Vector(0, scroll.ScrollBarMaximum.Y / 2);
                Layout(window);

                Assert.Null(cache.GetValue(viewer));
                var line = viewer.CurrentSourceLine;
                Assert.NotNull(line);
                Assert.True(line > 0);
                Assert.Equal(viewer.FindControl<MarkdownDocumentView>("DocumentView")!.GetCurrentHeadingSourceLine(), line);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Layout(Window window)
    {
        for (var pass = 0; pass < 4; pass++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
