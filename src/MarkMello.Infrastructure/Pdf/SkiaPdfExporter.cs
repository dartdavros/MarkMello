using System.Globalization;
using System.Text;
using System.Collections.Concurrent;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using SkiaSharp;

namespace MarkMello.Infrastructure.Pdf;

public sealed class SkiaPdfExporter : IPdfExporter
{
    public async Task ExportAsync(PdfExportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(request.Path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("PDF path must contain a target directory.", nameof(request));
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = File.Create(tempPath))
            using (var document = SKDocument.CreatePdf(stream))
            {
                if (document is null)
                {
                    throw new IOException("Could not create PDF document.");
                }

                var renderer = new PdfDocumentRenderer(document, request.Title);
                renderer.Render(request.Document, cancellationToken);
                document.Close();
            }

            await MoveWithRetryAsync(tempPath, fullPath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async Task MoveWithRetryAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 8;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(120 * attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(120 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class PdfDocumentRenderer
    {
        private const float PageWidth = 595;
        private const float PageHeight = 842;
        private const float MarginX = 54;
        private const float MarginY = 54;
        private const float ParagraphGap = 8;
        private const float SmallGap = 4;
        private const float TableCellPadding = 5;

        private static readonly SKColor TextColor = new(34, 39, 46);
        private static readonly SKColor MutedColor = new(87, 96, 106);
        private static readonly SKColor RuleColor = new(208, 215, 222);
        private static readonly SKColor CodeBackgroundColor = new(246, 248, 250);
        private static readonly SKColor TableHeaderBackgroundColor = new(246, 248, 250);
        private static readonly ConcurrentDictionary<string, byte[]> NotoEmojiAssetCache = new(StringComparer.Ordinal);
        private static readonly string[] EmojiFallbackFamilies =
        [
            "Segoe UI Emoji",
            "Apple Color Emoji",
            "Noto Color Emoji",
            "Segoe UI Symbol",
            "Noto Sans Symbols2",
            "Arial Unicode MS"
        ];
        private static readonly string[] SymbolFallbackFamilies =
        [
            "Segoe UI Symbol",
            "Noto Sans Symbols2",
            "Noto Sans Symbols",
            "Arial Unicode MS",
            "Segoe UI Emoji"
        ];

        private readonly SKDocument _document;
        private readonly string _title;
        private SKCanvas? _canvas;
        private float _y;
        private int _pageNumber;

        public PdfDocumentRenderer(SKDocument document, string title)
        {
            _document = document;
            _title = string.IsNullOrWhiteSpace(title) ? "Document" : title.Trim();
        }

        public void Render(RenderedMarkdownDocument document, CancellationToken cancellationToken)
        {
            BeginPage();
            DrawTitle(_title);

            if (document.Blocks.Count == 0)
            {
                DrawParagraph("(empty document)", TextStyle.Body, indent: 0);
            }
            else
            {
                foreach (var block in document.Blocks)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RenderBlock(block, indent: 0);
                }
            }

            EndPage();
        }

        private void RenderBlock(MarkdownBlock block, float indent)
        {
            switch (block)
            {
                case MarkdownHeadingBlock heading:
                    MoveDown(heading.Level == 1 ? 12 : 8);
                    DrawParagraph(ExtractPlainText(heading.Inlines), TextStyle.ForHeading(heading.Level), indent);
                    MoveDown(SmallGap);
                    break;

                case MarkdownParagraphBlock paragraph:
                    DrawParagraph(ExtractPlainText(paragraph.Inlines), TextStyle.Body, indent);
                    MoveDown(ParagraphGap);
                    break;

                case MarkdownQuoteBlock quote:
                    RenderQuote(quote, indent);
                    MoveDown(ParagraphGap);
                    break;

                case MarkdownListBlock list:
                    RenderList(list, indent);
                    MoveDown(SmallGap);
                    break;

                case MarkdownHorizontalRuleBlock:
                    DrawRule(indent);
                    MoveDown(ParagraphGap);
                    break;

                case MarkdownCodeBlock code:
                    DrawCodeBlock(code.Code, indent);
                    MoveDown(ParagraphGap);
                    break;

                case MarkdownTableBlock table:
                    RenderTable(table, indent);
                    MoveDown(ParagraphGap);
                    break;

                case MarkdownImageBlock image:
                    DrawParagraph(GetImageText(image), TextStyle.Caption, indent);
                    MoveDown(ParagraphGap);
                    break;

                case MarkdownDiagramBlock diagram:
                    DrawParagraph(GetDiagramText(diagram), TextStyle.Caption, indent);
                    DrawCodeBlock(diagram.Source, indent);
                    MoveDown(ParagraphGap);
                    break;
            }
        }

        private void RenderQuote(MarkdownQuoteBlock quote, float indent)
        {
            EnsurePageSpace(18);
            using var paint = new SKPaint { Color = RuleColor, IsAntialias = true };
            _canvas!.DrawRect(new SKRect(MarginX + indent, _y, MarginX + indent + 3, Math.Min(PageHeight - MarginY, _y + 36)), paint);

            foreach (var block in quote.Blocks)
            {
                RenderBlock(block, indent + 14);
            }
        }

        private void RenderList(MarkdownListBlock list, float indent)
        {
            for (var index = 0; index < list.Items.Count; index++)
            {
                var marker = list.IsOrdered ? $"{index + 1}." : "\u2022";
                var item = list.Items[index];
                var itemText = string.Join(
                    " ",
                    item.Blocks
                        .Select(ExtractPlainText)
                        .Where(static text => !string.IsNullOrWhiteSpace(text)));

                DrawParagraph($"{marker} {itemText}", TextStyle.Body, indent);
            }
        }

        private void RenderTable(MarkdownTableBlock table, float indent)
        {
            var columnCount = GetTableColumnCount(table);
            if (columnCount == 0)
            {
                return;
            }

            var x = MarginX + indent;
            var width = AvailableWidth(indent);
            var columnWidth = width / columnCount;

            if (table.Header.Count > 0)
            {
                DrawTableRow(
                    NormalizeTableRow(table.Header, columnCount),
                    TextStyle.TableHeader,
                    x,
                    columnWidth,
                    isHeader: true);
            }

            foreach (var row in table.Rows)
            {
                DrawTableRow(
                    NormalizeTableRow(row, columnCount),
                    TextStyle.Body,
                    x,
                    columnWidth,
                    isHeader: false);
            }
        }

        private void DrawTableRow(
            string[] cells,
            TextStyle style,
            float x,
            float columnWidth,
            bool isHeader)
        {
            using var textPaint = new SKPaint { Color = style.Color, IsAntialias = true };
            using var borderPaint = new SKPaint
            {
                Color = RuleColor,
                StrokeWidth = 1,
                Style = SKPaintStyle.Stroke,
                IsAntialias = false
            };
            using var backgroundPaint = new SKPaint { Color = TableHeaderBackgroundColor, IsAntialias = false };

            var availableTextWidth = Math.Max(24, columnWidth - TableCellPadding * 2);
            var wrappedCells = cells
                .Select(cell => WrapCellText(cell, style, textPaint, availableTextWidth))
                .ToArray();

            var maxLineCount = Math.Max(1, wrappedCells.Max(static lines => lines.Count));
            var rowHeight = Math.Max(24, maxLineCount * style.LineHeight + TableCellPadding * 2);

            EnsurePageSpace(rowHeight);

            var rowTop = _y;
            for (var columnIndex = 0; columnIndex < cells.Length; columnIndex++)
            {
                var cellLeft = x + columnIndex * columnWidth;
                var cellBounds = new SKRect(cellLeft, rowTop, cellLeft + columnWidth, rowTop + rowHeight);
                if (isHeader)
                {
                    _canvas!.DrawRect(cellBounds, backgroundPaint);
                }

                _canvas!.DrawRect(cellBounds, borderPaint);

                var baseline = rowTop + TableCellPadding - GetFontAscent(style);
                foreach (var line in wrappedCells[columnIndex])
                {
                    DrawTextWithFallback(
                        _canvas!,
                        line,
                        cellLeft + TableCellPadding,
                        baseline,
                        style,
                        textPaint);
                    baseline += style.LineHeight;
                }
            }

            _y += rowHeight;
        }

        private void DrawTitle(string title)
        {
            DrawParagraph(title, TextStyle.Title, indent: 0);
            MoveDown(10);
            DrawRule(indent: 0);
            MoveDown(16);
        }

        private void DrawParagraph(string text, TextStyle style, float indent)
        {
            var normalized = string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

            if (normalized.Length == 0)
            {
                return;
            }

            using var paint = new SKPaint { Color = style.Color, IsAntialias = true };

            foreach (var sourceLine in normalized.Split('\n'))
            {
                foreach (var wrappedLine in WrapWords(sourceLine, style, paint, AvailableWidth(indent)))
                {
                    DrawTextLine(new PdfTextLine(wrappedLine, style, indent), paint);
                }
            }
        }

        private void DrawCodeBlock(string code, float indent)
        {
            var normalized = (code ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .TrimEnd('\n');

            if (normalized.Length == 0)
            {
                return;
            }

            using var textPaint = new SKPaint { Color = TextStyle.Code.Color, IsAntialias = true };
            using var backgroundPaint = new SKPaint { Color = CodeBackgroundColor, IsAntialias = false };

            foreach (var sourceLine in normalized.Split('\n'))
            {
                foreach (var wrappedLine in WrapCharacters(sourceLine, TextStyle.Code, textPaint, AvailableWidth(indent) - 12))
                {
                    EnsurePageSpace(TextStyle.Code.LineHeight + 6);
                    var top = _y;
                    var left = MarginX + indent;
                    var right = PageWidth - MarginX;
                    _canvas!.DrawRect(new SKRect(left, top - 2, right, top + TextStyle.Code.LineHeight + 2), backgroundPaint);
                    DrawTextLine(new PdfTextLine(wrappedLine, TextStyle.Code, indent + 6), textPaint);
                }
            }
        }

        private void DrawTextLine(PdfTextLine line)
        {
            using var typeface = SKTypeface.FromFamilyName(line.Style.FamilyName, line.Style.FontStyle);
            using var font = new SKFont(typeface ?? SKTypeface.Default, line.Style.FontSize);
            using var paint = new SKPaint { Color = line.Style.Color, IsAntialias = true };
            DrawTextLine(line, font, paint);
        }

        private void DrawTextLine(PdfTextLine line, SKPaint paint)
        {
            using var typeface = SKTypeface.FromFamilyName(line.Style.FamilyName, line.Style.FontStyle);
            using var font = new SKFont(typeface ?? SKTypeface.Default, line.Style.FontSize);
            DrawTextLine(line, font, paint);
        }

        private void DrawTextLine(PdfTextLine line, SKFont font, SKPaint paint)
        {
            EnsurePageSpace(line.Style.LineHeight);
            var metrics = font.Metrics;
            var x = MarginX + line.Indent;
            var baseline = _y - metrics.Ascent;
            DrawTextWithFallback(_canvas!, line.Text, x, baseline, line.Style, paint);
            _y += line.Style.LineHeight;
        }

        private void DrawRule(float indent)
        {
            EnsurePageSpace(8);
            using var paint = new SKPaint { Color = RuleColor, StrokeWidth = 1, IsAntialias = true };
            var y = _y + 4;
            _canvas!.DrawLine(MarginX + indent, y, PageWidth - MarginX, y, paint);
            _y += 8;
        }

        private void MoveDown(float value)
        {
            EnsurePageSpace(value);
            _y += value;
        }

        private void EnsurePageSpace(float height)
        {
            if (_canvas is null)
            {
                BeginPage();
                return;
            }

            if (_y + height <= PageHeight - MarginY)
            {
                return;
            }

            EndPage();
            BeginPage();
        }

        private void BeginPage()
        {
            _pageNumber++;
            _canvas = _document.BeginPage(PageWidth, PageHeight);
            _y = MarginY;

            using var background = new SKPaint { Color = SKColors.White, IsAntialias = false };
            _canvas.DrawRect(new SKRect(0, 0, PageWidth, PageHeight), background);

            if (_pageNumber > 1)
            {
                DrawPageHeader();
            }
        }

        private void EndPage()
        {
            if (_canvas is null)
            {
                return;
            }

            DrawPageFooter();
            _document.EndPage();
            _canvas = null;
        }

        private void DrawPageHeader()
        {
            using var typeface = SKTypeface.FromFamilyName(TextStyle.Caption.FamilyName, TextStyle.Caption.FontStyle);
            using var font = new SKFont(typeface ?? SKTypeface.Default, TextStyle.Caption.FontSize);
            using var paint = new SKPaint { Color = MutedColor, IsAntialias = true };

            var header = _title;
            var metrics = font.Metrics;
            DrawTextWithFallback(_canvas!, header, MarginX, _y - metrics.Ascent, TextStyle.Caption, paint);
            _y += 26;
        }

        private void DrawPageFooter()
        {
            using var typeface = SKTypeface.FromFamilyName(TextStyle.Caption.FamilyName, TextStyle.Caption.FontStyle);
            using var font = new SKFont(typeface ?? SKTypeface.Default, TextStyle.Caption.FontSize);
            using var paint = new SKPaint { Color = MutedColor, IsAntialias = true };

            var footer = _pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var width = MeasureTextWithFallback(footer, TextStyle.Caption, paint);
            DrawTextWithFallback(_canvas!, footer, PageWidth - MarginX - width, PageHeight - 28, TextStyle.Caption, paint);
        }

        private static float AvailableWidth(float indent)
            => Math.Max(80, PageWidth - MarginX * 2 - indent);

        private static int GetTableColumnCount(MarkdownTableBlock table)
        {
            var columnCount = table.Header.Count;
            foreach (var row in table.Rows)
            {
                columnCount = Math.Max(columnCount, row.Count);
            }

            return columnCount;
        }

        private static string[] NormalizeTableRow(
            IReadOnlyList<MarkdownTableCell> cells,
            int columnCount)
        {
            var result = new string[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                result[columnIndex] = columnIndex < cells.Count
                    ? ExtractPlainText(cells[columnIndex].Inlines)
                    : string.Empty;
            }

            return result;
        }

        private static List<string> WrapCellText(string text, TextStyle style, SKPaint paint, float maxWidth)
        {
            var lines = new List<string>();
            var normalized = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim();

            if (normalized.Length == 0)
            {
                lines.Add(string.Empty);
                return lines;
            }

            foreach (var sourceLine in normalized.Split('\n'))
            {
                var wrapped = WrapWords(sourceLine, style, paint, maxWidth);
                if (wrapped.Count == 0)
                {
                    lines.Add(string.Empty);
                }
                else
                {
                    lines.AddRange(wrapped);
                }
            }

            return lines;
        }

        private static float GetFontAscent(TextStyle style)
        {
            using var typeface = SKTypeface.FromFamilyName(style.FamilyName, style.FontStyle);
            using var font = new SKFont(typeface ?? SKTypeface.Default, style.FontSize);
            return font.Metrics.Ascent;
        }

        private static List<string> WrapWords(string line, TextStyle style, SKPaint paint, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return [];
            }

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = string.Empty;

            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (MeasureTextWithFallback(candidate, style, paint) <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                }

                if (MeasureTextWithFallback(word, style, paint) <= maxWidth)
                {
                    current = word;
                }
                else
                {
                    var chunks = WrapCharacters(word, style, paint, maxWidth);
                    lines.AddRange(chunks.Take(Math.Max(0, chunks.Count - 1)));
                    current = chunks.Count == 0 ? string.Empty : chunks[^1];
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            return lines;
        }

        private static List<string> WrapCharacters(string line, TextStyle style, SKPaint paint, float maxWidth)
        {
            if (string.IsNullOrEmpty(line))
            {
                return [string.Empty];
            }

            var lines = new List<string>();
            var current = new StringBuilder();

            foreach (var c in line)
            {
                var candidate = current.ToString() + c;
                if (candidate.Length == 1 || MeasureTextWithFallback(candidate, style, paint) <= maxWidth)
                {
                    current.Append(c);
                    continue;
                }

                lines.Add(current.ToString());
                current.Clear();
                current.Append(c);
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
            }

            return lines;
        }

        private static void DrawTextWithFallback(SKCanvas canvas, string text, float x, float baseline, TextStyle style, SKPaint paint)
        {
            foreach (var textElement in EnumerateTextElements(text))
            {
                if (TryGetNotoEmojiAssetBytes(textElement, out var emojiBytes))
                {
                    DrawEmojiImage(canvas, emojiBytes, x, baseline, style);
                    x += GetEmojiImageSize(style);
                    continue;
                }

                using var typeface = ResolveTypeface(textElement, style);
                using var font = new SKFont(typeface ?? SKTypeface.Default, style.FontSize);
                canvas.DrawText(textElement, x, baseline, font, paint);
                x += font.MeasureText(textElement, paint);
            }
        }

        private static float MeasureTextWithFallback(string text, TextStyle style, SKPaint paint)
        {
            var width = 0f;
            foreach (var textElement in EnumerateTextElements(text))
            {
                if (TryGetNotoEmojiAssetBytes(textElement, out _))
                {
                    width += GetEmojiImageSize(style);
                    continue;
                }

                using var typeface = ResolveTypeface(textElement, style);
                using var font = new SKFont(typeface ?? SKTypeface.Default, style.FontSize);
                width += font.MeasureText(textElement, paint);
            }

            return width;
        }

        private static void DrawEmojiImage(SKCanvas canvas, byte[] bytes, float x, float baseline, TextStyle style)
        {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null)
            {
                return;
            }

            var size = GetEmojiImageSize(style);
            var top = baseline - size + style.FontSize * 0.12f;
            var destination = new SKRect(x, top, x + size, top + size);
            using var image = SKImage.FromBitmap(bitmap);
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            canvas.DrawImage(image, destination, sampling);
        }

        private static float GetEmojiImageSize(TextStyle style)
            => MathF.Max(style.FontSize, style.LineHeight * 0.9f);

        private static bool TryGetNotoEmojiAssetBytes(string textElement, out byte[] bytes)
        {
            foreach (var fileName in GetNotoEmojiAssetFileNameCandidates(textElement))
            {
                var cached = NotoEmojiAssetCache.GetOrAdd(fileName, LoadNotoEmojiAssetBytes);
                if (cached.Length == 0)
                {
                    continue;
                }

                bytes = cached;
                return true;
            }

            bytes = [];
            return false;
        }

        private static byte[] LoadNotoEmojiAssetBytes(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "NotoEmoji", "128", fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : [];
        }

        private static IEnumerable<string> GetNotoEmojiAssetFileNameCandidates(string textElement)
        {
            var codePoints = textElement
                .EnumerateRunes()
                .Select(static rune => rune.Value)
                .ToArray();

            if (codePoints.Length == 0)
            {
                yield break;
            }

            yield return ToNotoEmojiFileName(codePoints);

            var withoutVariationSelectors = codePoints
                .Where(static codePoint => codePoint is not 0xFE0E and not 0xFE0F)
                .ToArray();

            if (withoutVariationSelectors.Length != codePoints.Length && withoutVariationSelectors.Length > 0)
            {
                yield return ToNotoEmojiFileName(withoutVariationSelectors);
            }
        }

        private static string ToNotoEmojiFileName(IEnumerable<int> codePoints)
            => "emoji_u" + string.Join("_", codePoints.Select(static codePoint => codePoint.ToString("x", CultureInfo.InvariantCulture))) + ".png";

        private static SKTypeface? ResolveTypeface(string textElement, TextStyle style)
        {
            var families = IsLikelyEmojiTextElement(textElement)
                ? EmojiFallbackFamilies.Prepend(style.FamilyName)
                : SymbolFallbackFamilies.Prepend(style.FamilyName);

            foreach (var family in families)
            {
                var typeface = SKTypeface.FromFamilyName(family, style.FontStyle);
                if (typeface is null)
                {
                    continue;
                }

                using var font = new SKFont(typeface, style.FontSize);
                if (font.ContainsGlyphs(textElement))
                {
                    return typeface;
                }

                typeface.Dispose();
            }

            var codePoint = GetFirstCodePoint(textElement);
            return codePoint is null
                ? null
                : SKFontManager.Default.MatchCharacter(style.FamilyName, style.FontStyle, [], codePoint.Value);
        }

        private static IEnumerable<string> EnumerateTextElements(string text)
        {
            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                yield return enumerator.GetTextElement();
            }
        }

        private static bool IsLikelyEmojiTextElement(string textElement)
        {
            foreach (var rune in textElement.EnumerateRunes())
            {
                if (IsEmojiCodePoint(rune.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static int? GetFirstCodePoint(string textElement)
        {
            foreach (var rune in textElement.EnumerateRunes())
            {
                return rune.Value;
            }

            return null;
        }

        private static bool IsEmojiCodePoint(int codePoint)
            => codePoint is >= 0x1F000 and <= 0x1FAFF
               || codePoint is >= 0x2600 and <= 0x27BF
               || codePoint is >= 0x2300 and <= 0x23FF;

        private static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
        {
            var builder = new StringBuilder();
            foreach (var inline in inlines)
            {
                AppendPlainText(inline, builder);
            }

            return builder.ToString();
        }

        private static string ExtractPlainText(MarkdownBlock block) => block switch
        {
            MarkdownHeadingBlock heading => ExtractPlainText(heading.Inlines),
            MarkdownParagraphBlock paragraph => ExtractPlainText(paragraph.Inlines),
            MarkdownQuoteBlock quote => string.Join(" ", quote.Blocks.Select(ExtractPlainText)),
            MarkdownListBlock list => string.Join(
                " ",
                list.Items.Select(static item => string.Join(" ", item.Blocks.Select(ExtractPlainText)))),
            MarkdownCodeBlock code => code.Code,
            MarkdownTableBlock table => string.Join(
                " ",
                table.Rows.Select(static row => string.Join(" | ", row.Select(cell => ExtractPlainText(cell.Inlines))))),
            MarkdownImageBlock image => GetImageText(image),
            MarkdownDiagramBlock diagram => GetDiagramText(diagram),
            _ => string.Empty
        };

        private static void AppendPlainText(MarkdownInline inline, StringBuilder builder)
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
                case MarkdownImageInline image:
                    builder.Append(GetImageText(image));
                    break;
                case MarkdownLinkInline link:
                    AppendPlainText(link.Inlines, builder);
                    if (link.Inlines.Count == 0 && !string.IsNullOrWhiteSpace(link.Url))
                    {
                        builder.Append(link.Url);
                    }
                    break;
                case MarkdownLineBreakInline:
                    builder.AppendLine();
                    break;
            }
        }

        private static void AppendPlainText(IReadOnlyList<MarkdownInline> inlines, StringBuilder builder)
        {
            foreach (var inline in inlines)
            {
                AppendPlainText(inline, builder);
            }
        }

        private static string GetImageText(MarkdownImageInline image)
            => string.IsNullOrWhiteSpace(image.AltText)
                ? string.IsNullOrWhiteSpace(image.Url) ? "[image]" : $"[image: {image.Url}]"
                : image.AltText;

        private static string GetImageText(MarkdownImageBlock image)
            => string.IsNullOrWhiteSpace(image.AltText)
                ? string.IsNullOrWhiteSpace(image.Url) ? "[image]" : $"[image: {image.Url}]"
                : $"[image: {image.AltText}]";

        private static string GetDiagramText(MarkdownDiagramBlock diagram)
            => string.IsNullOrWhiteSpace(diagram.Title)
                ? $"[{diagram.Kind} diagram]"
                : $"[{diagram.Kind} diagram: {diagram.Title}]";
    }

    private sealed record PdfTextLine(string Text, TextStyle Style, float Indent);

    private readonly record struct TextStyle(
        string FamilyName,
        float FontSize,
        float LineHeight,
        SKColor Color,
        SKFontStyle FontStyle)
    {
        public static TextStyle Title { get; } = new("Arial", 24, 31, new SKColor(17, 24, 39), SKFontStyle.Bold);
        public static TextStyle Body { get; } = new("Arial", 11.5f, 17, new SKColor(34, 39, 46), SKFontStyle.Normal);
        public static TextStyle Caption { get; } = new("Arial", 10, 14, new SKColor(87, 96, 106), SKFontStyle.Italic);
        public static TextStyle Code { get; } = new("Consolas", 9.5f, 14, new SKColor(36, 41, 47), SKFontStyle.Normal);
        public static TextStyle TableHeader { get; } = new("Arial", 11.5f, 17, new SKColor(34, 39, 46), SKFontStyle.Bold);

        public static TextStyle ForHeading(int level)
            => Math.Clamp(level, 1, 6) switch
            {
                1 => new TextStyle("Arial", 22, 28, new SKColor(17, 24, 39), SKFontStyle.Bold),
                2 => new TextStyle("Arial", 18, 24, new SKColor(17, 24, 39), SKFontStyle.Bold),
                3 => new TextStyle("Arial", 15.5f, 21, new SKColor(17, 24, 39), SKFontStyle.Bold),
                _ => new TextStyle("Arial", 13, 18, new SKColor(17, 24, 39), SKFontStyle.Bold)
            };
    }
}
