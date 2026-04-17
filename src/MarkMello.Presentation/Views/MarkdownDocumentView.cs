using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Native Markdown renderer для viewer mode.
/// В этой итерации переносит selection ownership на document level и покрывает:
/// headings, paragraphs, quote paragraph content, list paragraph content,
/// code blocks и table cells.
/// </summary>
public sealed class MarkdownDocumentView : UserControl
{
    public static readonly StyledProperty<RenderedMarkdownDocument?> DocumentProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, RenderedMarkdownDocument?>(nameof(Document));

    public static readonly StyledProperty<ReadingPreferences> ReadingPreferencesProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, ReadingPreferences>(
            nameof(ReadingPreferences),
            ReadingPreferences.Default);

    private const double DragSelectionThreshold = 4;
    private static readonly Cursor IBeamCursor = new(StandardCursorType.Ibeam);

    private readonly StackPanel _root = new()
    {
        Orientation = Orientation.Vertical,
        Spacing = 0
    };

    private readonly List<MarkdownSelectionTextFragment> _selectionFragments = [];
    private MarkdownDocumentTextMap _textMap = MarkdownDocumentTextMap.Empty;
    private bool _isPointerPressed;
    private bool _isDraggingSelection;
    private Point _pointerPressOrigin;
    private MarkdownSelectionTextFragment? _pressedFragment;
    private MarkdownLinkSpan? _pressedLink;

