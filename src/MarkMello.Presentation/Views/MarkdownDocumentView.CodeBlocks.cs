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
using MarkMello.Presentation.Views.Markdown.Syntax;
using System.Globalization;
using System.Text;
using System.Threading;

namespace MarkMello.Presentation.Views;


public sealed partial class MarkdownDocumentView
{
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
            textWrapping: TextWrapping.NoWrap,
            styledText: MarkdownStyledText.FromCode(block.Code, CodeSyntaxHighlighter.Tokenize(block.Code, block.Info)));

        body.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                Padding = new Thickness(0, 0, 0, CodeBlockHorizontalScrollBarReserve),
                Child = codeFragment
            }
        });

        var contentGrid = new Grid();
        contentGrid.Children.Add(body);

        var copyButton = CreateCodeCopyButton(block.Code);
        copyButton.HorizontalAlignment = HorizontalAlignment.Right;
        copyButton.VerticalAlignment = VerticalAlignment.Top;
        copyButton.Margin = new Thickness(0, 0, 0, 0);
        contentGrid.Children.Add(copyButton);

        return new Border
        {
            Classes = { "mm-md-codeblock" },
            Child = contentGrid
        };
    }

    private Button CreateCodeCopyButton(string code)
    {
        var button = new Button
        {
            Classes = { "mm-code-copy-button" },
            Content = GetLocalizedString("ContextCopy", "Copy"),
            IsTabStop = true
        };

        ToolTip.SetTip(button, GetLocalizedString("CodeCopyTooltip", "Copy code"));
        button.Click += async (_, e) =>
        {
            await CopyTextToClipboardAsync(code).ConfigureAwait(true);
            e.Handled = true;
        };

        return button;
    }
}
