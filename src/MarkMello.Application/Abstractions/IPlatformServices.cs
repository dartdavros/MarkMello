namespace MarkMello.Application.Abstractions;

/// <summary>Куда делся удалённый элемент. Различие важно для текста подтверждения.</summary>
public enum TrashResult
{
    /// <summary>Перемещён в корзину — операция обратима.</summary>
    Trashed,

    /// <summary>Корзина на этой платформе или для этого пути недоступна.</summary>
    Unsupported,

    /// <summary>Корзина есть, но операция не удалась.</summary>
    Failed
}

/// <summary>
/// Контракт интеграции с платформой ОС (file associations, command-line activation, system theme и т.п.).
/// В M0 содержит только идентификатор платформы. Наполняется в M2 (file-first open path).
/// </summary>
public interface IPlatformServices
{
    /// <summary>Имя текущей платформы: Windows / macOS / Linux / Unknown.</summary>
    string PlatformName { get; }

    /// <summary>
    /// Переместить элемент в корзину ОС. Удаление из дерева обязано быть обратимым,
    /// поэтому безвозвратный <c>File.Delete</c> здесь недопустим (ADR-0007 Rule 7).
    /// </summary>
    ValueTask<TrashResult> MoveToTrashAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Показать элемент в файловом менеджере ОС.</summary>
    ValueTask RevealInFileManagerAsync(string path, CancellationToken cancellationToken = default);
}
