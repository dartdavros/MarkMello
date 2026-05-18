using Avalonia;
using MarkMello.Domain;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

public sealed class MainWindowPlacementTests
{
    [Fact]
    public void CalculateStartupWindowPlacementCentersDefaultWindowInsideWorkingArea()
    {
        var workingArea = new PixelRect(0, 0, 1920, 1040);

        var placement = MainWindow.CalculateStartupWindowPlacement(
            savedPlacement: null,
            workingArea,
            screenScaling: 1,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(1280d, placement.Width);
        Assert.Equal(840d, placement.Height);
        Assert.True(placement.X >= 0);
        Assert.True(placement.Y >= 0);
        Assert.True(placement.X + placement.Width <= workingArea.Width);
        Assert.True(placement.Y + placement.Height <= workingArea.Height);
    }

    [Fact]
    public void CalculateStartupWindowPlacementClampsSavedWindowInsideWorkingArea()
    {
        var workingArea = new PixelRect(0, 0, 1280, 720);
        var savedPlacement = new WindowPlacement(-200, -100, 1600, 1200, IsMaximized: false);

        var placement = MainWindow.CalculateStartupWindowPlacement(
            savedPlacement,
            workingArea,
            screenScaling: 1,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(1264d, placement.Width);
        Assert.Equal(704d, placement.Height);
        Assert.Equal(8d, placement.X);
        Assert.Equal(8d, placement.Y);
    }

    [Fact]
    public void CalculateStartupWindowPlacementUsesScreenScalingForPixelBounds()
    {
        var workingArea = new PixelRect(0, 0, 2880, 1800);
        var savedPlacement = new WindowPlacement(2600, 1700, 1200, 800, IsMaximized: false);

        var placement = MainWindow.CalculateStartupWindowPlacement(
            savedPlacement,
            workingArea,
            screenScaling: 2,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(472d, placement.X);
        Assert.Equal(192d, placement.Y);
        Assert.Equal(1200d, placement.Width);
        Assert.Equal(800d, placement.Height);
    }

    [Fact]
    public void CalculateAnchoredNormalPlacementKeepsPositionWhenWindowAlreadyInsideTargetScreen()
    {
        // Secondary monitor positioned right of primary.
        var workingArea = new PixelRect(1920, 0, 2560, 1440);

        var placement = MainWindow.CalculateAnchoredNormalPlacement(
            currentPosition: new PixelPoint(2200, 200),
            currentWidth: 1280,
            currentHeight: 840,
            workingArea,
            screenScaling: 1,
            minWidth: 640,
            minHeight: 480,
            defaultWidth: 1280,
            defaultHeight: 840);

        Assert.Equal(2200d, placement.X);
        Assert.Equal(200d, placement.Y);
        Assert.Equal(1280d, placement.Width);
        Assert.Equal(840d, placement.Height);
        Assert.False(placement.IsMaximized);
    }

    [Fact]
    public void CalculateAnchoredNormalPlacementClampsWindowSpanningTwoMonitorsBackToTargetScreen()
    {
        // Window sits at (1800,200), spilling out of primary (0..1920) onto secondary (1920..4480).
        // Caller decides target = secondary; placement must end up entirely within secondary.
        var secondaryWorkingArea = new PixelRect(1920, 0, 2560, 1440);

        var placement = MainWindow.CalculateAnchoredNormalPlacement(
            currentPosition: new PixelPoint(1800, 200),
            currentWidth: 1280,
            currentHeight: 840,
            secondaryWorkingArea,
            screenScaling: 1,
            minWidth: 640,
            minHeight: 480,
            defaultWidth: 1280,
            defaultHeight: 840);

        Assert.True(placement.X >= secondaryWorkingArea.X);
        Assert.True(placement.X + placement.Width <= secondaryWorkingArea.X + secondaryWorkingArea.Width);
        Assert.True(placement.Y >= secondaryWorkingArea.Y);
        Assert.True(placement.Y + placement.Height <= secondaryWorkingArea.Y + secondaryWorkingArea.Height);
        Assert.False(placement.IsMaximized);
    }

    [Fact]
    public void CalculateAnchoredNormalPlacementShrinksWindowToFitPortraitMonitor()
    {
        // Portrait monitor placed to the left of primary: 1080x1920 at (-1080, 0).
        var portraitWorkingArea = new PixelRect(-1080, 0, 1080, 1920);

        var placement = MainWindow.CalculateAnchoredNormalPlacement(
            currentPosition: new PixelPoint(-200, 500),
            currentWidth: 1280,
            currentHeight: 840,
            portraitWorkingArea,
            screenScaling: 1,
            minWidth: 640,
            minHeight: 480,
            defaultWidth: 1280,
            defaultHeight: 840);

        Assert.True(placement.Width <= portraitWorkingArea.Width);
        Assert.True(placement.X >= portraitWorkingArea.X);
        Assert.True(placement.X + placement.Width <= portraitWorkingArea.X + portraitWorkingArea.Width);
        Assert.True(placement.Y >= portraitWorkingArea.Y);
        Assert.False(placement.IsMaximized);
    }

    [Fact]
    public void CalculateAnchoredNormalPlacementHonoursScalingWhenSizingToWorkingArea()
    {
        // 4K monitor at 200% scaling — usable DIPs = 1920x1080.
        var workingArea = new PixelRect(3840, 0, 3840, 2160);

        var placement = MainWindow.CalculateAnchoredNormalPlacement(
            currentPosition: new PixelPoint(3900, 80),
            currentWidth: 5000, // bigger than available DIPs — must shrink
            currentHeight: 3000,
            workingArea,
            screenScaling: 2,
            minWidth: 640,
            minHeight: 480,
            defaultWidth: 1280,
            defaultHeight: 840);

        Assert.True(placement.Width * 2 <= workingArea.Width);
        Assert.True(placement.Height * 2 <= workingArea.Height);
        Assert.True(placement.X >= workingArea.X);
        Assert.True(placement.Y >= workingArea.Y);
        Assert.False(placement.IsMaximized);
    }

    [Fact]
    public void CalculateAnchoredNormalPlacementFallsBackToDefaultsForInvalidSize()
    {
        var workingArea = new PixelRect(0, 0, 1920, 1080);

        var placement = MainWindow.CalculateAnchoredNormalPlacement(
            currentPosition: new PixelPoint(50, 50),
            currentWidth: double.NaN,
            currentHeight: double.NegativeInfinity,
            workingArea,
            screenScaling: 1,
            minWidth: 640,
            minHeight: 480,
            defaultWidth: 1280,
            defaultHeight: 840);

        Assert.Equal(1280d, placement.Width);
        Assert.Equal(840d, placement.Height);
        Assert.False(placement.IsMaximized);
    }
}
