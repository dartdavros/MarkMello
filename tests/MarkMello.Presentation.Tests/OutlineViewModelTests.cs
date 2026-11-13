using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

public sealed class OutlineViewModelTests
{
    private static MarkdownHeadingBlock H(int level, string text, int line)
        => new(level, [new MarkdownTextInline(text)]) { SourceSpan = new MarkdownSourceSpan(line) };

    private static readonly RenderedMarkdownDocument Guide = new(
    [
        H(1, "Guide", 0), H(2, "Install", 2), H(3, "Windows", 4), H(3, "Linux", 8), H(2, "Run", 12)
    ]);

    private static OutlineViewModel Create(OutlinePreferences? prefs = null)
    {
        var vm = new OutlineViewModel();
        vm.Update(Guide, prefs ?? OutlinePreferences.Default);
        return vm;
    }

    [Fact]
    public void UpdateBuildsTreeAndAppliesFontSizes()
    {
        var vm = Create();

        var root = Assert.Single(vm.Items);
        Assert.False(vm.IsEmpty);
        Assert.Equal(15, root.FontSize);
        Assert.Equal(13, root.Children[0].Children[0].FontSize);
        Assert.Equal(13 * 1.5, root.Children[0].Children[0].LineBoxHeight, 3);
    }

    [Fact]
    public void EmptyDocumentSetsIsEmpty()
    {
        var vm = new OutlineViewModel();
        vm.Update(RenderedMarkdownDocument.Empty, OutlinePreferences.Default);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void CurrentLineActivatesLastHeadingAboveIt()
    {
        var vm = Create();

        vm.SetCurrentSourceLine(9);

        var linux = vm.Items[0].Children[0].Children[1];
        Assert.True(linux.IsActive);
        Assert.Single(Flatten(vm.Items), i => i.IsActive);
    }

    [Fact]
    public void CollapsedAncestorIsActivatedInsteadOfHiddenChild()
    {
        var vm = Create();
        var install = vm.Items[0].Children[0];

        vm.SetCurrentSourceLine(9);
        install.IsExpanded = false;

        Assert.True(install.IsActive);
        Assert.False(install.Children[1].IsActive);
    }

    [Fact]
    public void LineBeforeFirstHeadingActivatesNothing()
    {
        var vm = new OutlineViewModel();
        vm.Update(new RenderedMarkdownDocument([H(1, "A", 5)]), OutlinePreferences.Default);

        vm.SetCurrentSourceLine(2);

        Assert.DoesNotContain(Flatten(vm.Items), i => i.IsActive);
    }

    [Fact]
    public void RebuildKeepsCollapsedStateForSameHeading()
    {
        var vm = Create();
        vm.Items[0].Children[0].IsExpanded = false;

        vm.Update(Guide, OutlinePreferences.Default);

        Assert.False(vm.Items[0].Children[0].IsExpanded);
        Assert.True(vm.Items[0].Children[1].IsExpanded);
    }

    [Fact]
    public void FoldStateIsKeptPerDocumentKey()
    {
        var keyA = new object();
        var keyB = new object();
        var vm = new OutlineViewModel();
        vm.Update(Guide, OutlinePreferences.Default, keyA);
        vm.Items[0].Children[0].IsExpanded = false;

        vm.Update(Guide, OutlinePreferences.Default, keyB);
        Assert.All(Flatten(vm.Items), i => Assert.True(i.IsExpanded));

        vm.Update(Guide, OutlinePreferences.Default, keyA);
        Assert.False(vm.Items[0].Children[0].IsExpanded);
        Assert.True(vm.Items[0].Children[1].IsExpanded);
    }

    [Fact]
    public void FoldChangesAppliedByUpdateAreNotRecorded()
    {
        var keyA = new object();
        var keyB = new object();
        var vm = new OutlineViewModel();
        vm.Update(Guide, OutlinePreferences.Default, keyA);
        vm.Items[0].Children[0].IsExpanded = false;

        // B shares no state with A; the transient rebuild must not overwrite A's saved folds.
        vm.Update(Guide, OutlinePreferences.Default, keyB);
        vm.Update(Guide, OutlinePreferences.Default, keyA);

        Assert.False(vm.Items[0].Children[0].IsExpanded);
    }

    [Fact]
    public void ExpandingAgainRemovesTheSavedFold()
    {
        var key = new object();
        var vm = new OutlineViewModel();
        vm.Update(Guide, OutlinePreferences.Default, key);
        vm.Items[0].Children[0].IsExpanded = false;
        vm.Items[0].Children[0].IsExpanded = true;

        vm.Update(RenderedMarkdownDocument.Empty, OutlinePreferences.Default, key);
        vm.Update(Guide, OutlinePreferences.Default, key);

        Assert.True(vm.Items[0].Children[0].IsExpanded);
    }

    [Fact]
    public void HeadingsWithTheSameTextFoldIndependently()
    {
        var document = new RenderedMarkdownDocument(
        [
            H(1, "Doc", 0), H(2, "Example", 1), H(3, "Deep", 2), H(2, "Example", 4), H(3, "Deep", 5)
        ]);
        var key = new object();
        var vm = new OutlineViewModel();
        vm.Update(document, OutlinePreferences.Default, key);

        vm.Items[0].Children[1].IsExpanded = false;
        Assert.True(vm.Items[0].Children[0].IsExpanded);

        var shifted = new RenderedMarkdownDocument(
        [
            H(1, "Doc", 0), H(2, "Example", 3), H(3, "Deep", 4), H(2, "Example", 8), H(3, "Deep", 9), H(2, "Tail", 12)
        ]);
        vm.Update(shifted, OutlinePreferences.Default, key);

        Assert.True(vm.Items[0].Children[0].IsExpanded);
        Assert.False(vm.Items[0].Children[1].IsExpanded);
    }

    [Fact]
    public void SameStructureIsPatchedInPlaceAndSourceLinesFollow()
    {
        var key = new object();
        var vm = new OutlineViewModel();
        vm.Update(Guide, OutlinePreferences.Default, key);
        var before = Flatten(vm.Items).ToList();
        var shifted = new RenderedMarkdownDocument(
        [
            H(1, "Guide", 10), H(2, "Install", 12), H(3, "Windows", 14), H(3, "Linux", 18), H(2, "Run", 22)
        ]);
        vm.SetCurrentSourceLine(19);

        vm.Update(shifted, OutlinePreferences.Default, key);

        var after = Flatten(vm.Items).ToList();
        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Same(before[i], after[i]);
        }

        Assert.Equal([10, 12, 14, 18, 22], after.Select(i => i.SourceLine));
        Assert.True(after[3].IsActive);
    }

