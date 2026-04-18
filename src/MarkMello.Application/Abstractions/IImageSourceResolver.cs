namespace MarkMello.Application.Abstractions;

/// <summary>
/// Resolves an image reference from a markdown document to a readable byte
/// stream. Implementations are responsible for choosing a transport (local
/// file system, HTTP, data URI), enforcing size caps and short-circuiting
/// unsupported schemes.
/// </summary>
public interface IImageSourceResolver
{
    /// <summary>
    /// Attempts to open the image. Returns null if the source is unsupported,
    /// unreachable, too large, or an error occurred. Never throws for
    /// expected failure modes; the caller renders a placeholder when null.
    /// </summary>
    /// <param name="url">Raw URL as present in the markdown (may be relative).</param>
    /// <param name="baseDirectory">
    /// Absolute directory of the source .md file, or null when unknown.
    /// Used to resolve relative paths like <c>images/foo.png</c>.
    /// </param>
    Task<Stream?> TryOpenAsync(string url, string? baseDirectory, CancellationToken cancellationToken);
}
