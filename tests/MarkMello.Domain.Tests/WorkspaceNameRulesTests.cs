using MarkMello.Domain.Workspace;

namespace MarkMello.Domain.Tests;

public sealed class WorkspaceNameRulesTests
{
    [Theory]
    [InlineData("notes.md", WorkspaceNameProblem.None)]
    [InlineData("план встречи.md", WorkspaceNameProblem.None)]
    [InlineData("", WorkspaceNameProblem.Empty)]
    [InlineData("   ", WorkspaceNameProblem.Empty)]
    [InlineData("a/b.md", WorkspaceNameProblem.InvalidCharacters)]
    [InlineData("a:b.md", WorkspaceNameProblem.InvalidCharacters)]
    [InlineData("what?.md", WorkspaceNameProblem.InvalidCharacters)]
    [InlineData("notes.", WorkspaceNameProblem.InvalidCharacters)]
    [InlineData("CON.md", WorkspaceNameProblem.Reserved)]
    [InlineData("com1.txt", WorkspaceNameProblem.Reserved)]
    public void NamesAreValidatedAgainstTheStrictestPlatformRules(string name, WorkspaceNameProblem expected)
        => Assert.Equal(expected, WorkspaceNameRules.Validate(name));

    [Theory]
    [InlineData("notes", "notes.md")]
    [InlineData("notes.md", "notes.md")]
    [InlineData("notes.txt", "notes.txt")]
    [InlineData("archive.tar", "archive.tar")]
    public void MissingExtensionBecomesMarkdown(string input, string expected)
        => Assert.Equal(expected, WorkspaceNameRules.EnsureDocumentExtension(input));

    [Fact]
    public void FirstCopyGetsTheCopySuffix()
        => Assert.Equal(
            "README copy.md",
            WorkspaceNameRules.BuildDuplicateName("README.md", ["README.md"]));

    [Fact]
    public void FurtherCopiesAreNumbered()
    {
        var existing = new[] { "README.md", "README copy.md", "README copy 2.md" };

        Assert.Equal("README copy 3.md", WorkspaceNameRules.BuildDuplicateName("README.md", existing));
    }

    [Fact]
    public void FoldersAreDuplicatedWithoutAnExtension()
        => Assert.Equal("docs copy", WorkspaceNameRules.BuildDuplicateName("docs", ["docs"]));
}
