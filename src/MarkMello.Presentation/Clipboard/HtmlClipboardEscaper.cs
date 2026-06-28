using System.Net;

namespace MarkMello.Presentation.Clipboard;

internal static class HtmlClipboardEscaper
{
    public static string Encode(string value)
        => WebUtility.HtmlEncode(value);

    public static string AttributeEncode(string value)
        => WebUtility.HtmlEncode(value).Replace("\"", "&quot;", StringComparison.Ordinal);
}
