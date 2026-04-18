using System.Text;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using MarkMello.Application.Abstractions;

namespace MarkMello.Presentation.Views.Markdown;

internal static class MarkdownImageLoader
{
    public static async Task<(IImage Image, Stream BackingStream)?> TryLoadAsync(
        IImageSourceResolver? resolver,
        string url,
        string? baseDirectory,
        CancellationToken cancellationToken)
    {
        if (resolver is null || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        await using var stream = await resolver
            .TryOpenAsync(url, baseDirectory, cancellationToken)
            .ConfigureAwait(false);

        if (stream is null)
        {
            return null;
        }

        var imageBytes = await ReadAllBytesAsync(stream, cancellationToken).ConfigureAwait(false);
        if (imageBytes.Length == 0)
        {
            return null;
        }

        try
        {
            return await Dispatcher.UIThread.InvokeAsync(() => LoadImage(imageBytes));
        }
        catch
        {
            return null;
        }
    }

    public static void DisposeLoadedImage(IImage? image, Stream? backingStream)
    {
        if (image is IDisposable disposableImage)
        {
            disposableImage.Dispose();
        }

        backingStream?.Dispose();
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            if (buffer.Array is null)
            {
                return memoryStream.ToArray();
            }

            var length = checked((int)memoryStream.Length);
            return new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, length).ToArray();
        }

        await using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return copy.ToArray();
    }

    private static (IImage Image, Stream BackingStream) LoadImage(byte[] imageBytes)
    {
        var stream = new MemoryStream(imageBytes, writable: false);
        try
        {
            if (IsSvgStream(stream))
            {
                return (
                    new SvgImage
                    {
                        Source = SvgSource.LoadFromStream(stream)
                    },
                    stream);
            }

            return (new Bitmap(stream), stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static bool IsSvgStream(Stream stream)
    {
        if (!stream.CanRead)
        {
            return false;
        }

        long? restorePosition = null;
        if (stream.CanSeek)
        {
            restorePosition = stream.Position;
            stream.Position = 0;
        }

        try
        {
            var buffer = new byte[512];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
            {
                return false;
            }

            var header = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            return header.Contains("<svg", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (restorePosition is long position)
            {
                stream.Position = position;
            }
        }
    }
}
