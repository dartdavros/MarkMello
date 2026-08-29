using System.Globalization;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Логика полосы вкладок: порядок, переполнение, различение одноимённых файлов
/// и обход по `Ctrl Tab`. Всё без UI — правила должны держаться сами по себе.
/// </summary>
public sealed class OpenDocumentsViewModelTests
{
    [Fact]
    public void ClosingActiveTabActivatesTheNeighbourOnTheRight()
    {
        var documents = CreateDocuments();
        var first = Open(documents, @"C:\docs\a.md");
        var second = Open(documents, @"C:\docs\b.md");
        var third = Open(documents, @"C:\docs\c.md");
        documents.Activate(second);

        documents.Remove(second);

        Assert.Equal([first, third], documents.Tabs);
        Assert.Same(third, documents.ActiveTab);
    }

    [Fact]
    public void ClosingTheLastTabLeavesNoActiveTab()
    {
        var documents = CreateDocuments();
        var only = Open(documents, @"C:\docs\a.md");
        documents.Activate(only);

        documents.Remove(only);

        Assert.Empty(documents.Tabs);
        Assert.Null(documents.ActiveTab);
        Assert.False(documents.HasTabs);
    }

    [Fact]
    public void ActiveFlagFollowsTheActiveTab()
    {
        var documents = CreateDocuments();
        var first = Open(documents, @"C:\docs\a.md");
        var second = Open(documents, @"C:\docs\b.md");

        documents.Activate(first);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);

        documents.Activate(second);
        Assert.False(first.IsActive);
        Assert.True(second.IsActive);
    }

    [Fact]
    public void NeighbourWrapsAroundInBothDirections()
    {
        var documents = CreateDocuments();
        var first = Open(documents, @"C:\docs\a.md");
        var second = Open(documents, @"C:\docs\b.md");
        var third = Open(documents, @"C:\docs\c.md");

        documents.Activate(third);
        Assert.Same(first, documents.GetNeighbour(1));

        documents.Activate(first);
        Assert.Same(third, documents.GetNeighbour(-1));
        Assert.Same(second, documents.GetNeighbour(1));
    }

    [Fact]
    public void SameFileNamesGetTheirParentFolderAsDisambiguator()
    {
        var documents = CreateDocuments();
        var docsReadme = Open(documents, @"C:\project\docs\README.md");
        var srcReadme = Open(documents, @"C:\project\src\README.md");
        var vision = Open(documents, @"C:\project\docs\vision.md");

        Assert.Equal("docs", docsReadme.Disambiguator);
        Assert.Equal("src", srcReadme.Disambiguator);
        Assert.Null(vision.Disambiguator);

        documents.Remove(srcReadme);

        Assert.Null(docsReadme.Disambiguator);
    }

    [Fact]
    public void TabsFitWithinTheStripStayVisible()
    {
        var documents = CreateDocuments();
        documents.AvailableWidth = 1000;
        Open(documents, @"C:\docs\a.md");
        Open(documents, @"C:\docs\b.md");

        Assert.Equal(2, documents.VisibleTabs.Count);
        Assert.Empty(documents.OverflowTabs);
        Assert.False(documents.HasOverflow);
    }

    [Fact]
    public void ExtraTabsMoveIntoOverflowFromTheEnd()
    {
        var documents = CreateDocuments();
        documents.AvailableWidth = 400;
        var first = Open(documents, @"C:\docs\first-document.md");
        var second = Open(documents, @"C:\docs\second-document.md");
        var third = Open(documents, @"C:\docs\third-document.md");
        documents.Activate(first);

        Assert.Contains(first, documents.VisibleTabs);
        Assert.Contains(third, documents.OverflowTabs);
        Assert.True(documents.HasOverflow);
        Assert.Equal(documents.OverflowTabs.Count.ToString(CultureInfo.CurrentCulture), documents.OverflowCountLabel);
    }

    [Fact]
    public void ActiveTabStaysVisibleEvenWhenItWouldOverflow()
    {
        var documents = CreateDocuments();
        documents.AvailableWidth = 400;
        Open(documents, @"C:\docs\first-document.md");
        Open(documents, @"C:\docs\second-document.md");
        var third = Open(documents, @"C:\docs\third-document.md");

        documents.Activate(third);

        Assert.Contains(third, documents.VisibleTabs);
        Assert.DoesNotContain(third, documents.OverflowTabs);
    }

    [Fact]
    public void NarrowStripStillShowsOneTab()
    {
        var documents = CreateDocuments();
        documents.AvailableWidth = 80;
        var only = Open(documents, @"C:\docs\a-very-long-document-name.md");

        Assert.Equal([only], documents.VisibleTabs);
    }

    [Fact]
    public async Task CloseOthersClosesEveryOtherTab()
    {
        var closed = new List<DocumentTabViewModel>();
        var documents = new OpenDocumentsViewModel(
            static _ => Task.CompletedTask,
            tab =>
            {
                closed.Add(tab);
                return Task.CompletedTask;
            });

        var first = Open(documents, @"C:\docs\a.md");
        var second = Open(documents, @"C:\docs\b.md");
        var third = Open(documents, @"C:\docs\c.md");

        await documents.CloseOthersCommand.ExecuteAsync(second);

        Assert.Equal([first, third], closed);
    }

    [Fact]
    public void TabWidthIsClampedToTheDesignRange()
    {
        var shortTab = new DocumentTabViewModel(@"C:\a.md", "a.md");
        var longTab = new DocumentTabViewModel(
            @"C:\implementation-plan-folders-and-tabs-revision.md",
            "implementation-plan-folders-and-tabs-revision.md");

        Assert.Equal(DocumentTabViewModel.MinimumWidth, shortTab.EstimateWidth());
        Assert.Equal(DocumentTabViewModel.MaximumWidth, longTab.EstimateWidth());
    }

    private static OpenDocumentsViewModel CreateDocuments()
        => new(static _ => Task.CompletedTask, static _ => Task.CompletedTask);

    private static DocumentTabViewModel Open(OpenDocumentsViewModel documents, string path)
        => documents.Add(new DocumentTabViewModel(path, Path.GetFileName(path)));
}
