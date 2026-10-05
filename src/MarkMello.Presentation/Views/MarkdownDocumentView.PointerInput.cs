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
    private void RegisterSelectionFragment(MarkdownDocumentSelectionFragmentBase fragment, string path)
    {
        _selectionFragments.Add(fragment);
        _selectionFragmentPaths.Add(path);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsPointerInputFromScrollBarChrome(e.Source))
        {
            return;
        }

        if (IsPointerInputFromCodeCopyButton(e.Source))
        {
            return;
        }

        if (_quickInlineEditingController.HasActiveEditor)
        {
            if (_quickInlineEditingController.IsPointerOverActiveEditor(e.Source, e.GetPosition(this)))
            {
                return;
            }

            _quickInlineEditingController.CommitQuickEditor();
        }

        var currentPoint = e.GetCurrentPoint(this);
        if (currentPoint.Properties.IsRightButtonPressed)
        {
            _contextMenuLink = TryResolveLinkAtDocumentPoint(e.GetPosition(this), out var link)
                ? link
                : null;
            return;
        }

        if (!TryResolveFragment(e.GetPosition(this), out var fragment, out var localPosition))
        {
            return;
        }

        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Focus via NavigationMethod.Pointer so the act of starting a selection
        // does not raise RequestBringIntoView and make the ScrollViewer jump.
        Focus(NavigationMethod.Pointer);

        CancelPendingQuickEditTimer();

        if (e.ClickCount >= 3)
        {
            CommitSelection(fragment.DocumentRange, preserveOnRelease: true);
            BeginPointerSession(e, fragment, localPosition, allowLinkActivation: false);
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            var wordRange = fragment.GetDocumentWordRange(localPosition);
            if (!wordRange.IsEmpty)
            {
                CommitSelection(wordRange, preserveOnRelease: true);
                BeginPointerSession(e, fragment, localPosition, allowLinkActivation: false);
                e.Handled = true;
                return;
            }
        }

        _isPointerPressed = true;
        _isDraggingSelection = false;
        _preserveSelectionOnRelease = false;
        _pointerPressOrigin = e.GetPosition(this);
        _pressedLocalPosition = localPosition;
        _pressedFragment = fragment;
        _pressedLink = fragment.TryGetLinkAt(localPosition, out var pressedLink)
            ? pressedLink
            : null;

        var anchor = fragment.GetDocumentOffset(localPosition);
        SelectionAnchor = anchor;
        SelectionStart = anchor;
        SelectionEnd = anchor;
        ApplySelectionToFragments();

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerPressed || SelectionAnchor is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (!_isDraggingSelection && Point.Distance(position, _pointerPressOrigin) < DragSelectionThreshold)
        {
            return;
        }

        CancelPendingQuickEditTimer();
        _isDraggingSelection = true;
        var offset = ResolveDocumentOffset(position);
        SetSelection(SelectionAnchor.Value, offset);
        e.Handled = true;
    }

    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerPressed)
        {
            return;
        }

        var shouldTryQuickEdit = !_isDraggingSelection
            && !_quickInlineEditingController.HasActiveEditor
            && _pressedLink is null
            && _pressedFragment is not null
            && !string.IsNullOrEmpty(SourceText)
            && e.InitialPressMouseButton == MouseButton.Left;

        await TryActivatePressedLinkAsync(e);

        if (!_isDraggingSelection && !_preserveSelectionOnRelease)
        {
            ClearSelection();
        }

        var fragmentToEdit = _pressedFragment;
        var localPosToEdit = _pressedLocalPosition;

        ResetPointerState();
        e.Pointer.Capture(null);
        e.Handled = true;

        if (shouldTryQuickEdit && fragmentToEdit is not null)
        {
            ScheduleQuickEdit(fragmentToEdit, localPosToEdit);
        }
    }

    private void ScheduleQuickEdit(MarkdownDocumentSelectionFragmentBase fragment, Point localPosition)
    {
        CancelPendingQuickEditTimer();

        _pendingQuickEditAction = () =>
        {
            if (!_quickInlineEditingController.HasActiveEditor && !HasSelection)
            {
                _quickInlineEditingController.BeginQuickEdit(fragment, localPosition, _sourceLineAnchors, _root);
            }
        };

        _pendingQuickEditTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _pendingQuickEditTimer.Tick += (_, _) =>
        {
            CancelPendingQuickEditTimer();
            _pendingQuickEditAction?.Invoke();
            _pendingQuickEditAction = null;
        };
        _pendingQuickEditTimer.Start();
    }

    private void CancelPendingQuickEditTimer()
    {
        if (_pendingQuickEditTimer is not null)
        {
            _pendingQuickEditTimer.Stop();
            _pendingQuickEditTimer = null;
        }
        _pendingQuickEditAction = null;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ResetPointerState();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && HasSelection)
        {
            ClearSelection();
            e.Handled = true;
            return;
        }

        if (!HasCommandModifier(e.KeyModifiers))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.A:
                SelectAll();
                e.Handled = true;
                break;

            case Key.C:
                if (HasSelection)
                {
                    await CopySelectionToClipboardAsync();
                    e.Handled = true;
                }
                break;
        }
    }
}
