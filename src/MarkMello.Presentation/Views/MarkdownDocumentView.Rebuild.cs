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
    /// <summary>
    /// Пересобирает preview, переиспользуя контролы блоков, которые не
    /// изменились с прошлого рендера.
    ///
    /// Полная пересборка стоит порядка полусекунды на документе в сотню
    /// килобайт, а правка обычно затрагивает один блок, поэтому неизменившиеся
    /// блоки остаются в дереве как есть — им не нужны ни повторное построение,
    /// ни повторный layout.
    /// </summary>
    private void Rebuild()
    {
        CancelPendingQuickEditTimer();
        _quickInlineEditingController.CommitQuickEditor();

        DocumentRenderInvalidated?.Invoke(this, EventArgs.Empty);
        ResetPointerState();

        var document = Document;
        _textMap = document is null ? MarkdownDocumentTextMap.Empty : MarkdownDocumentTextMap.Create(document);
        ClearSelection();

        var generation = ++_renderGeneration;
        _hasPendingRenderedNotification = false;

        var reusable = CreateReusableBlockIndex();
        var previous = _builtBlocks;
        var rebuilt = new List<BuiltTopLevelBlock>(document?.Blocks.Count ?? 0);

        _selectionFragments.Clear();
        _selectionFragmentPaths.Clear();
        _headingAnchorRegistrations.Clear();
        _sourceLineAnchors.Clear();

        if (document is not null)
        {
            for (var index = 0; index < document.Blocks.Count; index++)
            {
                var block = document.Blocks[index];
                rebuilt.Add(TryReuseBlock(reusable, block, index) ?? BuildTopLevelBlock(block, index));
            }
        }

        _builtBlocks = rebuilt;
        DisposeReplacedBlocks(previous, rebuilt);
        SyncRootChildren(rebuilt);
        RebuildHeadingAnchorIndex();

        // Re-apply the active query against the rebuilt fragments, and do it
        // even when the document is empty or null so match counts do not go
        // stale after the content disappears.
        RebuildSearchMatches(keepCurrentIndex: true);

        if (document is null || document.Blocks.Count == 0)
        {
            return;
        }

        QueueDocumentRenderedNotification(generation);
    }

    /// <summary>
    /// Индексирует блоки прошлого рендера по содержимому. Каждая запись может
    /// быть выдана один раз — один и тот же контрол не может стоять в дереве дважды.
    /// </summary>
    private Dictionary<MarkdownBlock, Queue<BuiltTopLevelBlock>> CreateReusableBlockIndex()
    {
        var index = new Dictionary<MarkdownBlock, Queue<BuiltTopLevelBlock>>(
            _builtBlocks.Count,
            MarkdownBlockStructuralComparer.Instance);

        foreach (var built in _builtBlocks)
        {
            if (!index.TryGetValue(built.Block, out var bucket))
            {
                bucket = new Queue<BuiltTopLevelBlock>();
                index[built.Block] = bucket;
            }

            bucket.Enqueue(built);
        }

        return index;
    }

    private BuiltTopLevelBlock? TryReuseBlock(
        Dictionary<MarkdownBlock, Queue<BuiltTopLevelBlock>> reusable,
        MarkdownBlock block,
        int index)
    {
        if (!reusable.TryGetValue(block, out var bucket) || bucket.Count == 0)
        {
            return null;
        }

        var built = bucket.Dequeue();
        var path = $"b{index}";

        // Содержимое то же, но позиция в документе могла измениться: обновляем
        // текстовые диапазоны выделения и исходные строки для scroll sync.
        for (var i = 0; i < built.Fragments.Length; i++)
        {
            var fragment = built.Fragments[i];
            var fragmentPath = path + built.FragmentRelativePaths[i];
            if (_textMap.TryGetFragment(fragmentPath, out var mapped))
            {
                fragment.DocumentRange = mapped.Range;
            }

            _selectionFragments.Add(fragment);
            _selectionFragmentPaths.Add(fragmentPath);
        }

        var startLine = block.SourceSpan?.StartLine ?? 0;
        foreach (var anchor in built.SourceAnchors)
        {
            _sourceLineAnchors.Add(new MarkdownSourceLineVisualAnchor(
                anchor.Control,
                new MarkdownSourceSpan(startLine + anchor.RelativeStartLine, startLine + anchor.RelativeEndLine)));
        }

        foreach (var heading in built.HeadingAnchors)
        {
            _headingAnchorRegistrations.Add(heading);
        }

        built.Block = block;
        return built;
    }

    private BuiltTopLevelBlock BuildTopLevelBlock(MarkdownBlock block, int index)
    {
        var path = $"b{index}";
        var fragmentStart = _selectionFragments.Count;
        var anchorStart = _sourceLineAnchors.Count;
        var headingStart = _headingAnchorRegistrations.Count;

        var control = BuildBlock(block, path, nested: false);

        var fragmentCount = _selectionFragments.Count - fragmentStart;
        var fragments = new MarkdownDocumentSelectionFragmentBase[fragmentCount];
        var relativePaths = new string[fragmentCount];
        for (var i = 0; i < fragmentCount; i++)
        {
            fragments[i] = _selectionFragments[fragmentStart + i];
            relativePaths[i] = _selectionFragmentPaths[fragmentStart + i][path.Length..];
        }

        var startLine = block.SourceSpan?.StartLine ?? 0;
        var anchorCount = _sourceLineAnchors.Count - anchorStart;
        var anchors = new BuiltSourceAnchor[anchorCount];
        for (var i = 0; i < anchorCount; i++)
        {
            var anchor = _sourceLineAnchors[anchorStart + i];
            anchors[i] = new BuiltSourceAnchor(
                anchor.Control,
                anchor.SourceSpan.StartLine - startLine,
                anchor.SourceSpan.EndLine - startLine);
        }

        return new BuiltTopLevelBlock
        {
            Block = block,
            Control = control,
            Fragments = fragments,
            FragmentRelativePaths = relativePaths,
            SourceAnchors = anchors,
            HeadingAnchors = _headingAnchorRegistrations
                .GetRange(headingStart, _headingAnchorRegistrations.Count - headingStart)
                .ToArray()
        };
    }

    private static void DisposeReplacedBlocks(
        List<BuiltTopLevelBlock> previous,
        List<BuiltTopLevelBlock> current)
    {
        if (previous.Count == 0)
        {
            return;
        }

        var kept = new HashSet<BuiltTopLevelBlock>(current);
        foreach (var built in previous)
        {
            if (kept.Contains(built))
            {
                continue;
            }

            foreach (var fragment in built.Fragments)
            {
                fragment.Dispose();
            }
        }
    }

    /// <summary>
    /// Приводит детей корневого стека к целевому списку, не трогая уже стоящие
    /// на своих местах контролы: удаление и повторная вставка означали бы
    /// detach/attach со всей повторной стилизацией, ради избавления от которой
    /// переиспользование и делается.
    /// </summary>
    private void SyncRootChildren(List<BuiltTopLevelBlock> blocks)
    {
        var desired = new HashSet<Control>(blocks.Count);
        foreach (var built in blocks)
        {
            desired.Add(built.Control);
        }

        for (var index = _root.Children.Count - 1; index >= 0; index--)
        {
            if (!desired.Contains(_root.Children[index]))
            {
                _root.Children.RemoveAt(index);
            }
        }

        for (var index = 0; index < blocks.Count; index++)
        {
            var control = blocks[index].Control;
            if (index < _root.Children.Count && ReferenceEquals(_root.Children[index], control))
            {
                continue;
            }

            var existing = _root.Children.IndexOf(control);
            if (existing >= 0)
            {
                _root.Children.Move(existing, index);
            }
            else
            {
                _root.Children.Insert(index, control);
            }
        }
    }
}
