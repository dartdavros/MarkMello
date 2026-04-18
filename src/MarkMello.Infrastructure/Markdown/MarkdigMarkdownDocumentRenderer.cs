using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using MarkdigMarkdown = Markdig.Markdown;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using CodeBlock = Markdig.Syntax.CodeBlock;

namespace MarkMello.Infrastructure.Markdown;

/// <summary>
/// Markdig-based parse layer for M3.
/// Даёт устойчивый AST CommonMark + common extensions, затем переводит его
/// в UI-agnostic document model.
/// </summary>
public sealed class MarkdigMarkdownDocumentRenderer : IMarkdownDocumentRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public RenderedMarkdownDocument Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return RenderedMarkdownDocument.Empty;
        }

        var document = MarkdigMarkdown.Parse(markdown, Pipeline);
        var blocks = ConvertBlocks(document);
        return new RenderedMarkdownDocument(blocks);
    }

    private static List<MarkdownBlock> ConvertBlocks(ContainerBlock container)
    {
        var result = new List<MarkdownBlock>(container.Count);

        foreach (var block in container)
        {
            AddConvertedBlock(block, result);
        }

        return result;
    }

    private static void AddConvertedBlock(Block block, List<MarkdownBlock> target)
    {
        switch (block)
        {
            case HeadingBlock heading:
                target.Add(new MarkdownHeadingBlock(
                    Math.Clamp(heading.Level, 1, 6),
                    ConvertInlines(heading.Inline)));
                return;

            case ParagraphBlock paragraph:
                // A paragraph whose only meaningful inline node is an image
                // becomes a block-level image. This matches how authors write
                // "figure" style images as a standalone paragraph.
                if (TryExtractStandaloneImage(paragraph.Inline, out var standaloneImage))
                {
                    target.Add(standaloneImage);
                    return;
                }
                target.Add(new MarkdownParagraphBlock(ConvertInlines(paragraph.Inline)));
                return;

            case QuoteBlock quote:
                target.Add(new MarkdownQuoteBlock(ConvertBlocks(quote)));
                return;

            case ListBlock list:
                target.Add(ConvertList(list));
                return;

            case ThematicBreakBlock:
                target.Add(new MarkdownHorizontalRuleBlock());
                return;

            case FencedCodeBlock fencedCode:
                target.Add(new MarkdownCodeBlock(
                    NormalizeNullable(fencedCode.Info?.ToString()),
                    ExtractCode(fencedCode)));
                return;

            case CodeBlock codeBlock:
                target.Add(new MarkdownCodeBlock(null, ExtractCode(codeBlock)));
                return;

            case Table table:
                target.Add(ConvertTable(table));
                return;

            case HtmlBlock htmlBlock:
                // We intentionally do NOT switch on htmlBlock.Type here --
                // Markdig's HtmlBlockType enum has changed names across
                // versions (ScriptBlock, ScriptTag, ScriptPreOrStyle...).
                // Instead, HtmlToPlainText strips scripts, styles, comments
                // and CDATA blocks by content, which is stable regardless
                // of Markdig's internal classification.
                var htmlRaw = htmlBlock.Lines.ToString();
                if (TryExtractStandaloneImgTag(htmlRaw, out var imgBlock))
                {
                    target.Add(imgBlock);
                    return;
                }
                var plainText = HtmlToPlainText(htmlRaw);
                if (!string.IsNullOrWhiteSpace(plainText))
                {
                    target.Add(new MarkdownParagraphBlock([
                        new MarkdownTextInline(plainText)
                    ]));
                }
                return;

            case ContainerBlock nested:
                foreach (var nestedBlock in ConvertBlocks(nested))
                {
                    target.Add(nestedBlock);
                }
                return;

            case LeafBlock leaf:
                var leafText = ExtractLeafText(leaf);
                if (!string.IsNullOrWhiteSpace(leafText))
                {
                    target.Add(new MarkdownParagraphBlock([
                        new MarkdownTextInline(leafText)
                    ]));
                }
                return;
        }
    }

    private static MarkdownListBlock ConvertList(ListBlock list)
    {
        var items = new List<MarkdownListItem>(list.Count);

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
            {
                continue;
            }

            items.Add(new MarkdownListItem(ConvertBlocks(item)));
        }

        return new MarkdownListBlock(list.IsOrdered, items);
    }

    private static MarkdownTableBlock ConvertTable(Table table)
    {
        var header = new List<MarkdownTableCell>();
        var rows = new List<IReadOnlyList<MarkdownTableCell>>();

        foreach (var child in table)
        {
            if (child is not TableRow row)
            {
                continue;
            }

            var cells = new List<MarkdownTableCell>(row.Count);
            foreach (var rowChild in row)
            {
                if (rowChild is not TableCell cell)
                {
                    continue;
                }

                cells.Add(new MarkdownTableCell(ConvertBlocksToInlines(cell)));
            }

            if (row.IsHeader)
            {
                header.AddRange(cells);
            }
            else
            {
                rows.Add(cells);
            }
        }

        return new MarkdownTableBlock(header, rows);
    }

    private static IReadOnlyList<MarkdownInline> ConvertBlocksToInlines(ContainerBlock container)
    {
        var blocks = ConvertBlocks(container);
        if (blocks.Count == 0)
        {
            return Array.Empty<MarkdownInline>();
        }

        var result = new List<MarkdownInline>();
        var first = true;

        foreach (var block in blocks)
        {
            if (!first)
            {
                result.Add(new MarkdownLineBreakInline());
            }
            first = false;

            switch (block)
            {
                case MarkdownParagraphBlock paragraph:
                    AddInlineRange(result, paragraph.Inlines);
                    break;

                case MarkdownHeadingBlock heading:
                    AddInlineRange(result, heading.Inlines);
                    break;

                case MarkdownCodeBlock code:
                    result.Add(new MarkdownCodeInline(code.Code));
                    break;

                default:
                    var text = ExtractPlainText(block);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(new MarkdownTextInline(text));
                    }
                    break;
            }
        }

        return result;
    }

    private static IReadOnlyList<MarkdownInline> ConvertInlines(ContainerInline? container)
    {
        if (container is null)
        {
            return Array.Empty<MarkdownInline>();
        }

        var result = new List<MarkdownInline>();

        foreach (var inline in container)
        {
            AddConvertedInline(inline, result);
        }

        return result;
    }

    private static void AddConvertedInline(Inline inline, List<MarkdownInline> target)
    {
        switch (inline)
        {
            case LiteralInline literal:
                var text = literal.Content.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    target.Add(new MarkdownTextInline(text));
                }
                return;

            case LineBreakInline:
                target.Add(new MarkdownLineBreakInline());
                return;

            case CodeInline code:
                target.Add(new MarkdownCodeInline(code.Content.ToString()));
                return;

            case LinkInline link when !link.IsImage:
                var linkText = ConvertInlines(link);
                target.Add(new MarkdownLinkInline(
                    linkText,
                    NormalizeNullable(link.Url) ?? string.Empty,
                    NormalizeNullable(link.Title)));
                return;

            case EmphasisInline emphasis:
                var children = ConvertInlines(emphasis);
                if (emphasis.DelimiterCount >= 2)
                {
                    target.Add(new MarkdownStrongInline(children));
                }
                else
                {
                    target.Add(new MarkdownEmphasisInline(children));
                }
                return;

            case HtmlEntityInline entityInline:
                var decoded = entityInline.Transcoded.ToString();
                if (!string.IsNullOrEmpty(decoded))
                {
                    target.Add(new MarkdownTextInline(decoded));
                }
                return;

            case HtmlInline htmlInline:
                HandleInlineHtmlTag(htmlInline.Tag, target);
                return;

            case ContainerInline nested:
                foreach (var child in ConvertInlines(nested))
                {
                    target.Add(child);
                }
                return;
        }
    }

    private static string ExtractCode(CodeBlock codeBlock)
    {
        var text = codeBlock.Lines.ToString();
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    private static string ExtractLeafText(LeafBlock block)
    {
        if (block.Inline is not null)
        {
            return ExtractPlainText(ConvertInlines(block.Inline));
        }

        return block.Lines.ToString();
    }

    private static void AddInlineRange(List<MarkdownInline> target, IReadOnlyList<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            target.Add(inline);
        }
    }

    private static string ExtractPlainText(MarkdownBlock block) => block switch
    {
        MarkdownParagraphBlock paragraph => ExtractPlainText(paragraph.Inlines),
        MarkdownHeadingBlock heading => ExtractPlainText(heading.Inlines),
        MarkdownCodeBlock code => code.Code,
        MarkdownQuoteBlock quote => string.Join(Environment.NewLine, quote.Blocks.Select(ExtractPlainText)),
        MarkdownListBlock list => string.Join(Environment.NewLine, list.Items.Select(item => string.Join(" ", item.Blocks.Select(ExtractPlainText)))),
        MarkdownTableBlock table => string.Join(Environment.NewLine, table.Rows.Select(row => string.Join(" | ", row.Select(cell => ExtractPlainText(cell.Inlines))))),
        _ => string.Empty
    };

    private static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        if (inlines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            AppendPlainText(inline, builder);
        }
        return builder.ToString();
    }

    private static void AppendPlainText(MarkdownInline inline, System.Text.StringBuilder builder)
    {
        switch (inline)
        {
            case MarkdownTextInline text:
                builder.Append(text.Text);
                break;
            case MarkdownStrongInline strong:
                AppendPlainText(strong.Inlines, builder);
                break;
            case MarkdownEmphasisInline emphasis:
                AppendPlainText(emphasis.Inlines, builder);
                break;
            case MarkdownCodeInline code:
                builder.Append(code.Code);
                break;
            case MarkdownLinkInline link:
                if (link.Inlines.Count > 0)
                {
                    AppendPlainText(link.Inlines, builder);
                }
                else if (!string.IsNullOrWhiteSpace(link.Url))
                {
                    builder.Append(link.Url);
                }
                break;
            case MarkdownLineBreakInline:
                builder.AppendLine();
                break;
        }
    }

    private static void AppendPlainText(IReadOnlyList<MarkdownInline> inlines, System.Text.StringBuilder builder)
    {
        foreach (var inline in inlines)
        {
            AppendPlainText(inline, builder);
        }
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // --- HTML handling ----------------------------------------------------

    private static readonly Regex ImgTagPattern = new(
        @"<img\b[^>]*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AltAttrPattern = new(
        @"\balt\s*=\s*(?:""([^""]*)""|'([^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SrcAttrPattern = new(
        @"\bsrc\s*=\s*(?:""([^""]*)""|'([^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleAttrPattern = new(
        @"\btitle\s*=\s*(?:""([^""]*)""|'([^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LineBreakTagPattern = new(
        @"^<br\b[^>]*/?>$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnyTagPattern = new(
        @"<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled);

    // Patterns that must strip their *entire* body, not just the opening/closing
    // tags. If we only stripped <script> without its content we would leak
    // executable code text into the reading view.
    private static readonly Regex ScriptBodyPattern = new(
        @"<script\b[^>]*>.*?</script\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex StyleBodyPattern = new(
        @"<style\b[^>]*>.*?</style\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CommentBodyPattern = new(
        @"<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CDataBodyPattern = new(
        @"<!\[CDATA\[.*?\]\]>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ProcessingInstructionPattern = new(
        @"<\?.*?\?>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex DoctypePattern = new(
        @"<!DOCTYPE\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Converts an HTML block body to plain text suitable for rendering inside a
    /// Markdown paragraph. Images are preserved as [image: alt] placeholders, all
    /// other tags are stripped, and HTML entities are decoded. Whitespace collapses.
    /// </summary>
    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        // 0) Drop things whose *content* must not appear in the viewer at all,
        //    not just their surrounding tags: script bodies, style rules,
        //    comments, CDATA, processing instructions, doctype.
        //    Doing this by content (not by Markdig's HtmlBlockType enum) keeps
        //    the code independent of Markdig version-specific enum names.
        var scrubbed = html;
        scrubbed = ScriptBodyPattern.Replace(scrubbed, string.Empty);
        scrubbed = StyleBodyPattern.Replace(scrubbed, string.Empty);
        scrubbed = CommentBodyPattern.Replace(scrubbed, string.Empty);
        scrubbed = CDataBodyPattern.Replace(scrubbed, string.Empty);
        scrubbed = ProcessingInstructionPattern.Replace(scrubbed, string.Empty);
        scrubbed = DoctypePattern.Replace(scrubbed, string.Empty);

        // 1) Replace <img> with a readable placeholder so the user sees that an
        //    image was present even though we don't render images yet.
        var withImagePlaceholders = ImgTagPattern.Replace(scrubbed, m => FormatImagePlaceholder(m.Value));

        // 2) Strip every remaining tag (including <picture>, <source>, <div>, <p>, ...).
        var tagsStripped = AnyTagPattern.Replace(withImagePlaceholders, " ");

        // 3) Decode HTML entities (&amp; -> &, &nbsp; -> non-breaking space, ...).
        var decoded = WebUtility.HtmlDecode(tagsStripped);

        // 4) Collapse runs of whitespace to a single space and trim.
        return WhitespacePattern.Replace(decoded, " ").Trim();
    }

    private static void HandleInlineHtmlTag(string tag, List<MarkdownInline> target)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        if (LineBreakTagPattern.IsMatch(tag))
        {
            // <br>, <br/>, <br /> should preserve the author's intended line break.
            target.Add(new MarkdownLineBreakInline());
            return;
        }

        if (ImgTagPattern.IsMatch(tag))
        {
            target.Add(new MarkdownTextInline(FormatImagePlaceholder(tag)));
            return;
        }

        // Generic container/opener/closer tags (<b>, </b>, <span>, ...): drop them.
        // The surrounding literal inlines already carry the visible text, so
        // skipping the tag text is equivalent to "strip tags, keep content".
    }

    private static string FormatImagePlaceholder(string imgTag)
    {
        var altMatch = AltAttrPattern.Match(imgTag);
        if (altMatch.Success)
        {
            var alt = altMatch.Groups[1].Success
                ? altMatch.Groups[1].Value
                : altMatch.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(alt))
            {
                return $"[image: {alt}]";
            }
        }

        return "[image]";
    }

    /// <summary>
    /// Detects the "figure" pattern: a paragraph whose only visible content
    /// is a single markdown image (![alt](url) optionally preceded/followed
    /// by whitespace or a line break). Returns the extracted image block.
    /// </summary>
    private static bool TryExtractStandaloneImage(
        ContainerInline? container,
        out MarkdownImageBlock imageBlock)
    {
        imageBlock = null!;
        if (container is null)
        {
            return false;
        }

        LinkInline? onlyImage = null;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    // Allow whitespace-only literals around the image.
                    var text = literal.Content.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return false;
                    }
                    break;

                case LineBreakInline:
                    // A trailing soft break doesn't disqualify the figure pattern.
                    break;

                case LinkInline link when link.IsImage:
                    if (onlyImage is not null)
                    {
                        // Two or more images in the same paragraph -- keep
                        // them inline so the user's layout intent is clear.
                        return false;
                    }
                    onlyImage = link;
                    break;

                default:
                    // Anything else (emphasis, other links, code, nested HTML)
                    // means this paragraph is a real paragraph, not a figure.
                    return false;
            }
        }

        if (onlyImage is null)
        {
            return false;
        }

        var altText = ExtractPlainText(ConvertInlines(onlyImage));
        imageBlock = new MarkdownImageBlock(
            Url: NormalizeNullable(onlyImage.Url) ?? string.Empty,
            AltText: string.IsNullOrWhiteSpace(altText) ? null : altText,
            Title: NormalizeNullable(onlyImage.Title));
        return true;
    }

    /// <summary>
    /// Detects an HTML block whose only meaningful content is a single
    /// &lt;img&gt; tag (optionally wrapped in picture/source/figure/figcaption/div).
    /// Returns the extracted image block with the inner src/alt/title.
    /// </summary>
    private static bool TryExtractStandaloneImgTag(string html, out MarkdownImageBlock imageBlock)
    {
        imageBlock = null!;
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var imgMatches = ImgTagPattern.Matches(html);
        if (imgMatches.Count != 1)
        {
            // Zero or many -- not a figure, fall back to plain-text handling.
            return false;
        }

        var imgTag = imgMatches[0].Value;

        // Everything EXCEPT that single <img> must reduce to just tags and
        // whitespace -- no visible text belongs next to the image when we
        // promote it. Remove the img itself, then strip surrounding tags;
        // what's left should be empty.
        var withoutImg = html.Replace(imgTag, string.Empty, StringComparison.Ordinal);
        var textAround = AnyTagPattern.Replace(withoutImg, " ").Trim();
        if (!string.IsNullOrWhiteSpace(textAround))
        {
            return false;
        }

        var src = ExtractAttr(SrcAttrPattern, imgTag);
        if (string.IsNullOrWhiteSpace(src))
        {
            return false;
        }

        var alt = ExtractAttr(AltAttrPattern, imgTag);
        var title = ExtractAttr(TitleAttrPattern, imgTag);

        imageBlock = new MarkdownImageBlock(
            Url: src,
            AltText: string.IsNullOrWhiteSpace(alt) ? null : alt,
            Title: string.IsNullOrWhiteSpace(title) ? null : title);
        return true;
    }

    private static string? ExtractAttr(Regex pattern, string tag)
    {
        var m = pattern.Match(tag);
        if (!m.Success)
        {
            return null;
        }
        var value = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
