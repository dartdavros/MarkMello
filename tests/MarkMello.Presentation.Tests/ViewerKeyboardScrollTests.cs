using Avalonia.Input;
using MarkMello.Presentation.Views;

namespace MarkMello.Presentation.Tests;

public sealed class ViewerKeyboardScrollTests
{
    [Theory]
    [InlineData(Key.Down, KeyModifiers.None, 100, 1000, 40, 600, 140)]
    [InlineData(Key.Up, KeyModifiers.None, 100, 1000, 40, 600, 60)]
    [InlineData(Key.PageDown, KeyModifiers.None, 100, 1000, 40, 600, 652)]
    [InlineData(Key.PageUp, KeyModifiers.None, 600, 1000, 40, 600, 48)]
    [InlineData(Key.Home, KeyModifiers.None, 600, 1000, 40, 600, 0)]
    [InlineData(Key.End, KeyModifiers.None, 600, 1000, 40, 600, 1000)]
    [InlineData(Key.Space, KeyModifiers.None, 100, 1000, 40, 600, 652)]
    [InlineData(Key.Space, KeyModifiers.Shift, 600, 1000, 40, 600, 48)]
    public void GetKeyboardScrollOffsetReturnsExpectedOffset(
        Key key,
        KeyModifiers modifiers,
        double current,
        double maximum,
        double smallChange,
        double viewport,
        double expected)
    {
        var offset = ViewerView.GetKeyboardScrollOffset(key, modifiers, current, maximum, smallChange, viewport);

        Assert.Equal(expected, offset);
    }

    [Fact]
    public void GetKeyboardScrollOffsetIgnoresNonScrollKey()
    {
        var offset = ViewerView.GetKeyboardScrollOffset(Key.A, KeyModifiers.None, 100, 1000, 40, 600);

        Assert.Null(offset);
    }
}
