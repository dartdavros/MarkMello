using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Размещение панели оглавления (ADR-0008): ширина колонок, перетаскивание границ
/// и связь панели с активным хостом навигации (reader или editor).
/// </summary>
public partial class MainWindow
{
    // Правая колонка живёт в SidebarLayout (колонки: 0 файлы, 1 граница, 2 документ, 3 граница, 4 оглавление),
    // левая вложена в колонку документа (OutlineLeftLayout: 0 оглавление, 1 граница, 2 документ),
    // чтобы граница файлового сайдбара по-прежнему соседствовала с колонкой документа.
    private const int OutlineRightColumn = 4;
    private const int OutlineLeftColumn = 0;
    private const int OutlineDocumentColumn = 2;
    private const int SidebarDocumentColumn = 2;

    /// <summary>Пока оглавление видно, колонка документа не уже этого значения (решение владельца, ADR-0008).</summary>
    private const double DocumentMinWidth = 320;

    private OutlineViewModel? _attachedOutline;
    private IOutlineNavigationHost? _outlineHost;
    private bool _outlineHostRefreshScheduled;

    private void SyncOutlineColumns()
    {
        if (SidebarLayout is not { ColumnDefinitions: { Count: > OutlineRightColumn } columns }
            || OutlineLeftLayout is not { ColumnDefinitions: { Count: > OutlineLeftColumn } leftColumns })
        {
            return;
        }

        var preferences = _viewModel.OutlinePreferences;
        var leftVisible = _viewModel.OutlineLeftContent is not null;
        var rightVisible = _viewModel.OutlineRightContent is not null;
        SetOutlineColumn(leftColumns[OutlineLeftColumn], leftVisible, preferences.Width);
        SetOutlineColumn(columns[OutlineRightColumn], rightVisible, preferences.Width);

        // Охрана от схлопывания документа: обе границы файлов и правого оглавления двигают
        // верхнюю колонку 2, поэтому ей нужен запас под левое оглавление плюс документ.
        if (leftColumns.Count > OutlineDocumentColumn && columns.Count > SidebarDocumentColumn)
        {
            leftColumns[OutlineDocumentColumn].MinWidth = leftVisible ? DocumentMinWidth : 0;
            columns[SidebarDocumentColumn].MinWidth = leftVisible || rightVisible
                ? DocumentMinWidth + (leftVisible ? OutlinePreferences.NormalizeWidth(preferences.Width) : 0)
                : 0;
        }
        AttachOutline(_viewModel.Outline);
    }

    private static void SetOutlineColumn(ColumnDefinition column, bool visible, double width)
    {
        column.MinWidth = visible ? OutlinePreferences.MinWidth : 0;
        column.MaxWidth = OutlinePreferences.MaxWidth;
        column.Width = visible ? new GridLength(OutlinePreferences.NormalizeWidth(width)) : new GridLength(0);
    }

    private void OnOutlineSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        if (SidebarLayout is not { ColumnDefinitions: { Count: > OutlineRightColumn } columns }
            || OutlineLeftLayout is not { ColumnDefinitions: { Count: > OutlineLeftColumn } leftColumns })
        {
            return;
        }

        var column = ReferenceEquals(sender, OutlineRightSplitter)
            ? columns[OutlineRightColumn]
            : leftColumns[OutlineLeftColumn];
        _viewModel.SetOutlineWidth(column.Width.Value);
    }

    private void AttachOutline(OutlineViewModel? outline)
    {
        if (ReferenceEquals(outline, _attachedOutline))
        {
            return;
        }

        if (_attachedOutline is not null)
        {
            _attachedOutline.NavigateRequested -= OnOutlineNavigateRequested;
        }

        _attachedOutline = outline;
        if (outline is null)
        {
            DetachOutlineHost();
            return;
        }

        outline.NavigateRequested += OnOutlineNavigateRequested;
        ScheduleOutlineHostRefresh();
    }

    private void OnOutlineNavigateRequested(object? sender, int sourceLine)
    {
        var host = ResolveOutlineHost();
        if (host is null)
        {
            return;
        }

        host.TryNavigateToSourceLine(sourceLine);
        _attachedOutline?.SetCurrentSourceLine(host.CurrentSourceLine);
    }

    /// <summary>
    /// Хост меняется при переключении viewer и editor; шаблон нового контента создаётся
    /// позже смены свойства, поэтому поиск откладывается до следующего прохода диспетчера.
    /// </summary>
    private void ScheduleOutlineHostRefresh()
    {
        if (_attachedOutline is null || _outlineHostRefreshScheduled)
        {
            return;
        }

        _outlineHostRefreshScheduled = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _outlineHostRefreshScheduled = false;
                DetachOutlineHost();
                if (_attachedOutline is not null && ResolveOutlineHost() is { } host)
                {
                    _attachedOutline.SetCurrentSourceLine(host.CurrentSourceLine);
                }
            },
            DispatcherPriority.Loaded);
    }

    private IOutlineNavigationHost? ResolveOutlineHost()
    {
        if (_outlineHost is Visual cached && cached.IsAttachedToVisualTree())
        {
            return _outlineHost;
        }

        DetachOutlineHost();

        _outlineHost = this.FindControl<Panel>("BodyPanel")?
            .GetVisualDescendants()
            .OfType<IOutlineNavigationHost>()
            .FirstOrDefault();
        if (_outlineHost is not null)
        {
            _outlineHost.CurrentSourceLineChanged += OnOutlineHostLineChanged;
        }

        return _outlineHost;
    }

    private void DetachOutlineHost()
    {
        if (_outlineHost is null)
        {
            return;
        }

        _outlineHost.CurrentSourceLineChanged -= OnOutlineHostLineChanged;
        _outlineHost = null;
    }

    private void OnOutlineHostLineChanged(object? sender, EventArgs e)
        => _attachedOutline?.SetCurrentSourceLine(_outlineHost?.CurrentSourceLine);
}
