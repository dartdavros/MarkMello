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
    private Control BuildBlock(MarkdownBlock block, string path, bool nested, bool insideQuote = false)
    {
        var control = block switch
        {
            MarkdownHeadingBlock heading => BuildHeading(heading, path),
            MarkdownParagraphBlock paragraph => BuildParagraph(paragraph, path, nested, insideQuote),
            MarkdownQuoteBlock quote => BuildQuote(quote, path),
            MarkdownListBlock list => BuildList(list, path, insideQuote),
            MarkdownHorizontalRuleBlock => BuildHorizontalRule(),
            MarkdownCodeBlock code => BuildCodeBlock(code, path),
            MarkdownTableBlock table => BuildTable(table, path),
            MarkdownImageBlock image => BuildImageBlock(image),
            MarkdownDiagramBlock diagram => BuildDiagramBlock(diagram),
            MarkdownMathBlock math => BuildMathBlock(math),
            _ => BuildFallback(block)
        };

        RegisterSourceLineAnchor(block, control);
        return control;
    }

    private void RegisterSourceLineAnchor(MarkdownBlock block, Control control)
    {
        if (block.SourceSpan is not { } sourceSpan)
        {
            return;
        }

        _sourceLineAnchors.Add(new MarkdownSourceLineVisualAnchor(control, sourceSpan));
    }

    private static MarkdownDiagramBlockView BuildDiagramBlock(MarkdownDiagramBlock block)
        => new(block);

    private MarkdownImageView BuildImageBlock(MarkdownImageBlock block)
        => new(
            resolver: ImageSourceResolver,
            url: block.Url,
            altText: block.AltText,
            title: block.Title,
            width: block.Width,
            height: block.Height,
            baseDirectory: Document?.BaseDirectory)
        {
            Margin = new Thickness(0, 12, 0, 22),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 1200,
        };

    private Control BuildHeading(MarkdownHeadingBlock block, string path)
    {
        var fontSize = GetHeadingFontSize(block.Level);
        var lineHeight = Math.Max(fontSize * 1.25, fontSize + 4);
        var margin = block.Level == 1
            ? new Thickness(0, 0, 0, 10)
            : block.Level == 2
                ? new Thickness(0, 28, 0, 14)
                : new Thickness(0, 18, 0, 10);

        // Design: h1 -> 700, h2+ -> 600. Previous code had this inverted.
        var weight = block.Level == 1 ? FontWeight.Bold : FontWeight.SemiBold;

        // h5 / h6 render with the soft text colour in the design.
        var baseForeground = block.Level >= 5 ? LookupBrush("MmTextSoftBrush") : null;

        var headingControl = BuildSelectionFragment(
            path,
            block.Inlines,
            margin,
            fontSize,
            lineHeight,
            weight,
            FontStyle.Normal,
            fallbackClassName: "mm-md-heading",
            baseForeground: baseForeground);

        RegisterHeadingAnchor(block, headingControl);
        return headingControl;
    }

    private Control BuildParagraph(MarkdownParagraphBlock block, string path, bool nested, bool insideQuote)
    {
        var fontStyle = insideQuote ? FontStyle.Italic : FontStyle.Normal;
        var baseForeground = insideQuote ? LookupBrush("MmTextSoftBrush") : null;

        return BuildSelectionFragment(
            path,
            block.Inlines,
            nested ? new Thickness(0, 0, 0, 10) : new Thickness(0, 0, 0, 18),
            ReadingPreferences.FontSize,
            GetBodyLineHeight(),
            FontWeight.Normal,
            fontStyle,
            fallbackClassName: "mm-md-paragraph",
            baseForeground: baseForeground);
    }

    private StackPanel BuildList(MarkdownListBlock block, string path, bool insideQuote = false)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 18)
        };

        for (var index = 0; index < block.Items.Count; index++)
        {
            panel.Children.Add(BuildListItem(block, block.Items[index], index, $"{path}.i{index}", insideQuote));
        }

        return panel;
    }

    private Grid BuildListItem(MarkdownListBlock list, MarkdownListItem item, int index, string path, bool insideQuote = false)
    {
        var bullet = BuildSelectionFragment(
            $"{path}.m",
            [new MarkdownTextInline(list.IsOrdered ? $"{index + 1}. " : "• ")],
            margin: default,
            ReadingPreferences.FontSize,
            GetBodyLineHeight(),
            FontWeight.Normal,
            FontStyle.Normal,
            fallbackClassName: "mm-md-list-bullet",
            textWrapping: TextWrapping.NoWrap);

        bullet.VerticalAlignment = VerticalAlignment.Top;

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        for (var blockIndex = 0; blockIndex < item.Blocks.Count; blockIndex++)
        {
            content.Children.Add(BuildBlock(item.Blocks[blockIndex], $"{path}.b{blockIndex}", nested: true, insideQuote: insideQuote));
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

    private static Grid BuildHorizontalRule()
    {
        // Design: 40% wide, horizontally centered. Avalonia has no percentage
        // widths, so we model it as a three-column grid in 3*,4*,3* ratio with
        // the rule in the middle column.
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3*,4*,3*"),
            Margin = new Thickness(0, 32, 0, 32),
        };
        var line = new Border { Classes = { "mm-md-hr" } };
        Grid.SetColumn(line, 1);
        grid.Children.Add(line);
        return grid;
    }
}
