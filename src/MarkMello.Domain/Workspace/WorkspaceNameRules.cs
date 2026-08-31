namespace MarkMello.Domain.Workspace;

/// <summary>Почему имя не подходит. Сообщение подбирает Presentation, домен только классифицирует.</summary>
public enum WorkspaceNameProblem
{
    None,
    Empty,
    InvalidCharacters,
    Reserved
}

/// <summary>
/// Правила имён файлов и папок при создании и переименовании. Держим их в домене,
/// потому что от них зависит и валидация ввода, и генерация имени копии.
/// </summary>
public static class WorkspaceNameRules
{
    /// <summary>Запрещённый набор Windows — самый строгий, поэтому применяется на всех платформах.</summary>
    public static IReadOnlyList<char> InvalidCharacters { get; } = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static WorkspaceNameProblem Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return WorkspaceNameProblem.Empty;
        }

        var trimmed = name.Trim();

        foreach (var character in trimmed)
        {
            if (InvalidCharacters.Contains(character) || char.IsControl(character))
            {
                return WorkspaceNameProblem.InvalidCharacters;
            }
        }

        // Точки и пробелы в конце Windows молча срезает — имя перестаёт быть тем, что ввели.
        if (trimmed.EndsWith('.') || !string.Equals(trimmed, name.Trim(), StringComparison.Ordinal))
        {
            return WorkspaceNameProblem.InvalidCharacters;
        }

        var withoutExtension = Path.GetFileNameWithoutExtension(trimmed);
        return ReservedNames.Contains(withoutExtension)
            ? WorkspaceNameProblem.Reserved
            : WorkspaceNameProblem.None;
    }

    /// <summary>
    /// Имя нового документа: расширение подставляется, если пользователь его не написал.
    /// Ввод собственного расширения (например «.txt») остаётся как есть.
    /// </summary>
    public static string EnsureDocumentExtension(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        return SupportedDocumentTypes.IsSupportedPath(trimmed) || Path.HasExtension(trimmed)
            ? trimmed
            : trimmed + ".md";
    }

    /// <summary>
    /// Имя копии: «README.md» → «README copy.md» → «README copy 2.md».
    /// Суффикс не локализуется: имя файла — не элемент интерфейса, и оно не должно
    /// меняться от языка приложения.
    /// </summary>
    public static string BuildDuplicateName(string originalName, IEnumerable<string> existingNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
        ArgumentNullException.ThrowIfNull(existingNames);

        var taken = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var stem = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);

        var candidate = $"{stem} copy{extension}";
        if (!taken.Contains(candidate))
        {
            return candidate;
        }

        for (var index = 2; ; index++)
        {
            candidate = $"{stem} copy {index}{extension}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
