using MarkMello.Domain.Workspace;

namespace MarkMello.Domain.Tests;

public sealed class WorkspaceModelTests
{
    [Fact]
    public void OrderingPutsDirectoriesBeforeFiles()
    {
        var entries = new List<WorkspaceEntry>
        {
            WorkspaceEntry.ForFile(@"C:\docs\architecture.md", "architecture.md"),
            WorkspaceEntry.ForDirectory(@"C:\docs\src", "src"),
            WorkspaceEntry.ForFile(@"C:\docs\README.md", "README.md"),
            WorkspaceEntry.ForDirectory(@"C:\docs\adr", "adr")
        };

        entries.Sort(WorkspaceEntryOrdering.Instance);

        Assert.Equal(
            ["adr", "src", "architecture.md", "README.md"],
            entries.Select(entry => entry.Name));
    }

    [Fact]
    public void OrderingIgnoresCaseWithinGroup()
    {
        var entries = new List<WorkspaceEntry>
        {
            WorkspaceEntry.ForFile(@"C:\docs\b.md", "b.md"),
            WorkspaceEntry.ForFile(@"C:\docs\A.md", "A.md")
        };

        entries.Sort(WorkspaceEntryOrdering.Instance);

        Assert.Equal(["A.md", "b.md"], entries.Select(entry => entry.Name));
    }

    [Theory]
    [InlineData(".git", true)]
    [InlineData("node_modules", true)]
    [InlineData("BIN", true)]
    [InlineData("obj", true)]
    [InlineData("src", false)]
    [InlineData("binary-notes", false)]
    public void IgnoredDirectoryNamesAreMatchedCaseInsensitively(string name, bool expected)
        => Assert.Equal(expected, WorkspaceEntryFilter.IsIgnoredDirectoryName(name));

    [Theory]
    [InlineData(".env", true)]
    [InlineData("notes.md", false)]
    public void DotPrefixedNamesAreTreatedAsHidden(string name, bool expected)
        => Assert.Equal(expected, WorkspaceEntryFilter.IsDotPrefixedName(name));

    [Theory]
    [InlineData("notes.md", true)]
    [InlineData("notes.markdown", true)]
    [InlineData("notes.txt", true)]
    [InlineData("pack.bat", false)]
    [InlineData("LICENSE", false)]
    public void FileEntriesKnowWhetherTheyOpenInTheViewer(string name, bool expected)
    {
        var entry = WorkspaceEntry.ForFile(Path.Combine("C:", "docs", name), name);

        Assert.Equal(expected, entry.IsSupportedDocument);
    }

    [Fact]
    public void FolderDisplayNameIsTheLastSegment()
    {
        var folder = WorkspaceFolder.Create(@"C:\projects\MarkMello\docs\");

        Assert.Equal("docs", folder.DisplayName);
        Assert.Equal(@"C:\projects\MarkMello\docs", folder.RootPath);
    }

    [Theory]
    [InlineData(null, 260d)]
    [InlineData(120d, 220d)]
    [InlineData(999d, 340d)]
    [InlineData(280d, 280d)]
    [InlineData(double.NaN, 260d)]
    public void SidebarWidthIsClampedToTheDesignRange(double? stored, double expected)
        => Assert.Equal(expected, WorkspaceSidebarWidth.Normalize(stored));
}
