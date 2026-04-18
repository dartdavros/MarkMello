using Avalonia;

namespace MarkMello.Presentation.Views.Markdown;

internal readonly record struct MarkdownFragmentHitTestCandidate(Rect Bounds, Point LocalPoint);

internal static class MarkdownFragmentHitTester
{
    public static int FindBestIndex(IReadOnlyList<MarkdownFragmentHitTestCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return -1;
        }

        var bestIndex = -1;
        var bestDistance = double.PositiveInfinity;

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (candidate.Bounds.Contains(candidate.LocalPoint))
            {
                return index;
            }

            var distance = DistanceSquared(candidate.Bounds, candidate.LocalPoint);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static double DistanceSquared(Rect rect, Point point)
    {
        var dx = point.X < rect.X ? rect.X - point.X : point.X > rect.Right ? point.X - rect.Right : 0;
        var dy = point.Y < rect.Y ? rect.Y - point.Y : point.Y > rect.Bottom ? point.Y - rect.Bottom : 0;
        return dx * dx + dy * dy;
    }
}
