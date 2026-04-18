using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MarkMello.Application.Abstractions;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Block-level image view. Loads the image lazily when attached to the visual
/// tree (so opening a markdown file with many images does not block the viewer
/// path). Shows a placeholder block with the alt text if the image cannot be
/// loaded -- see constitution §6 "fast path must stay simple" and the viewer
/// error-handling rules in architecture.md.
///
/// The control is intentionally NOT part of the document text map: image
/// rendering lives outside MarkdownSelectionTextFragment's text-flow model
/// (ADR-0001). Selection/copy skips images.
/// </summary>
internal sealed class MarkdownImageView : ContentControl, IDisposable
{
    private readonly IImageSourceResolver? _resolver;
    private readonly string _url;
    private readonly string? _altText;
    private readonly double? _width;
    private readonly double? _height;
    private readonly string? _baseDirectory;
    private readonly CancellationTokenSource _cts = new();
    private IImage? _loadedImage;
    private Stream? _loadedImageBackingStream;
    private Image? _imageControl;
    private bool _loadStarted;
    private bool _loadCompleted;
    private bool _disposed;

    public MarkdownImageView(
        IImageSourceResolver? resolver,
        string url,
        string? altText,
        string? title,
        double? width,
        double? height,
        string? baseDirectory)
    {
        _resolver = resolver;
        _url = url ?? string.Empty;
        _altText = altText;
        _width = width;
        _height = height;
        _baseDirectory = baseDirectory;

        HorizontalAlignment = HorizontalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        UseLayoutRounding = true;

        // Render a quiet placeholder first; the actual image (or a failure
        // placeholder) replaces it when loading completes.
        Content = BuildLoadingPlaceholder();

        ToolTip.SetTip(this, string.IsNullOrWhiteSpace(title) ? altText : title);

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_loadStarted)
        {
            return;
        }

        _loadStarted = true;
        _ = LoadAsync(_cts.Token);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
        MarkdownImageLoader.DisposeLoadedImage(_loadedImage, _loadedImageBackingStream);
        _loadedImage = null;
        _loadedImageBackingStream = null;
        _imageControl = null;

        AttachedToVisualTree -= OnAttachedToVisualTree;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        ApplyImageConstraints(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_resolver is null || string.IsNullOrWhiteSpace(_url))
        {
            ShowFailurePlaceholder();
            return;
        }

        try
        {
            var loaded = await MarkdownImageLoader
                .TryLoadAsync(_resolver, _url, _baseDirectory, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (loaded is null)
            {
                await Dispatcher.UIThread.InvokeAsync(ShowFailurePlaceholder);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => ShowImage(loaded.Value.Image, loaded.Value.BackingStream));
        }
        catch (OperationCanceledException)
        {
            // Detached from the tree mid-flight; nothing to do.
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(ShowFailurePlaceholder);
        }
    }

    private void ShowImage(IImage imageSource, Stream backingStream)
    {
        if (_loadCompleted)
        {
            MarkdownImageLoader.DisposeLoadedImage(imageSource, backingStream);
            return;
        }

        _loadedImage = imageSource;
        _loadedImageBackingStream = backingStream;
        _loadCompleted = true;

        var image = new Image
        {
            Source = imageSource,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            UseLayoutRounding = true,
        };

        _imageControl = image;
        ApplyImageConstraints(Bounds.Width);

        if (string.IsNullOrWhiteSpace(_altText))
        {
            Content = image;
            return;
        }

        // Caption below the image, in the "soft text" style -- mirrors the
        // "figure > figcaption" convention for markdown image blocks.
        var caption = new TextBlock
        {
            Text = _altText,
            FontSize = 12,
            Opacity = 0.75,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        caption.Classes.Add("mm-md-image-caption");

        Content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { image, caption }
        };
    }

    private void ShowFailurePlaceholder()
    {
        if (_loadCompleted)
        {
            return;
        }
        _loadCompleted = true;
        _imageControl = null;

        var label = string.IsNullOrWhiteSpace(_altText)
            ? "Image unavailable"
            : $"Image unavailable — {_altText}";

        var border = new Border
        {
            Padding = new Thickness(16, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                Opacity = 0.7,
            }
        };
        border.Classes.Add("mm-md-image-placeholder");
        Content = border;
    }

    private Border BuildLoadingPlaceholder()
    {
        var border = new Border
        {
            Padding = new Thickness(16, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_altText) ? "Loading image…" : _altText,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                Opacity = 0.55,
            }
        };
        border.Classes.Add("mm-md-image-placeholder");
        return border;
    }

    private void ApplyImageConstraints(double availableWidth)
    {
        if (_imageControl is null)
        {
            return;
        }

        _imageControl.Width = double.NaN;
        _imageControl.Height = double.NaN;
        _imageControl.MaxWidth = ResolveMaxWidth(_width, availableWidth);
        _imageControl.MaxHeight = ResolveMaxHeight(_height);
    }

    internal static double ResolveMaxWidth(double? requestedWidth, double availableWidth)
    {
        var normalizedAvailableWidth = double.IsFinite(availableWidth) && availableWidth > 0
            ? availableWidth
            : double.PositiveInfinity;

        if (requestedWidth is > 0)
        {
            return Math.Min(requestedWidth.Value, normalizedAvailableWidth);
        }

        return normalizedAvailableWidth;
    }

    internal static double ResolveMaxHeight(double? requestedHeight)
        => requestedHeight is > 0
            ? requestedHeight.Value
            : double.PositiveInfinity;
}
