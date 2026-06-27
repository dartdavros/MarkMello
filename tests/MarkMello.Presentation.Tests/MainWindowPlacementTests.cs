using Avalonia;
using MarkMello.Domain;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

public sealed class MainWindowPlacementTests
{
    [Fact]
    public void CalculateTitleBarContentMarginKeepsDefaultInsetOutsideMacOS()
    {
        var margin = MainWindow.CalculateTitleBarContentMargin(isMacOS: false);

        Assert.Equal(new Thickness(14, 0, 0, 0), margin);
    }

    [Fact]
    public void CalculateTitleBarContentMarginReservesMacOSWindowButtonsArea()
    {
        var margin = MainWindow.CalculateTitleBarContentMargin(isMacOS: true);

        Assert.Equal(new Thickness(82, 0, 14, 0), margin);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void CanUseDraggableTitleBarAllowsWindowsAndMacOS(
        bool isWindows,
        bool isMacOS,
        bool expected)
    {
        Assert.Equal(expected, MainWindow.CanUseDraggableTitleBar(isWindows, isMacOS));
    }

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
    public void CalculateWindowsMonitorMaximizeBoundsUsesWorkAreaRelativeToMonitor()
    {
        var monitorBounds = new PixelRect(1920, 0, 1920, 1080);
        var workingArea = new PixelRect(1960, 0, 1880, 1040);

        var bounds = MainWindow.CalculateWindowsMonitorMaximizeBounds(
            monitorBounds,
            workingArea,
            renderScaling: 1,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(40, bounds.MaxPositionX);
        Assert.Equal(0, bounds.MaxPositionY);
        Assert.Equal(1880, bounds.MaxSizeWidth);
        Assert.Equal(1040, bounds.MaxSizeHeight);
    }

    [Fact]
    public void CalculateWindowsMonitorMaximizeBoundsHandlesNegativeMonitorCoordinates()
    {
        var monitorBounds = new PixelRect(-1080, 0, 1080, 1920);
        var workingArea = new PixelRect(-1080, 40, 1080, 1880);

        var bounds = MainWindow.CalculateWindowsMonitorMaximizeBounds(
            monitorBounds,
            workingArea,
            renderScaling: 1,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(0, bounds.MaxPositionX);
        Assert.Equal(40, bounds.MaxPositionY);
        Assert.Equal(1080, bounds.MaxSizeWidth);
        Assert.Equal(1880, bounds.MaxSizeHeight);
    }

    [Fact]
    public void CalculateWindowsMonitorMaximizeBoundsUsesTopMonitorWorkAreaRelativeToThatMonitor()
    {
        var monitorBounds = new PixelRect(0, -1080, 1920, 1080);
        var workingArea = new PixelRect(0, -1080, 1920, 1032);

        var bounds = MainWindow.CalculateWindowsMonitorMaximizeBounds(
            monitorBounds,
            workingArea,
            renderScaling: 1,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(0, bounds.MaxPositionX);
        Assert.Equal(0, bounds.MaxPositionY);
        Assert.Equal(1920, bounds.MaxSizeWidth);
        Assert.Equal(1032, bounds.MaxSizeHeight);
    }

    [Fact]
    public void CalculateWindowsMonitorMaximizeBoundsUsesPortraitMonitorWorkArea()
    {
        var monitorBounds = new PixelRect(-1080, -1588, 1080, 1920);
        var workingArea = new PixelRect(-1080, -1588, 1080, 1872);

        var bounds = MainWindow.CalculateWindowsMonitorMaximizeBounds(
            monitorBounds,
            workingArea,
            renderScaling: 1,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(0, bounds.MaxPositionX);
        Assert.Equal(0, bounds.MaxPositionY);
        Assert.Equal(1080, bounds.MaxSizeWidth);
        Assert.Equal(1872, bounds.MaxSizeHeight);
    }

    [Fact]
    public void CalculateWindowsMonitorMaximizeBoundsScalesMinTrackSize()
    {
        var monitorBounds = new PixelRect(0, 0, 3840, 2160);
        var workingArea = new PixelRect(0, 0, 3840, 2080);

        var bounds = MainWindow.CalculateWindowsMonitorMaximizeBounds(
            monitorBounds,
            workingArea,
            renderScaling: 1.5,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(960, bounds.MinTrackWidth);
        Assert.Equal(720, bounds.MinTrackHeight);
    }

    [Fact]
    public void CalculatePixelRectIntersectionAreaReturnsOverlapArea()
    {
        var first = new PixelRect(0, -120, 1920, 720);
        var second = new PixelRect(0, 0, 1920, 1080);

        var area = MainWindow.CalculatePixelRectIntersectionArea(first, second);

        Assert.Equal(1920L * 600, area);
    }

    [Fact]
    public void CalculatePixelRectIntersectionAreaReturnsZeroForSeparatedScreens()
    {
        var first = new PixelRect(0, 0, 1920, 1080);
        var second = new PixelRect(0, -1080, 1920, 1080);

        var area = MainWindow.CalculatePixelRectIntersectionArea(first, second);

        Assert.Equal(0, area);
    }

    [Fact]
    public void FindWindowsMonitorAreaForWindowBoundsChoosesPrimaryMonitorInStackedLayout()
    {
        var monitorArea = MainWindow.FindWindowsMonitorAreaForWindowBounds(
            new PixelRect(120, 10, 1200, 700),
            CreateStackedWindowsMonitorAreas());

        Assert.True(monitorArea.HasValue);
        Assert.Equal(new PixelRect(0, 0, 1920, 1080), monitorArea.Value.MonitorBounds);
        Assert.Equal(new PixelRect(0, 0, 1920, 1032), monitorArea.Value.WorkingArea);
    }

    [Fact]
    public void FindWindowsMonitorAreaForWindowBoundsChoosesTopMonitorInStackedLayout()
    {
        var monitorArea = MainWindow.FindWindowsMonitorAreaForWindowBounds(
            new PixelRect(320, -960, 1000, 700),
            CreateStackedWindowsMonitorAreas());

        Assert.True(monitorArea.HasValue);
        Assert.Equal(new PixelRect(0, -1080, 1920, 1080), monitorArea.Value.MonitorBounds);
        Assert.Equal(new PixelRect(0, -1080, 1920, 1032), monitorArea.Value.WorkingArea);
    }

    [Fact]
    public void FindWindowsMonitorAreaForWindowBoundsChoosesPortraitMonitorInStackedLayout()
    {
        var monitorArea = MainWindow.FindWindowsMonitorAreaForWindowBounds(
            new PixelRect(-1020, -1200, 900, 900),
            CreateStackedWindowsMonitorAreas());

        Assert.True(monitorArea.HasValue);
        Assert.Equal(new PixelRect(-1080, -1588, 1080, 1920), monitorArea.Value.MonitorBounds);
        Assert.Equal(new PixelRect(-1080, -1588, 1080, 1872), monitorArea.Value.WorkingArea);
    }

    [Fact]
    public void FindWindowsMonitorAreaForWindowBoundsChoosesLargestIntersectionNearSharedEdge()
    {
        var monitorArea = MainWindow.FindWindowsMonitorAreaForWindowBounds(
            new PixelRect(100, -120, 1000, 500),
            CreateStackedWindowsMonitorAreas());

        Assert.True(monitorArea.HasValue);
        Assert.Equal(new PixelRect(0, 0, 1920, 1080), monitorArea.Value.MonitorBounds);
    }

    [Fact]
    public void FindWindowsMonitorAreaForWorkingBoundsMatchesAeroSnapOnPrimaryMonitor()
    {
        var monitorArea = MainWindow.FindWindowsMonitorAreaForWorkingBounds(
            new PixelRect(-1, 1, 1921, 1031),
            CreateStackedWindowsMonitorAreas(),
            tolerancePixels: 8);

        Assert.True(monitorArea.HasValue);
        Assert.Equal(new PixelRect(0, 0, 1920, 1032), monitorArea.Value.WorkingArea);
    }

    [Fact]
    public void FindWindowsMonitorAreaForWorkingBoundsMatchesAeroSnapOnTopMonitor()
    {
        var monitorArea = MainWindow.FindWindowsMonitorAreaForWorkingBounds(
            new PixelRect(0, -1080, 1920, 1032),
            CreateStackedWindowsMonitorAreas(),
            tolerancePixels: 8);

        Assert.True(monitorArea.HasValue);
        Assert.Equal(new PixelRect(0, -1080, 1920, 1032), monitorArea.Value.WorkingArea);
    }

    [Fact]
    public void CalculateWindowsDragRestoreBoundsKeepsCursorRatioWithinRestoredWindow()
    {
        var normalPlacement = new WindowPlacement(120, 80, 1000, 700, IsMaximized: false);
        var maximizedBounds = new PixelRect(0, 0, 1920, 1032);
        var cursorPoint = new MainWindow.POINT { X = 1440, Y = 18 };

        var bounds = MainWindow.CalculateWindowsDragRestoreBounds(
            normalPlacement,
            maximizedBounds,
            cursorPoint,
            renderScaling: 1,
            minWidth: 640,
            minHeight: 480);

        Assert.Equal(1000, bounds.Width);
        Assert.Equal(700, bounds.Height);
        Assert.Equal(690, bounds.X);
        Assert.Equal(0, bounds.Y);
    }

    [Fact]
    public void IsPixelRectCloseToAllowsSmallWindowFrameDifferences()
    {
        var actual = new PixelRect(-1, 1, 1921, 1031);
        var expected = new PixelRect(0, 0, 1920, 1032);

        Assert.True(MainWindow.IsPixelRectCloseTo(actual, expected, tolerancePixels: 2));
    }

    [Fact]
    public void IsPixelRectCloseToRejectsDifferentMonitorBounds()
    {
        var actual = new PixelRect(0, -1080, 1920, 1032);
        var expected = new PixelRect(0, 0, 1920, 1032);

        Assert.False(MainWindow.IsPixelRectCloseTo(actual, expected, tolerancePixels: 8));
    }

    private static MainWindow.WindowsMonitorArea[] CreateStackedWindowsMonitorAreas()
        => new[]
        {
            CreateWindowsMonitorArea(
                monitorBounds: new PixelRect(0, 0, 1920, 1080),
                workingArea: new PixelRect(0, 0, 1920, 1032)),
            CreateWindowsMonitorArea(
                monitorBounds: new PixelRect(-1080, -1588, 1080, 1920),
                workingArea: new PixelRect(-1080, -1588, 1080, 1872)),
            CreateWindowsMonitorArea(
                monitorBounds: new PixelRect(0, -1080, 1920, 1080),
                workingArea: new PixelRect(0, -1080, 1920, 1032))
        };

    private static MainWindow.WindowsMonitorArea CreateWindowsMonitorArea(
        PixelRect monitorBounds,
        PixelRect workingArea)
        => new(monitorBounds, workingArea, Scaling: 1);
}
