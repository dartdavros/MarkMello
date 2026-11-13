using Avalonia;
using Avalonia.Controls;
using MarkMello.Domain;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

public sealed class MainWindowPlacementTests
{
    [Fact]
    public void CalculateTitleBarContentMarginMatchesTheSharedContentInsetOutsideMacOS()
    {
        var margin = MainWindow.CalculateTitleBarContentMargin(isMacOS: false);

        Assert.Equal(new Thickness(16, 0), margin);
    }

    [Fact]
    public void CalculateTitleBarContentMarginReservesMacOSWindowButtonsArea()
    {
        var margin = MainWindow.CalculateTitleBarContentMargin(isMacOS: true);

        Assert.Equal(new Thickness(82, 0, 16, 0), margin);
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

    /// <summary>
    /// Свёрнутое окно возвращается в то состояние, из которого его свернули. Закрытое
    /// из панели задач в свёрнутом виде, оно должно и при следующем запуске открыться так же.
    /// </summary>
    [Theory]
    [InlineData(new[] { WindowState.Maximized, WindowState.Minimized }, true)]
    [InlineData(new[] { WindowState.Minimized }, false)]
    [InlineData(new[] { WindowState.Maximized, WindowState.Normal, WindowState.Minimized }, false)]
    [InlineData(new[] { WindowState.Maximized, WindowState.Minimized, WindowState.Maximized }, true)]
    [InlineData(new[] { WindowState.Maximized, WindowState.Minimized, WindowState.Maximized, WindowState.Normal, WindowState.Minimized }, false)]
    [InlineData(new[] { WindowState.Minimized, WindowState.Normal, WindowState.Maximized, WindowState.Minimized }, true)]
    public void WindowPlacementForPersistenceKeepsTheStateAMinimizedWindowRestoresTo(
        WindowState[] transitions,
        bool expectedMaximized)
    {
        var normalPlacement = new WindowPlacement(120, 80, 900, 700, IsMaximized: false);
        // Свёрнутое окно Windows уводит за пределы экрана: сохранять эти границы нельзя.
        var minimizedPlacement = new WindowPlacement(-32000, -32000, 160, 28, IsMaximized: false);
        var state = WindowState.Normal;
        var restoresToMaximized = false;

        foreach (var next in transitions)
        {
            restoresToMaximized = MainWindow.ResolveRestoresToMaximized(state, next, restoresToMaximized);
            state = next;
        }

        var placement = MainWindow.ResolveWindowPlacementForPersistence(
            state,
            restoresToMaximized,
            normalPlacement,
            minimizedPlacement);

        Assert.Equal(normalPlacement with { IsMaximized = expectedMaximized }, placement);
    }

    /// <summary>
    /// Ширина сайдбара принадлежит колонке: сплиттер двигает именно её, а фиксированная
    /// ширина у самого сайдбара оставляла рядом пустую полосу.
    /// </summary>
    [Theory]
    [InlineData(260d, 260d)]
    [InlineData(120d, 220d)]
    [InlineData(900d, 340d)]
    public void VisibleSidebarColumnKeepsTheWidthWithinTheDesignRange(double stored, double expected)
    {
        var (minWidth, width) = MainWindow.CalculateSidebarColumn(showsSidebar: true, stored);

        Assert.Equal(220d, minWidth);
        Assert.Equal(expected, width.Value);
    }

    [Fact]
    public void HiddenSidebarCollapsesTheColumnCompletely()
    {
        var (minWidth, width) = MainWindow.CalculateSidebarColumn(showsSidebar: false, sidebarWidth: 300);

        // Минимум тоже снимается: иначе от скрытого сайдбара осталась бы полоса в 220.
        Assert.Equal(0d, minWidth);
        Assert.Equal(0d, width.Value);
    }
}
