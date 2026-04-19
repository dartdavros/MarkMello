using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownInlineCodePadMetricsTests
{
    [Fact]
    public void SpacerRunsUseAbsoluteBaselineFromPadMetrics()
    {
        var metrics = new MarkdownInlineCodePadMetrics(
            LeftWidth: 4,
            RightWidth: 4,
            Height: 12,
            Baseline: 9);

        var left = MarkdownSpacerTextRun.Left(metrics);
        var right = MarkdownSpacerTextRun.Right(metrics);

        Assert.Equal(9, left.Baseline);
        Assert.Equal(9, right.Baseline);
    }
}
