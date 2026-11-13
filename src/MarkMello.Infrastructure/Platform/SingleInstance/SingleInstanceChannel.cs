using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MarkMello.Infrastructure.Platform.SingleInstance;

/// <summary>
/// Общие для обеих сторон канала имя и формат сообщения. Сообщение — версия
/// протокола, число путей и сами пути; клиент пишет его и закрывает соединение.
/// </summary>
internal static class SingleInstanceChannel
{
    public const int MaxPaths = 64;

    private const int ProtocolVersion = 1;

    /// <summary>
    /// Имя уникально для пользователя и сеанса: другой пользователь или второй
    /// RDP-сеанс получают свой экземпляр, а не окно в чужом.
    /// </summary>
    public static string GetName()
    {
        using var process = Process.GetCurrentProcess();
        var scope = string.Join(
            '|',
            Environment.UserDomainName,
            Environment.UserName,
            process.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Хэш, а не имя пользователя: имя может содержать символы, недопустимые
        // в имени mutex или сокета, а длина пути сокета на Unix ограничена.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scope));
        return "MarkMello-" + Convert.ToHexString(hash, 0, 8);
    }

    /// <summary>
    /// Аргументы превращаются в абсолютные пути во вторичном процессе:
    /// относительный путь имеет смысл только относительно его рабочего каталога.
    /// </summary>
    public static IReadOnlyList<string> ResolvePaths(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var paths = new List<string>();
        foreach (var arg in args)
        {
            // Ключи (в том числе диагностические) путями не являются.
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
            {
                continue;
            }

            if (!File.Exists(arg) && !Directory.Exists(arg))
            {
                continue;
            }

            paths.Add(Path.GetFullPath(arg));
            if (paths.Count == MaxPaths)
            {
                break;
            }
        }

        return paths;
    }

    public static void Write(Stream stream, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(paths);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(ProtocolVersion);
        writer.Write(paths.Count);
        foreach (var path in paths)
        {
            writer.Write(path);
        }

        writer.Flush();
    }

    /// <summary>
    /// Возвращает null для повреждённого или чужого сообщения: это не ошибка
    /// пользователя, сигнал просто игнорируется.
    /// </summary>
    public static IReadOnlyList<string>? TryRead(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadInt32() != ProtocolVersion)
            {
                return null;
            }

            var count = reader.ReadInt32();
            if (count is < 0 or > MaxPaths)
            {
                return null;
            }

            var paths = new List<string>(count);
            for (var index = 0; index < count; index++)
            {
                paths.Add(reader.ReadString());
            }

            return paths;
        }
        catch (Exception exception) when (exception is IOException or FormatException)
        {
            // EndOfStreamException — тоже IOException: сообщение оборвалось.
            return null;
        }
    }
}
