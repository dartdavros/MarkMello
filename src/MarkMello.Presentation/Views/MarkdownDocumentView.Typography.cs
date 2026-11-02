using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MarkMello.Domain;
using MarkMello.Presentation.Services;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Views;

public sealed partial class MarkdownDocumentView
{
    private TextBlock BuildFallback(MarkdownBlock block)
    {
        return new TextBlock
        {
            Text = MarkdownDocumentTextMap.ExtractPlainText(block),
            Classes = { "mm-md-paragraph" },
            FontFamily = ResolveBodyFontFamily(),
            FontSize = ReadingPreferences.FontSize,
            LineHeight = GetBodyLineHeight(),
            LetterSpacing = ReadingPreferences.LetterSpacing,
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
        TextWrapping textWrapping = TextWrapping.Wrap,
        IBrush? baseForeground = null,
        double letterSpacing = 0,
        MarkdownStyledText? styledText = null)
    {
        if (fallbackClassName != "mm-md-codeblock-text")
        {
            letterSpacing += ReadingPreferences.LetterSpacing;
        }
        var styled = styledText ?? MarkdownStyledText.FromInlines(inlines);
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
                LetterSpacing = letterSpacing,
                TextWrapping = textWrapping,
                UseLayoutRounding = true,
                Classes = { fallbackClassName }
            };

            if (baseForeground is not null)
            {
                fallback.Foreground = baseForeground;
            }

            return fallback;
        }

        if (MarkdownImageFlowFragment.TryCreate(inlines, out var imageItems))
        {
            var imageFlow = new MarkdownImageFlowFragment(imageItems)
            {
                Margin = margin,
                DocumentRange = fragment.Range,
                ImageSourceResolver = ImageSourceResolver,
                BaseDirectory = Document?.BaseDirectory,
                BaseFontFamily = resolvedFontFamily,
                BaseFontSize = fontSize,
                BaseLineHeight = lineHeight
            };

            imageFlow.Classes.Add(fallbackClassName);
            RegisterSelectionFragment(imageFlow, path);
            imageFlow.SelectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
            return imageFlow;
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
            BaseForeground = baseForeground,
            BaseLetterSpacing = letterSpacing,
            InlineCodeFontFamily = ResolveMonoFontFamily(),
            LayoutTextWrapping = textWrapping,
            ImageSourceResolver = ImageSourceResolver,
            BaseDirectory = Document?.BaseDirectory,
            Cursor = TryCreateCursor(StandardCursorType.Ibeam)
        };

        control.Classes.Add(fallbackClassName);
        RegisterSelectionFragment(control, path);
        control.SelectionRange = new DocumentTextRange(SelectionStart, SelectionEnd);
        return control;
    }

    private FontFamily ResolveBodyFontFamily() => ResolveDocumentFontFamily(ReadingPreferences.FontFamily);

    private FontFamily ResolveSansFontFamily() => ResolveDocumentFontFamily(FontFamilyMode.Sans);

    private FontFamily ResolveMonoFontFamily() => ResolveDocumentFontFamily(FontFamilyMode.Mono);

    private FontFamily ResolveDocumentFontFamily(FontFamilyMode mode)
    {
        var name = ReadingPreferences.GetFontFamilyName(mode);
        return (name is null ? null : DocumentFontCatalog.Shared.Find(name))
            ?? LookupFontFamily(DocumentFontCatalog.ResourceKey(mode));
    }

    private FontFamily LookupFontFamily(string resourceKey)
    {
        if (this.TryFindResource(resourceKey, ActualThemeVariant, out var value) && value is FontFamily family)
        {
            return family;
        }

        // Fallbacks mirror the minimal tail of the stacks in Themes/Typography.axaml
        // so that rendering stays sensible if the ResourceDictionary is not yet attached.
        return resourceKey switch
        {
            "MmDocumentSerifFontFamily" => new FontFamily("Georgia, Cambria, serif"),
            "MmDocumentSansFontFamily" => new FontFamily("Segoe UI, system-ui, sans-serif"),
            "MmDocumentMonoFontFamily" => new FontFamily("Consolas, Menlo, monospace"),
            _ => FontFamily.Default
        };
    }

}
