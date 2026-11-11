using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using MarkdigMarkdown = Markdig.Markdown;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkMello.Application.Abstractions;
using MarkMello.Application.Diagrams;
using MarkMello.Domain;
using CodeBlock = Markdig.Syntax.CodeBlock;

namespace MarkMello.Infrastructure.Markdown;


public sealed partial class MarkdigMarkdownDocumentRenderer
{
    private static MarkdownBlock WithSourceSpan(MarkdownBlock block, Block sourceBlock, string source)
        => WithSourceSpan(block, CreateSourceSpan(sourceBlock, source));

    private static MarkdownBlock WithSourceSpan(MarkdownBlock block, MarkdownSourceSpan? sourceSpan)
        => sourceSpan is null ? block : block with { SourceSpan = sourceSpan };

    private static MarkdownSourceSpan? CreateSourceSpan(Block block, string source)
    {
        int? startLine = block.Line >= 0 ? block.Line : null;

        // У setext-заголовка («Текст» + строка из ---/===) Markdig держит в Line строку
        // подчёркивания, а не строку текста; Span же начинается с текста. Без поправки
        // оглавление ведёт курсор на подчёркивание, а конец span уезжает на строку дальше.
        // Span покрывает текст и подчёркивание, поэтому первая строка текста получается
        // вычитанием числа переводов строки внутри span (без прохода по исходнику с нуля).
        if (startLine is not null && block is HeadingBlock { IsSetext: true })
        {
            startLine = Math.Max(0, startLine.Value - CountSpanLineBreaks(block, source));
        }

        int? endLine = startLine is null
            ? null
            : startLine.Value + CountSourceLineBreaks(block, source);

        if (block is ContainerBlock container)
        {
            foreach (var child in container)
            {
                var childSpan = CreateSourceSpan(child, source);
                if (childSpan is null)
                {
                    continue;
                }

                startLine = startLine is null
                    ? childSpan.Value.StartLine
                    : Math.Min(startLine.Value, childSpan.Value.StartLine);
                endLine = endLine is null
                    ? childSpan.Value.EndLine
                    : Math.Max(endLine.Value, childSpan.Value.EndLine);
            }
        }

        return startLine is null
            ? null
            : new MarkdownSourceSpan(startLine.Value, endLine ?? startLine.Value);
    }

    /// <summary>
    /// Сколько переводов строки блок занимает в исходнике.
    ///
    /// Считается по <see cref="MarkdownObject.Span"/> исходного текста, а не по
    /// <see cref="LeafBlock.Lines"/>: у абзацев и заголовков Markdig очищает
    /// Lines после разбора inline-содержимого, а у фенсед-блоков туда не входят
    /// строки ограждения. И то и другое давало span короче реального, из-за чего
    /// синхронизация скролла промахивалась мимо конца блока.
    /// </summary>
    private static int CountSourceLineBreaks(Block block, string source)
        => Math.Max(CountSpanLineBreaks(block, source), CountFenceLineBreaks(block));

    private static int CountSpanLineBreaks(Block block, string source)
    {
        var span = block.Span;
        if (span.Start < 0 || span.End < span.Start || span.End >= source.Length)
        {
            return 0;
        }

        var lineBreaks = 0;
        for (var index = span.Start; index <= span.End; index++)
        {
            if (source[index] == '\n')
            {
                lineBreaks++;
            }
        }

        // Завершающий перевод строки принадлежит последней строке блока,
        // а не следующей за ним.
        return source[span.End] == '\n' ? Math.Max(0, lineBreaks - 1) : lineBreaks;
    }

    /// <summary>
    /// Запасной подсчёт для фенсед-блока: у незакрытого ограждения Markdig не
    /// растягивает Span за строку открытия, а такой блок нормален во время
    /// набора текста.
    /// </summary>
    private static int CountFenceLineBreaks(Block block)
    {
        if (block is not FencedCodeBlock fenced)
        {
            return 0;
        }

        var contentLines = Math.Max(0, fenced.Lines.Count - 1);
        var closingFence = fenced.ClosingFencedCharCount > 0 ? 1 : 0;
        return 1 + contentLines + closingFence;
    }
}
