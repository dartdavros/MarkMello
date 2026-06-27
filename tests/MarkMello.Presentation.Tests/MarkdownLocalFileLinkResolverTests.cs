using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownLocalFileLinkResolverTests : IDisposable
{
    private readonly string _directory = CreateTempDirectory();

    [Fact]
    public void TryResolveReturnsNeighborMarkdownFile()
    {
        var target = WriteFile("another.md");

        var resolved = MarkdownLocalFileLinkResolver.TryResolve("another.md", _directory, out var targetPath);

        Assert.True(resolved);
        Assert.Equal(target, targetPath);
    }

    [Fact]
    public void TryResolveDecodesUrlEscapedFileName()
    {
        var target = WriteFile("file name.md");

        var resolved = MarkdownLocalFileLinkResolver.TryResolve("file%20name.md", _directory, out var targetPath);

        Assert.True(resolved);
        Assert.Equal(target, targetPath);
    }

    [Fact]
    public void TryResolveIgnoresAnchorFragmentAfterFileName()
    {
        var target = WriteFile("another.md");

        var resolved = MarkdownLocalFileLinkResolver.TryResolve("another.md#section", _directory, out var targetPath);

        Assert.True(resolved);
        Assert.Equal(target, targetPath);
    }

    [Fact]
    public void TryResolveReturnsFalseForMissingBaseDirectory()
    {
        var resolved = MarkdownLocalFileLinkResolver.TryResolve("another.md", null, out var targetPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, targetPath);
    }

    [Fact]
    public void TryResolveReturnsFalseForAbsoluteUri()
    {
        var resolved = MarkdownLocalFileLinkResolver.TryResolve("https://example.com/another.md", _directory, out var targetPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, targetPath);
    }

    [Fact]
    public void TryResolveReturnsFalseForUnsupportedFileType()
    {
        WriteFile("another.pdf");

        var resolved = MarkdownLocalFileLinkResolver.TryResolve("another.pdf", _directory, out var targetPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, targetPath);
    }

    [Fact]
    public void TryResolveReturnsFalseForMissingFile()
    {
        var resolved = MarkdownLocalFileLinkResolver.TryResolve("missing.md", _directory, out var targetPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, targetPath);
    }

    [Fact]
    public void TryResolveReturnsFalseForMalformedEscape()
    {
        var resolved = MarkdownLocalFileLinkResolver.TryResolve("bad%zz.md", _directory, out var targetPath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, targetPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteFile(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, string.Empty);
        return Path.GetFullPath(path);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