    [Fact]
    public void DifferentStructureRebuildsTheTree()
    {
        var key = new object();
        var vm = new OutlineViewModel();
        vm.Update(Guide, OutlinePreferences.Default, key);
        var first = vm.Items[0];

        vm.Update(new RenderedMarkdownDocument([H(1, "Guide", 0), H(2, "Install", 2)]), OutlinePreferences.Default, key);

        Assert.NotSame(first, vm.Items[0]);
        Assert.Equal(2, Flatten(vm.Items).Count());
    }

    [Fact]
    public void SameStructureWithAnotherDocumentKeyRebuilds()
    {
        var vm = new OutlineViewModel();
        vm.Update(Guide, OutlinePreferences.Default, new object());
        var first = vm.Items[0];

        vm.Update(Guide, OutlinePreferences.Default, new object());

        Assert.NotSame(first, vm.Items[0]);
    }

    [Fact]
    public void NavigateRaisesSourceLine()
    {
        var vm = Create();
        int? requested = null;
        vm.NavigateRequested += (_, line) => requested = line;

        vm.NavigateCommand.Execute(vm.Items[0].Children[1]);

        Assert.Equal(12, requested);
    }

    [Fact]
    public void ApplyTypographyUpdatesSizesWithoutRebuild()
    {
        var vm = Create();
        var root = vm.Items[0];

        vm.ApplyTypography(OutlinePreferences.Default with { H1FontSize = 19, LineHeight = 2.0 });

        Assert.Same(root, vm.Items[0]);
        Assert.Equal(19, root.FontSize);
        Assert.Equal(38, root.LineBoxHeight, 3);
    }

    private static IEnumerable<OutlineItemViewModel> Flatten(IEnumerable<OutlineItemViewModel> items)
        => items.SelectMany(i => new[] { i }.Concat(Flatten(i.Children)));
}
