using Avalonia.Controls;
using MarkMello.Domain;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownListLayoutTests
{
    [Fact]
    public void ListMarkerHasNoArtificialTopOffset()
    {
        var document = new RenderedMarkdownDocument(
        [
            new MarkdownListBlock(true,
            [
                new MarkdownListItem(
                [
                    new MarkdownParagraphBlock(
                    [
                        new MarkdownStrongInline([new MarkdownTextInline("Viewer first.")]),
                        new MarkdownTextInline(" Reading comes before editing, always.")
                    ])
                ])
            ])
        ]);

        var view = new MarkdownDocumentView
        {
            Document = document,
            ReadingPreferences = ReadingPreferences.Default
        };

        var viewport = Assert.IsType<Border>(view.Content);
        var root = Assert.IsType<StackPanel>(viewport.Child);
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        var row = Assert.IsType<Grid>(Assert.Single(listPanel.Children));
        var marker = Assert.IsType<MarkdownSelectionTextFragment>(row.Children[0]);

        Assert.Equal(0, marker.Margin.Top);
    }
}
