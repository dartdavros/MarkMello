using MarkMello.Application.Abstractions;
using MarkMello.Domain.Workspace;

namespace MarkMello.Application.UseCases;

/// <summary>
/// Файловые операции дерева: создание, переименование, дублирование, удаление.
/// Валидация имени и выбор имени копии живут здесь, чтобы правила были одинаковы
/// для всех точек входа — контекстного меню, кнопок шапки и клавиатуры.
/// </summary>
public sealed class WorkspaceFileOperationsUseCase
{
    private readonly IWorkspaceFileSystem _fileSystem;
    private readonly IPlatformServices _platform;

    public WorkspaceFileOperationsUseCase(IWorkspaceFileSystem fileSystem, IPlatformServices platform)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(platform);

        _fileSystem = fileSystem;
        _platform = platform;
    }

    public Task<WorkspaceMutationResult> CreateFileAsync(
        string directoryPath,
        string rawName,
        CancellationToken cancellationToken = default)
        => CreateAsync(directoryPath, rawName, isDirectory: false, cancellationToken);

    public Task<WorkspaceMutationResult> CreateDirectoryAsync(
        string directoryPath,
        string rawName,
        CancellationToken cancellationToken = default)
        => CreateAsync(directoryPath, rawName, isDirectory: true, cancellationToken);

    private async Task<WorkspaceMutationResult> CreateAsync(
        string directoryPath,
        string rawName,
        bool isDirectory,
        CancellationToken cancellationToken)
    {
        var problem = WorkspaceNameRules.Validate(rawName);
        if (problem != WorkspaceNameProblem.None)
        {
            return new WorkspaceMutationResult.InvalidName(problem);
        }

        var name = isDirectory
            ? rawName.Trim()
            : WorkspaceNameRules.EnsureDocumentExtension(rawName);

        var targetPath = Path.Combine(directoryPath, name);
        if (_fileSystem.Exists(targetPath))
        {
            return new WorkspaceMutationResult.NameTaken(name, isDirectory);
        }

        try
        {
            var entry = isDirectory
                ? await _fileSystem.CreateDirectoryAsync(directoryPath, name, cancellationToken).ConfigureAwait(false)
                : await _fileSystem.CreateFileAsync(directoryPath, name, cancellationToken).ConfigureAwait(false);

            return new WorkspaceMutationResult.Success(entry);
        }
        catch (Exception exception)
        {
            return ToFailure(exception, targetPath);
        }
    }

    public async Task<WorkspaceMutationResult> RenameAsync(
        string path,
        string rawName,
        CancellationToken cancellationToken = default)
    {
        var problem = WorkspaceNameRules.Validate(rawName);
        if (problem != WorkspaceNameProblem.None)
        {
            return new WorkspaceMutationResult.InvalidName(problem);
        }

        if (!_fileSystem.Exists(path))
        {
            return new WorkspaceMutationResult.NotFound(path);
        }

        var isDirectory = _fileSystem.DirectoryExists(path);
        var name = isDirectory ? rawName.Trim() : WorkspaceNameRules.EnsureDocumentExtension(rawName);
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var targetPath = Path.Combine(directory, name);

        // Смена только регистра — не конфликт: файловая система сама разберётся.
        if (!string.Equals(targetPath, path, StringComparison.OrdinalIgnoreCase) && _fileSystem.Exists(targetPath))
        {
            return new WorkspaceMutationResult.NameTaken(name, isDirectory);
        }

        try
        {
            var entry = await _fileSystem.RenameAsync(path, name, cancellationToken).ConfigureAwait(false);
            return new WorkspaceMutationResult.Success(entry);
        }
        catch (Exception exception)
        {
            return ToFailure(exception, path);
        }
    }

    public async Task<WorkspaceMutationResult> DuplicateAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Exists(path))
        {
            return new WorkspaceMutationResult.NotFound(path);
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;

        try
        {
            var siblings = await _fileSystem.GetChildNamesAsync(directory, cancellationToken).ConfigureAwait(false);
            var duplicateName = WorkspaceNameRules.BuildDuplicateName(Path.GetFileName(path), siblings);
            var entry = await _fileSystem.DuplicateAsync(path, duplicateName, cancellationToken).ConfigureAwait(false);
            return new WorkspaceMutationResult.Success(entry);
        }
        catch (Exception exception)
        {
            return ToFailure(exception, path);
        }
    }

    /// <summary>
    /// Удаление всегда через корзину. Если корзина недоступна, операция не выполняется —
    /// вызывающая сторона обязана переспросить с другим текстом, а не удалять молча.
    /// </summary>
    public async Task<WorkspaceMutationResult> DeleteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Exists(path))
        {
            return new WorkspaceMutationResult.NotFound(path);
        }

        try
        {
            var result = await _platform.MoveToTrashAsync(path, cancellationToken).ConfigureAwait(false);

            return result switch
            {
                TrashResult.Trashed => new WorkspaceMutationResult.Deleted(path, MovedToTrash: true),
                TrashResult.Unsupported => new WorkspaceMutationResult.TrashUnavailable(path),
                _ => new WorkspaceMutationResult.Failed(path, "trash")
            };
        }
        catch (Exception exception)
        {
            return ToFailure(exception, path);
        }
    }

    /// <summary>
    /// Безвозвратное удаление. Вызывается только после отдельного подтверждения,
    /// в котором пользователю сказано, что корзина недоступна.
    /// </summary>
    public async Task<WorkspaceMutationResult> DeletePermanentlyAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.Exists(path))
        {
            return new WorkspaceMutationResult.NotFound(path);
        }

        try
        {
            await _fileSystem.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            return new WorkspaceMutationResult.Deleted(path, MovedToTrash: false);
        }
        catch (Exception exception)
        {
            return ToFailure(exception, path);
        }
    }

    /// <summary>Сколько элементов в папке верхним уровнем — для текста подтверждения.</summary>
    public async Task<int> CountChildrenAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _fileSystem.CountChildrenAsync(directoryPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public ValueTask RevealAsync(string path, CancellationToken cancellationToken = default)
        => _platform.RevealInFileManagerAsync(path, cancellationToken);

    private static WorkspaceMutationResult ToFailure(Exception exception, string path)
        => exception switch
        {
            UnauthorizedAccessException => new WorkspaceMutationResult.AccessDenied(path),
            FileNotFoundException or DirectoryNotFoundException => new WorkspaceMutationResult.NotFound(path),
            IOException io => new WorkspaceMutationResult.Failed(path, io.Message),
            _ => new WorkspaceMutationResult.Failed(path, exception.Message)
        };
}
