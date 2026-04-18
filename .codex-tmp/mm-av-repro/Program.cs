using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MarkMello.Presentation;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .SetupWithoutStarting();

var path = @"C:\Users\drmar\AppData\Local\Temp\mm-landscape-test.jpg";
using var fs = File.OpenRead(path);
var bmp = new Bitmap(fs);

Console.WriteLine($"Natural bitmap size: {bmp.Size}");

var image = new Image
{
    Source = bmp,
    Stretch = Stretch.Uniform,
    StretchDirection = StretchDirection.DownOnly,
    Width = 1920
};

image.Measure(new Size(656, double.PositiveInfinity));
Console.WriteLine($"Image DesiredSize after measure(656, inf): {image.DesiredSize}");

var caption = new TextBlock
{
    Text = "Moon",
    FontSize = 12,
    Margin = new Thickness(0, 8, 0, 0)
};

var stack = new StackPanel
{
    Orientation = Orientation.Vertical,
    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
    Children = { image, caption }
};

var host = new ContentControl
{
    Content = stack,
    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
    MaxWidth = 1200,
};

var readerBorder = new Border
{
    MaxWidth = 800,
    Padding = new Thickness(72,96,72,160),
    Child = host,
};

readerBorder.Measure(new Size(1000, double.PositiveInfinity));
Console.WriteLine($"Reader Border DesiredSize: {readerBorder.DesiredSize}");
Console.WriteLine($"Host DesiredSize: {host.DesiredSize}");
Console.WriteLine($"Stack DesiredSize: {stack.DesiredSize}");
Console.WriteLine($"Image DesiredSize inside stack: {image.DesiredSize}");
