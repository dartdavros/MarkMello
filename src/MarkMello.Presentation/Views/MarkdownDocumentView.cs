using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using MarkMello.Domain;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Native Markdown renderer для viewer mode.
/// Строит визуальное дерево из UI-agnostic document model без WebView/HTML path.
/// </summary>
public sealed class MarkdownDocumentView : UserControl
{
    public static readonly StyledProperty<RenderedMarkdownDocument?> DocumentProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, RenderedMarkdownDocument?>(nameof(Document));

    public static readonly StyledProperty<ReadingPreferences> ReadingPreferencesProperty =
        AvaloniaProperty.Register<MarkdownDocumentView, ReadingPreferences>(
            nameof(ReadingPreferences),
            ReadingPreferences.Default);

    private readonly StackPanel _root = new()
    {
        Orientation = Orientation.Vertical,
        Spacing = 0
    };

    static MarkdownDocumentView()
    {
        DocumentProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.Rebuild());
        ReadingPreferencesProperty.Changed.AddClassHandler<MarkdownDocumentView>((view, _) => view.Rebuild());
    }

    public MarkdownDocumentView()
    {
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

    private void Rebuild()
    {
        _root.Children.Clear();

        var document = Document;
        if (document is null || document.Blocks.Count == 0)
        {
            return;
        }

        foreach (var block in document.Blocks)
        {
            _root.Children.Add(BuildBlock(block, nested: false));
        }
    }

    private Control BuildBlock(MarkdownBlock block, bool nested)
        => block switch
        {
            MarkdownHeadingBlock heading => BuildHeading(heading),
            MarkdownParagraphBlock paragraph => BuildParagraph(paragraph, nested),
            MarkdownQuoteBlock quote => BuildQuote(quote),
            MarkdownListBlock list => BuildList(list),
            MarkdownHorizontalRuleBlock => BuildHorizontalRule(),
            MarkdownCodeBlock code => BuildCodeBlock(code),
            MarkdownTableBlock table => BuildTable(table),
            _ => BuildFallback(block)
        };

    private TextBlock BuildHeading(MarkdownHeadingBlock block)
    {
        var textBlock = CreateTextBlock("mm-md-heading");
        textBlock.FontSize = GetHeadingFontSize(block.Level);
        textBlock.LineHeight = Math.Max(textBlock.FontSize * 1.2, textBlock.FontSize + 4);
        textBlock.Margin = block.Level <= 2
            ? new Thickness(0, 0, 0, 22)
            : new Thickness(0, 10, 0, 16);
        textBlock.FontWeight = block.Level <= 2 ? FontWeight.SemiBold : FontWeight.Bold;

        AddInlines(EnsureInlines(textBlock), block.Inlines);
        return textBlock;
    }

    private TextBlock BuildParagraph(MarkdownParagraphBlock block, bool nested)
    {
        var textBlock = CreateTextBlock("mm-md-paragraph");
        textBlock.Margin = nested
            ? new Thickness(0, 0, 0, 10)
            : new Thickness(0, 0, 0, 18);

        AddInlines(EnsureInlines(textBlock), block.Inlines);
        return textBlock;
    }

    private Border BuildQuote(MarkdownQuoteBlock block)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        foreach (var nestedBlock in block.Blocks)
        {
            stack.Children.Add(BuildBlock(nestedBlock, nested: true));
        }

        return new Border
        {
            Classes = { "mm-md-quote" },
            Child = stack
        };
    }

    private StackPanel BuildList(MarkdownListBlock block)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 18)
        };

        for (var index = 0; index < block.Items.Count; index++)
        {
            panel.Children.Add(BuildListItem(block, block.Items[index], index));
        }

        return panel;
    }

    private Grid BuildListItem(MarkdownListBlock list, MarkdownListItem item, int index)
    {
        var bullet = new TextBlock
        {
            Text = list.IsOrdered ? $"{index + 1}." : "•",
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            FontSize = ReadingPreferences.FontSize,
            FontFamily = ResolveBodyFontFamily(),
            FontWeight = FontWeight.Medium,
            Classes = { "mm-md-list-bullet" }
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        foreach (var block in item.Blocks)
        {
            content.Children.Add(BuildBlock(block, nested: true));
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
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
        => new Border
        {
            Classes = { "mm-md-hr" }
        };

    private Border BuildCodeBlock(MarkdownCodeBlock block)
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
                Classes = { "mm-md-code-info" }
            });
        }

        body.Children.Add(new SelectableTextBlock
        {
            Text = block.Code,
            Classes = { "mm-md-codeblock-text" },
            FontFamily = ResolveMonoFontFamily(),
            FontSize = Math.Max(12, ReadingPreferences.FontSize - 2),
            LineHeight = Math.Max(16, (ReadingPreferences.FontSize - 2) * 1.5),
            TextWrapping = TextWrapping.NoWrap
        });

        return new Border
        {
            Classes = { "mm-md-codeblock" },
            Child = body
        };
    }

    private Control BuildTable(MarkdownTableBlock table)
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
            AddTableRow(grid, table.Header, currentRow, isHeader: true);
            currentRow++;
        }

        foreach (var row in table.Rows)
        {
            AddTableRow(grid, row, currentRow, isHeader: false);
            currentRow++;
        }

        return new Border
        {
            Classes = { "mm-md-table" },
            Child = grid,
            Margin = new Thickness(0, 0, 0, 22)
        };
    }

    private void AddTableRow(Grid grid, IReadOnlyList<MarkdownTableCell> cells, int rowIndex, bool isHeader)
    {
        for (var columnIndex = 0; columnIndex < grid.ColumnDefinitions.Count; columnIndex++)
        {
            var cell = columnIndex < cells.Count
                ? cells[columnIndex]
                : new MarkdownTableCell(Array.Empty<MarkdownInline>());

            var textBlock = CreateTextBlock(isHeader ? "mm-md-table-header" : "mm-md-table-text");
            textBlock.Margin = default;
            AddInlines(EnsureInlines(textBlock), cell.Inlines);

            var border = new Border
            {
                Classes = { isHeader ? "mm-md-table-header-cell" : "mm-md-table-cell" },
                Child = textBlock
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
            Text = block.ToString(),
            Classes = { "mm-md-paragraph" },
            FontFamily = ResolveBodyFontFamily(),
            FontSize = ReadingPreferences.FontSize,
            LineHeight = GetBodyLineHeight()
        };
    }

    private static InlineCollection EnsureInlines(TextBlock textBlock)
    {
        if (textBlock.Inlines is not null)
        {
            return textBlock.Inlines;
        }

        textBlock.Inlines = new InlineCollection();
        return textBlock.Inlines;
    }

    private static InlineCollection EnsureInlines(Span span)
    {
        if (span.Inlines is not null)
        {
            return span.Inlines;
        }

        span.Inlines = new InlineCollection();
        return span.Inlines;
    }

    private TextBlock CreateTextBlock(string className)
    {
        return new TextBlock
        {
            Classes = { className },
            FontFamily = ResolveBodyFontFamily(),
            FontSize = ReadingPreferences.FontSize,
            LineHeight = GetBodyLineHeight(),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private void AddInlines(InlineCollection target, IReadOnlyList<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            AddInline(target, inline);
        }
    }

    private void AddInline(InlineCollection target, MarkdownInline inline)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                target.Add(new Run(text.Text));
                break;

            case MarkdownStrongInline strong:
                var bold = new Bold();
                AddInlines(EnsureInlines(bold), strong.Inlines);
                target.Add(bold);
                break;

            case MarkdownEmphasisInline emphasis:
                var italic = new Italic();
                AddInlines(EnsureInlines(italic), emphasis.Inlines);
                target.Add(italic);
                break;

            case MarkdownCodeInline code:
                var codeSpan = new Span
                {
                    Classes = { "mm-md-code-inline" },
                    FontFamily = ResolveMonoFontFamily()
                };
                EnsureInlines(codeSpan).Add(new Run(code.Code));
                target.Add(codeSpan);
                break;

            case MarkdownLineBreakInline:
                target.Add(new LineBreak());
                break;

            case MarkdownLinkInline link:
                if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
                {
                    var linkText = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(link.Text) ? link.Url : link.Text,
                        Classes = { "mm-md-link-text" },
                        FontFamily = ResolveBodyFontFamily(),
                        FontSize = ReadingPreferences.FontSize
                    };

                    var button = new HyperlinkButton
                    {
                        Classes = { "mm-md-link" },
                        Content = linkText,
                        NavigateUri = uri
                    };

                    ToolTip.SetTip(button, string.IsNullOrWhiteSpace(link.Title) ? link.Url : link.Title);

                    target.Add(new InlineUIContainer
                    {
                        BaselineAlignment = BaselineAlignment.Baseline,
                        Child = button
                    });
                }
                else
                {
                    var fallback = new Underline
                    {
                        Classes = { "mm-md-link-fallback" }
                    };
                    EnsureInlines(fallback).Add(new Run(string.IsNullOrWhiteSpace(link.Text) ? link.Url : link.Text));
                    target.Add(fallback);
                }
                break;
        }
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
