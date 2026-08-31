using Avalonia;
using Avalonia.Controls;
using MarkMello.Domain;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Covers the source-line ↔ preview-offset mapping behind edit-mode scroll
/// synchronization. The regression these guard against: the mapping used to
/// speak in whole source lines, so a paragraph written as one soft-wrapped line
/// had no interior resolution and the preview stuck to its top edge while the
/// editor scrolled on.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class EditModeScrollSyncTests
{
    private static readonly double[] RoundTripPositions = [0.0, 0.5, 1.0, 2.0, 3.5, 4.0];

    private readonly AvaloniaHeadlessFixture _fixture;

    public EditModeScrollSyncTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task PositionInsideASingleLineParagraphAdvancesThePreview()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = CreateLaidOutView();

            Assert.True(view.TryGetVerticalOffsetForSourcePosition(0, out var atFirst));
            Assert.True(view.TryGetVerticalOffsetForSourcePosition(1, out var midway));
            Assert.True(view.TryGetVerticalOffsetForSourcePosition(2, out var atSecond));

            Assert.True(atSecond > atFirst, $"Second block must sit below the first ({atSecond} vs {atFirst}).");
            Assert.InRange(midway, atFirst + 0.001, atSecond - 0.001);
        }, CancellationToken.None);
    }

    [Fact]
    public Task MappingIsMonotonicAcrossTheDocument()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = CreateLaidOutView();

            var previous = double.NegativeInfinity;
            for (var position = 0.0; position <= 6.0; position += 0.25)
            {
                Assert.True(view.TryGetVerticalOffsetForSourcePosition(position, out var offset));
                Assert.True(offset >= previous, $"Offset went backwards at source position {position}.");
                previous = offset;
            }
        }, CancellationToken.None);
    }

    [Fact]
    public Task OffsetAndSourcePositionRoundTrip()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = CreateLaidOutView();

            foreach (var position in RoundTripPositions)
            {
                Assert.True(view.TryGetVerticalOffsetForSourcePosition(position, out var offset));
                Assert.True(view.TryGetSourcePositionForVerticalOffset(offset, out var roundTripped));
                Assert.Equal(position, roundTripped, precision: 3);
            }
        }, CancellationToken.None);
    }

    [Fact]
    public Task PositionsOutsideTheDocumentClampToItsEnds()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = CreateLaidOutView();

            Assert.True(view.TryGetVerticalOffsetForSourcePosition(0, out var first));
            Assert.True(view.TryGetVerticalOffsetForSourcePosition(-5, out var beforeStart));
            Assert.Equal(first, beforeStart);

            Assert.True(view.TryGetVerticalOffsetForSourcePosition(1_000, out var pastEnd));
            Assert.True(view.TryGetVerticalOffsetForSourcePosition(4, out var lastBlock));
            Assert.True(pastEnd >= lastBlock);
        }, CancellationToken.None);
    }

    [Fact]
    public Task EmptyDocumentReportsNoMapping()
    {
        return _fixture.Session.Dispatch(() =>
        {
            var view = new MarkdownDocumentView
            {
                Document = RenderedMarkdownDocument.Empty,
                ReadingPreferences = ReadingPreferences.Default
            };

            Assert.False(view.TryGetVerticalOffsetForSourcePosition(0, out _));
            Assert.False(view.TryGetSourcePositionForVerticalOffset(0, out _));
        }, CancellationToken.None);
    }

    /// <summary>
    /// Three paragraphs, each a single source line separated by a blank line —
    /// the shape markdown takes when paragraphs are soft-wrapped rather than
    /// hard-wrapped.
    /// </summary>
    private static MarkdownDocumentView CreateLaidOutView()
    {
        var document = new RenderedMarkdownDocument(
        [
            Paragraph("First paragraph of the document.", 0),
            Paragraph("Second paragraph, a good deal longer so that it wraps across several visual lines in the preview column.", 2),
            Paragraph("Third paragraph closes the document.", 4)
        ]);

        var view = new MarkdownDocumentView
        {
            Document = document,
            ReadingPreferences = ReadingPreferences.Default
        };

        var window = new Window { Width = 600, Height = 400, Content = view };
        window.Show();
        window.Measure(new Size(600, 400));
        window.Arrange(new Rect(0, 0, 600, 400));
        return view;
    }

    private static MarkdownParagraphBlock Paragraph(string text, int line)
        => new([new MarkdownTextInline(text)])
        {
            SourceSpan = new MarkdownSourceSpan(line)
        };
}