    static MarkdownDocumentView()
    {
        DocumentProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.Rebuild());
        ReadingPreferencesProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.Rebuild());
    }

    public MarkdownDocumentView()
    {
        Focusable = true;
        UseLayoutRounding = true;
        _root.UseLayoutRounding = true;

        KeyDown += OnKeyDown;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;

        Content = _root;
    }

    public RenderedMarkdownDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public ReadingPreferences ReadingPreferences
    {
        get => GetValue(ReadingPreferencesProperty);
        set => SetValue(ReadingPreferencesProperty, value);
    }

    public int? SelectionAnchor { get; private set; }

    public int SelectionStart { get; private set; }

    public int SelectionEnd { get; private set; }

    public bool HasSelection => SelectionEnd > SelectionStart;

    public string SelectedText => HasSelection
        ? _textMap.GetText(new DocumentTextRange(SelectionStart, SelectionEnd))
        : string.Empty;

    public void SelectAll()
    {
        if (_textMap.Text.Length == 0)
        {
            ClearSelection();
            return;
        }

        SelectionAnchor = 0;
        SelectionStart = 0;
        SelectionEnd = _textMap.Text.Length;
        ApplySelectionToFragments();
    }

    public void ClearSelection()
    {
        SelectionAnchor = null;
        SelectionStart = 0;
        SelectionEnd = 0;
        ApplySelectionToFragments();
    }

    private void Rebuild()
    {
        DisposeSelectionFragments();
        _root.Children.Clear();
        ResetPointerState();

        var document = Document;
        _textMap = document is null ? MarkdownDocumentTextMap.Empty : MarkdownDocumentTextMap.Create(document);
        ClearSelection();

        if (document is null || document.Blocks.Count == 0)
        {
            return;
        }

        for (var index = 0; index < document.Blocks.Count; index++)
        {
            _root.Children.Add(BuildBlock(document.Blocks[index], $"b{index}", nested: false));
        }
    }

    private void DisposeSelectionFragments()
    {
        foreach (var fragment in _selectionFragments)
        {
            fragment.PointerPressed -= OnFragmentPointerPressed;
            fragment.Dispose();
        }

        _selectionFragments.Clear();
    }

    private Control BuildBlock(MarkdownBlock block, string path, bool nested)
        => block switch
        {
            MarkdownHeadingBlock heading => BuildHeading(heading, path),
            MarkdownParagraphBlock paragraph => BuildParagraph(paragraph, path, nested),
            MarkdownQuoteBlock quote => BuildQuote(quote, path),
            MarkdownListBlock list => BuildList(list, path),
            MarkdownHorizontalRuleBlock => BuildHorizontalRule(),
            MarkdownCodeBlock code => BuildCodeBlock(code, path),
            MarkdownTableBlock table => BuildTable(table, path),
            _ => BuildFallback(block)
        };

    private Control BuildHeading(MarkdownHeadingBlock block, string path)
    {
        var fontSize = GetHeadingFontSize(block.Level);
        var lineHeight = Math.Max(fontSize * 1.2, fontSize + 4);
        var margin = block.Level <= 2
            ? new Thickness(0, 0, 0, 22)
            : new Thickness(0, 10, 0, 16);
        var weight = block.Level <= 2 ? FontWeight.SemiBold : FontWeight.Bold;

        return BuildSelectionFragment(
            path,
            block.Inlines,
            margin,
            fontSize,
            lineHeight,
            weight,
            FontStyle.Normal,
            fallbackClassName: "mm-md-heading");
    }

    private Control BuildParagraph(MarkdownParagraphBlock block, string path, bool nested)
    {
        return BuildSelectionFragment(
            path,
            block.Inlines,
            nested ? new Thickness(0, 0, 0, 10) : new Thickness(0, 0, 0, 18),
            ReadingPreferences.FontSize,
            GetBodyLineHeight(),
            FontWeight.Normal,
            FontStyle.Normal,
            fallbackClassName: "mm-md-paragraph");
    }

    private Border BuildQuote(MarkdownQuoteBlock block, string path)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        for (var index = 0; index < block.Blocks.Count; index++)
        {
            stack.Children.Add(BuildBlock(block.Blocks[index], $"{path}.b{index}", nested: true));
        }

        return new Border
        {
            Classes = { "mm-md-quote" },
            Child = stack
        };
    }

    private StackPanel BuildList(MarkdownListBlock block, string path)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 18)
        };

        for (var index = 0; index < block.Items.Count; index++)
        {
            panel.Children.Add(BuildListItem(block, block.Items[index], index, $"{path}.i{index}"));
        }

        return panel;
    }

    private Grid BuildListItem(MarkdownListBlock list, MarkdownListItem item, int index, string path)
    {
        var bullet = new TextBlock
        {
            Text = list.IsOrdered ? $"{index + 1}." : "•",
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            FontSize = ReadingPreferences.FontSize,
            FontFamily = ResolveBodyFontFamily(),
            FontWeight = FontWeight.Medium,
            UseLayoutRounding = true,
            Classes = { "mm-md-list-bullet" }
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        for (var blockIndex = 0; blockIndex < item.Blocks.Count; blockIndex++)
        {
            content.Children.Add(BuildBlock(item.Blocks[blockIndex], $"{path}.b{blockIndex}", nested: true));
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new(GridLength.Auto),
                new(new GridLength(1, GridUnitType.Star))
            },
            ColumnSpacing = 12
        };

        Grid.SetColumn(bullet, 0);
        Grid.SetColumn(content, 1);
        row.Children.Add(bullet);
        row.Children.Add(content);
        return row;
    }

    private static Border BuildHorizontalRule()
        => new()
        {
            Classes = { "mm-md-hr" }
        };

    private Border BuildCodeBlock(MarkdownCodeBlock block, string path)
    {
        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8
        };

        if (!string.IsNullOrWhiteSpace(block.Info))
        {
            body.Children.Add(new TextBlock
            {
                Text = block.Info,
                UseLayoutRounding = true,
                Classes = { "mm-md-code-info" }
            });
        }

        var codeFragment = BuildSelectionFragment(
            path,
            [new MarkdownTextInline(block.Code)],
            margin: default,
            fontSize: Math.Max(12, ReadingPreferences.FontSize - 2),
            lineHeight: Math.Max(16, (ReadingPreferences.FontSize - 2) * 1.5),
            fontWeight: FontWeight.Normal,
            fontStyle: FontStyle.Normal,
            fallbackClassName: "mm-md-codeblock-text",
            baseFontFamily: ResolveMonoFontFamily(),
            textWrapping: TextWrapping.NoWrap);

        body.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = codeFragment
        });

        return new Border
        {
            Classes = { "mm-md-codeblock" },
            Child = body
        };
    }

    private Control BuildTable(MarkdownTableBlock table, string path)
    {
        var columnCount = Math.Max(
            table.Header.Count,
            table.Rows.Count == 0 ? 0 : table.Rows.Max(static row => row.Count));

        if (columnCount == 0)
        {
            return BuildFallback(table);
        }

        var grid = new Grid
        {
            ColumnSpacing = 0,
            RowSpacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        var totalRows = table.Rows.Count + (table.Header.Count > 0 ? 1 : 0);
        for (var rowIndex = 0; rowIndex < totalRows; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        var currentRow = 0;
        if (table.Header.Count > 0)
        {
            AddTableRow(grid, table.Header, currentRow, isHeader: true, pathPrefix: $"{path}.h");
            currentRow++;
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            AddTableRow(grid, table.Rows[rowIndex], currentRow, isHeader: false, pathPrefix: $"{path}.r{rowIndex}.c");
            currentRow++;
        }

        return new Border
        {
            Classes = { "mm-md-table" },
            Child = grid,
            Margin = new Thickness(0, 0, 0, 22)
        };
    }

    private void AddTableRow(Grid grid, IReadOnlyList<MarkdownTableCell> cells, int rowIndex, bool isHeader, string pathPrefix)
    {
        for (var columnIndex = 0; columnIndex < grid.ColumnDefinitions.Count; columnIndex++)
        {
            var cell = columnIndex < cells.Count
                ? cells[columnIndex]
                : new MarkdownTableCell(Array.Empty<MarkdownInline>());

            var content = BuildSelectionFragment(
                $"{pathPrefix}{columnIndex}",
                cell.Inlines,
                margin: default,
                fontSize: ReadingPreferences.FontSize,
                lineHeight: GetBodyLineHeight(),
                fontWeight: isHeader ? FontWeight.SemiBold : FontWeight.Normal,
                fontStyle: FontStyle.Normal,
                fallbackClassName: isHeader ? "mm-md-table-header" : "mm-md-table-text");

            var border = new Border
            {
                Classes = { isHeader ? "mm-md-table-header-cell" : "mm-md-table-cell" },
                Child = content
            };

            Grid.SetRow(border, rowIndex);
            Grid.SetColumn(border, columnIndex);
            grid.Children.Add(border);
        }
    }

    private TextBlock BuildFallback(MarkdownBlock block)
    {
        return new TextBlock
        {
            Text = MarkdownDocumentTextMap.ExtractPlainText(block),
            Classes = { "mm-md-paragraph" },
            FontFamily = ResolveBodyFontFamily(),
            FontSize = ReadingPreferences.FontSize,
            LineHeight = GetBodyLineHeight(),
            TextWrapping = TextWrapping.Wrap,
            UseLayoutRounding = true
        };
    }

    private Control BuildSelectionFragment(
        string path,
        IReadOnlyList<MarkdownInline> inlines,
        Thickness margin,
        double fontSize,
        double lineHeight,
        FontWeight fontWeight,
        FontStyle fontStyle,
        string fallbackClassName,
        FontFamily? baseFontFamily = null,
        TextWrapping textWrapping = TextWrapping.Wrap)
    {
        var styled = MarkdownStyledText.FromInlines(inlines);
        if (styled.Text.Length == 0)
        {
            return new Border
            {
                Height = 0,
                Margin = margin
            };
        }

        var resolvedFontFamily = baseFontFamily ?? ResolveBodyFontFamily();
        if (!_textMap.TryGetFragment(path, out var fragment))
        {
            var fallback = new TextBlock
            {
                Text = styled.Text,
                Margin = margin,
                FontFamily = resolvedFontFamily,
                FontSize = fontSize,
                FontWeight = fontWeight,
                FontStyle = fontStyle,
                LineHeight = lineHeight,
                TextWrapping = textWrapping,
                UseLayoutRounding = true,
                Classes = { fallbackClassName }
            };

            return fallback;
        }

        var control = new MarkdownSelectionTextFragment
        {
            Margin = margin,
            StyledText = styled,
            DocumentRange = fragment.Range,
            BaseFontFamily = resolvedFontFamily,
            BaseFontSize = fontSize,
            BaseFontWeight = fontWeight,
            BaseFontStyle = fontStyle,
            BaseLineHeight = lineHeight,
            LayoutTextWrapping = textWrapping,
            Cursor = IBeamCursor
        };

        control.PointerPressed += OnFragmentPointerPressed;
        control.Classes.Add(fallbackClassName);
        _selectionFragments.Add(control);
        control.SelectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
        return control;
    }

    private void OnFragmentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not MarkdownSelectionTextFragment fragment)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(this);
        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        _isPointerPressed = true;
        _isDraggingSelection = false;
        _pointerPressOrigin = e.GetPosition(this);
        _pressedFragment = fragment;
        _pressedLink = fragment.TryGetLinkAt(e.GetPosition(fragment), out var pressedLink)
            ? pressedLink
            : null;

        var anchor = fragment.GetDocumentOffset(e.GetPosition(fragment));
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

        await TryActivatePressedLinkAsync(e);

        if (!_isDraggingSelection)
        {
            ClearSelection();
        }

        ResetPointerState();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ResetPointerState();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
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

    private void SetSelection(int firstOffset, int secondOffset)
    {
        var range = DocumentTextRange.FromBounds(firstOffset, secondOffset);
        SelectionStart = range.Start;
        SelectionEnd = range.End;
        ApplySelectionToFragments();
    }

    private void ApplySelectionToFragments()
    {
        var range = new DocumentTextRange(SelectionStart, SelectionEnd);
        foreach (var fragment in _selectionFragments)
        {
            fragment.SelectionRange = range;
        }
    }

    private async Task CopySelectionToClipboardAsync()
    {
        var text = SelectedText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await ClipboardExtensions.SetTextAsync(
            clipboard,
            text.Replace("\n", Environment.NewLine, StringComparison.Ordinal));
    }

    private async Task TryActivatePressedLinkAsync(PointerReleasedEventArgs e)
    {
        if (_isDraggingSelection
            || SelectionAnchor is null
            || SelectionStart != SelectionEnd
            || _pressedFragment is null
            || _pressedLink is not MarkdownLinkSpan pressedLink)
        {
            return;
        }

        var releasePosition = e.GetPosition(_pressedFragment);
        if (!_pressedFragment.TryGetLinkAt(releasePosition, out var releasedLink))
        {
            return;
        }

        if (releasedLink.Range != pressedLink.Range || !string.Equals(releasedLink.Url, pressedLink.Url, StringComparison.Ordinal))
        {
            return;
        }

        if (!Uri.TryCreate(pressedLink.Url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is null)
        {
            return;
        }

        await launcher.LaunchUriAsync(uri);
    }

    private int ResolveDocumentOffset(Point position)
    {
        if (_selectionFragments.Count == 0)
        {
            return 0;
        }

        MarkdownSelectionTextFragment? nearestFragment = null;
        Point nearestLocalPoint = default;
        double nearestDistance = double.PositiveInfinity;

        foreach (var fragment in _selectionFragments)
        {
            var localPoint = this.TranslatePoint(position, fragment);
            if (localPoint is null)
            {
                continue;
            }

            var local = localPoint.Value;
            if (local.Y >= 0 && local.Y <= fragment.Bounds.Height)
            {
                return fragment.GetDocumentOffset(ClampPointToFragment(fragment, local));
            }

            var distance = local.Y < 0
                ? -local.Y
                : local.Y - fragment.Bounds.Height;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestFragment = fragment;
                nearestLocalPoint = local;
            }
        }

        if (nearestFragment is null)
        {
            return 0;
        }

        return nearestFragment.GetDocumentOffset(ClampPointToFragment(nearestFragment, nearestLocalPoint));
    }

    private static Point ClampPointToFragment(MarkdownSelectionTextFragment fragment, Point point)
    {
        var width = Math.Max(fragment.Bounds.Width, 1);
        var height = Math.Max(fragment.Bounds.Height, 1);
        return new Point(
            Math.Clamp(point.X, 0, width),
            Math.Clamp(point.Y, 0, height - 1));
    }

    private static bool HasCommandModifier(KeyModifiers modifiers)
        => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private void ResetPointerState()
    {
        _isPointerPressed = false;
        _isDraggingSelection = false;
        _pointerPressOrigin = default;
        _pressedFragment = null;
        _pressedLink = null;
    }

    private FontFamily ResolveBodyFontFamily() => ReadingPreferences.FontFamily switch
    {
        FontFamilyMode.Sans => new FontFamily("Segoe UI, Inter, system-ui, sans-serif"),
        FontFamilyMode.Mono => ResolveMonoFontFamily(),
        _ => new FontFamily("Georgia, Iowan Old Style, Cambria, serif")
    };

    private static FontFamily ResolveMonoFontFamily() => new("Cascadia Code, Consolas, Menlo, monospace");

    private double GetBodyLineHeight() => Math.Max(ReadingPreferences.FontSize * ReadingPreferences.LineHeight, ReadingPreferences.FontSize + 4);

    private double GetHeadingFontSize(int level)
    {
        var baseSize = ReadingPreferences.FontSize;
        return level switch
        {
            1 => baseSize * 2.0,
            2 => baseSize * 1.65,
            3 => baseSize * 1.4,
            4 => baseSize * 1.2,
            5 => baseSize * 1.05,
            _ => baseSize
        };
    }
}
