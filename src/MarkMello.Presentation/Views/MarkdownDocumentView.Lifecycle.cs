using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Presentation.Clipboard;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.Views.Markdown;
using MarkMello.Presentation.Views.Markdown.Minimap;
using System.Globalization;
using System.Text;
using System.Threading;

namespace MarkMello.Presentation.Views;


public sealed partial class MarkdownDocumentView
{
    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        EnsureRootTransitions();
        EnsureContextMenu();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        CancelPendingQuickEditTimer();
        _quickInlineEditingController.CommitQuickEditor();

        LayoutUpdated -= OnLayoutUpdatedAfterDocumentRebuild;
        _hasPendingRenderedNotification = false;
        _readingPreferencesRefreshCts?.Cancel();
        _readingPreferencesRefreshCts?.Dispose();
        _readingPreferencesRefreshCts = null;
    }

    private void EnsureRootTransitions()
    {
        if (_root.Transitions is not null || !Dispatcher.UIThread.CheckAccess())
        {
            return;
        }

        _root.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(140),
                Easing = new CubicEaseOut()
            }
        ];
    }

    private void EnsureContextMenu()
    {
        if (ContextMenu is not null || !Dispatcher.UIThread.CheckAccess())
        {
            return;
        }

        ContextMenu = BuildContextMenu();
    }

    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // If the request originates on this control itself (e.g. from focus
        // change during a selection gesture), there is nothing to bring into
        // view -- the document already is the scroll content. Allowing it to
        // bubble causes the ScrollViewer to jump to the top of our bounds.
        if (ReferenceEquals(e.TargetObject, this))
        {
            e.Handled = true;
        }
    }

    private void QueueDocumentRenderedNotification(long generation)
    {
        _hasPendingRenderedNotification = true;
        LayoutUpdated -= OnLayoutUpdatedAfterDocumentRebuild;
        LayoutUpdated += OnLayoutUpdatedAfterDocumentRebuild;

        Dispatcher.UIThread.Post(
            () => CompleteDocumentRenderedNotification(generation),
            DispatcherPriority.Render);
    }

    private void OnLayoutUpdatedAfterDocumentRebuild(object? sender, EventArgs e)
        => CompleteDocumentRenderedNotification(_renderGeneration);

    private void CompleteDocumentRenderedNotification(long generation)
    {
        if (!_hasPendingRenderedNotification || generation != _renderGeneration || Document is null)
        {
            return;
        }

        _hasPendingRenderedNotification = false;
        LayoutUpdated -= OnLayoutUpdatedAfterDocumentRebuild;
        DocumentRendered?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshForReadingPreferencesChange()
    {
        DocumentRenderInvalidated?.Invoke(this, EventArgs.Empty);
        _readingPreferencesRefreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _readingPreferencesRefreshCts = cts;

        _root.Opacity = 0.9;
        _ = AnimateReadingPreferencesRefreshAsync(cts.Token);
    }

    private async Task AnimateReadingPreferencesRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(48, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        RebuildFromScratch();
        _root.Opacity = 1;
    }

    /// <summary>
    /// Полная пересборка без переиспользования. Нужна там, где изменился не сам
    /// документ, а способ его отрисовки (шрифты reading preferences, resolver
    /// изображений): содержимое блоков осталось прежним, но готовые контролы
    /// уже не соответствуют новым настройкам.
    /// </summary>
    private void RebuildFromScratch()
    {
        DisposeSelectionFragments();
        _root.Children.Clear();
        Rebuild();
    }

    private void ApplyDocumentPadding()
    {
        _viewport.Padding = DocumentPadding;
    }

    private void DisposeSelectionFragments()
    {
        foreach (var fragment in _selectionFragments)
        {
            fragment.Dispose();
        }

        _selectionFragments.Clear();
        _selectionFragmentPaths.Clear();
        _builtBlocks = [];
    }
}
