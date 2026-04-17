using MarkMello.Domain;

namespace MarkMello.Domain.Tests;

public sealed class DocumentTextRangeTests
{
    [Fact]
    public void FromBoundsNormalizesReversedOffsets()
    {
        var range = DocumentTextRange.FromBounds(12, 4);

        Assert.Equal(4, range.Start);
        Assert.Equal(12, range.End);
        Assert.Equal(8, range.Length);
    }

    [Fact]
    public void IntersectionReturnsOverlapOnly()
    {
        var left = new DocumentTextRange(3, 10);
        var right = new DocumentTextRange(7, 15);

        var intersection = left.Intersection(right);

        Assert.Equal(new DocumentTextRange(7, 10), intersection);
    }

    [Theory]
    [InlineData(-10, 5, 5)]
    [InlineData(9, 9, 9)]
    [InlineData(50, 20, 20)]
    public void ClampOffsetConstrainsOffsetToRange(int offset, int expected, int upperBound)
    {
        var range = new DocumentTextRange(5, upperBound);

        var clamped = range.ClampOffset(offset);

        Assert.Equal(expected, clamped);
    }
}
