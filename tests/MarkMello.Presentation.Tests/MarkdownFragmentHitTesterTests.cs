using Avalonia;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownFragmentHitTesterTests
{
    [Fact]
    public void FindBestIndexPrefersFragmentThatActuallyContainsPoint()
    {
        var candidates = new[]
        {
            new MarkdownFragmentHitTestCandidate(new Rect(0, 0, 16, 24), new Point(72, 10)),
            new MarkdownFragmentHitTestCandidate(new Rect(0, 0, 400, 48), new Point(24, 10))
        };

        var bestIndex = MarkdownFragmentHitTester.FindBestIndex(candidates);

        Assert.Equal(1, bestIndex);
    }

    [Fact]
    public void FindBestIndexFallsBackToNearestFragmentWhenPointLandsInGap()
    {
        var candidates = new[]
        {
            new MarkdownFragmentHitTestCandidate(new Rect(0, 0, 16, 24), new Point(20, 10)),
            new MarkdownFragmentHitTestCandidate(new Rect(0, 0, 400, 48), new Point(-8, 10))
        };

        var bestIndex = MarkdownFragmentHitTester.FindBestIndex(candidates);

        Assert.Equal(0, bestIndex);
    }
}
