using MarkMello.Infrastructure.Workspace;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Проверки на реальной файловой системе: фейк в тестах дерева не ловит
/// ни сортировку, ни фильтрацию служебных каталогов.
/// </summary>
public sealed class DirectoryWorkspaceFileSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "markmello-workspace-tests",
        Guid.NewGuid().ToString("N"));

    public DirectoryWorkspaceFileSystemTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task DirectoriesComeFirstAndNamesAreOrderedCaseInsensitively()
    {
        CreateFile("zeta.md");
        CreateFile("Alpha.md");
        CreateDirectory("src");
        CreateDirectory("Assets");

        var entries = await new DirectoryWorkspaceFileSystem().EnumerateChildrenAsync(_root);

        Assert.Equal(
            ["Assets", "src", "Alpha.md", "zeta.md"],
            entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task ServiceDirectoriesAndDotEntriesAreSkipped()
    {
        CreateDirectory(".git");
        CreateDirectory("node_modules");
        CreateDirectory("bin");
        CreateDirectory("obj");
        CreateDirectory("docs");
        CreateFile(".env");
        CreateFile("notes.md");

        var entries = await new DirectoryWorkspaceFileSystem().EnumerateChildrenAsync(_root);

        Assert.Equal(["docs", "notes.md"], entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task HiddenFilesAreSkipped()
    {
        var hidden = CreateFile("hidden.md");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        CreateFile("visible.md");

        var entries = await new DirectoryWorkspaceFileSystem().EnumerateChildrenAsync(_root);

        Assert.Equal(["visible.md"], entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task NonDocumentFilesAreListedButNotOpenable()
    {
        CreateFile("notes.md");
        CreateFile("pack.bat");
        CreateFile("LICENSE");

        var entries = await new DirectoryWorkspaceFileSystem().EnumerateChildrenAsync(_root);

        Assert.Equal(["LICENSE", "notes.md", "pack.bat"], entries.Select(entry => entry.Name));
        Assert.True(entries.Single(entry => entry.Name == "notes.md").IsSupportedDocument);
        Assert.False(entries.Single(entry => entry.Name == "pack.bat").IsSupportedDocument);
        Assert.False(entries.Single(entry => entry.Name == "LICENSE").IsSupportedDocument);
    }

    [Fact]
    public void MissingDirectoryIsReportedWithoutThrowing()
        => Assert.False(new DirectoryWorkspaceFileSystem()
            .DirectoryExists(Path.Combine(_root, "missing")));

    private string CreateFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "# test");
        return path;
    }

    private void CreateDirectory(string name) => Directory.CreateDirectory(Path.Combine(_root, name));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Мусор в temp не повод валить тестовый прогон.
        }
    }
}
