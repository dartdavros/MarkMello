using System.Net.Http;
using MarkMello.Application.Abstractions;

namespace MarkMello.Infrastructure.Images;

/// <summary>
/// Resolves image URLs declared in markdown documents.
///
/// Supports:
///   - absolute http:// and https:// URIs (size-capped, MIME-checked)
///   - data: URIs (image/png, image/jpeg, image/gif, image/webp, image/bmp),
///     base64-encoded only
///   - absolute local file paths and file:// URIs
///   - paths relative to the markdown's base directory
///
/// Returns null on any failure (unreachable host, unsupported scheme, wrong
/// content type, size cap exceeded, IO error). This is intentional -- the
/// view displays a placeholder block in that case rather than propagating
/// an exception into the viewer path.
/// </summary>
public sealed class DefaultImageSourceResolver : IImageSourceResolver
{
    private const long MaxImageBytes = 20L * 1024 * 1024; // 20 MB

    private static readonly HashSet<string> AllowedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/gif",
        "image/webp",
        "image/bmp",
    };

    // Single HttpClient per process. Idle connections are pooled by SocketsHttpHandler.
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        // Some CDNs (github user-content, imgur, etc.) serve a generic page
        // if the UA looks like a robot. A plain UA avoids that surface.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MarkMello/1.0 (viewer)");
        return client;
    }

    public async Task<Stream?> TryOpenAsync(string url, string? baseDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            // data: URIs
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return TryOpenDataUri(url);
            }

            // http / https
            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
                && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            {
                return await TryOpenHttpAsync(absolute, cancellationToken).ConfigureAwait(false);
            }

            // file://
            if (absolute is not null && absolute.Scheme == Uri.UriSchemeFile)
            {
                return TryOpenLocal(absolute.LocalPath);
            }

            // Absolute local path (Windows drive letter, or UNIX-style "/...")
            if (Path.IsPathRooted(url))
            {
                return TryOpenLocal(url);
            }

            // Relative path -- needs a base directory.
            if (baseDirectory is not null)
            {
                var combined = Path.GetFullPath(Path.Combine(baseDirectory, url));
                // Refuse to escape the tree via "../../../etc/passwd" style
                // if the resolved absolute path does not live under the base.
                // (This is a conservative stance; MarkMello is local-only, but
                // the rule is cheap and reduces surprise.)
                if (!IsUnder(combined, baseDirectory))
                {
                    return null;
                }
                return TryOpenLocal(combined);
            }

            return null;
        }
        catch
        {
            // IImageSourceResolver contract: never throw for expected failures.
            return null;
        }
    }

    private static MemoryStream? TryOpenDataUri(string url)
    {
        // Syntax: data:[<media type>][;base64],<payload>
        var commaIdx = url.IndexOf(',', StringComparison.Ordinal);
        if (commaIdx < 0)
        {
            return null;
        }

        var meta = url.AsSpan(5, commaIdx - 5); // after "data:"
        var payload = url.AsSpan(commaIdx + 1);

        var isBase64 = meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        if (!isBase64)
        {
            // URL-encoded text variant is not safe to render as an image.
            return null;
        }

        var mediaTypePart = meta.Slice(0, meta.Length - ";base64".Length).ToString();
        if (mediaTypePart.Length > 0
            && !AllowedMediaTypes.Contains(mediaTypePart.Trim()))
        {
            return null;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(payload.ToString());
        }
        catch (FormatException)
        {
            return null;
        }

        if (decoded.LongLength > MaxImageBytes)
        {
            return null;
        }

        return new MemoryStream(decoded, writable: false);
    }

    private static FileStream? TryOpenLocal(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new FileInfo(path);
        if (info.Length > MaxImageBytes)
        {
            return null;
        }

        return info.OpenRead();
    }

    private static async Task<Stream?> TryOpenHttpAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await HttpClient
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && !AllowedMediaTypes.Contains(mediaType))
        {
            return null;
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is > MaxImageBytes)
        {
            return null;
        }

        // Buffer to memory with an explicit cap so we never blow up on a
        // server that under-reports Content-Length.
        await using var network = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await network.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaxImageBytes)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static bool IsUnder(string candidate, string root)
    {
        var candidateFull = Path.GetFullPath(candidate);
        var rootFull = Path.GetFullPath(root);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
        {
            rootFull += Path.DirectorySeparatorChar;
        }
        return candidateFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
    }
}
