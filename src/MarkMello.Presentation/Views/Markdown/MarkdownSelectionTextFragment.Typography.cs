using Avalonia.Controls;
using Avalonia.Media;
using MarkMello.Domain;
using MarkMello.Presentation.Views.Markdown.Syntax;

namespace MarkMello.Presentation.Views.Markdown;

internal sealed partial class MarkdownSelectionTextFragment
{
    public FontFamily? InlineCodeFontFamily { get; init; }

    private MarkdownFormattedTextLayout GetOrCreateTextLayout(double availableWidth)
    {
        var normalizedWidth = NormalizeLayoutWidth(availableWidth);
        if (_textLayout is not null && Math.Abs(_layoutWidth - normalizedWidth) < 0.5)
        {
            return _textLayout;
        }

        InvalidateTextLayout();
        _layoutWidth = normalizedWidth;
        _textLayout = new MarkdownFormattedTextLayout(
            StyledText,
            _inlineImages,
            BaseFontFamily,
            InlineCodeFontFamily ?? ResolveInlineCodeFontFamily(),
            BaseFontSize,
            BaseFontWeight,
            BaseFontStyle,
            double.IsNaN(BaseLineHeight) ? double.NaN : BaseLineHeight,
            _letterSpacing,
            LayoutTextWrapping,
            normalizedWidth,
            ResolveBaseTextBrush(),
            BuildLinkTextDecorations(),
            ResolveSyntaxBrush);

        return _textLayout;
    }

    /// <summary>
    /// Syntax colours live in the theme dictionaries, so a theme switch re-resolves them
    /// together with the rest of the text when the layout is invalidated.
    /// </summary>
    private IBrush? ResolveSyntaxBrush(SyntaxTokenKind kind)
    {
        var key = kind switch
        {
            SyntaxTokenKind.Keyword => "MmSyntaxKeywordBrush",
            SyntaxTokenKind.String => "MmSyntaxStringBrush",
            SyntaxTokenKind.Comment => "MmSyntaxCommentBrush",
            SyntaxTokenKind.Number => "MmSyntaxNumberBrush",
            SyntaxTokenKind.Type => "MmSyntaxTypeBrush",
            SyntaxTokenKind.Function => "MmSyntaxFunctionBrush",
            _ => null
        };

        return key is not null && this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : null;
    }

    private FontFamily ResolveInlineCodeFontFamily()
    {
        if (this.TryFindResource("MmDocumentMonoFontFamily", ActualThemeVariant, out var value)
            && value is FontFamily family)
        {
            return family;
        }

        return new FontFamily("JetBrains Mono, Cascadia Code, Consolas, Menlo, monospace");
    }

}
