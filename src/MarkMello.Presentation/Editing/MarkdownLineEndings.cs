namespace MarkMello.Presentation.Editing;

/// <summary>
/// Определяет перевод строки документа.
///
/// TextBox по умолчанию вставляет <see cref="Environment.NewLine"/>, то есть на
/// Windows CRLF. В LF-документе один Enter делает документ отличающимся от
/// исходного в месте, которого не видно, и он остаётся «изменённым» даже после
/// отката правки. Редактор должен продолжать документ тем же переводом строки,
/// которым тот написан.
/// </summary>
public static class MarkdownLineEndings
{
    public const string Windows = "\r\n";
    public const string Unix = "\n";

    /// <summary>
    /// Преобладающий перевод строки в <paramref name="text"/>. Для текста без
    /// переводов строки возвращает перевод строки платформы.
    /// </summary>
    public static string Detect(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Environment.NewLine;
        }

        var crlf = 0;
        var lf = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            if (index > 0 && text[index - 1] == '\r')
            {
                crlf++;
            }
            else
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0)
        {
            return Environment.NewLine;
        }

        // При смешанных переводах строки выигрывает большинство; ничья трактуется
        // в пользу CRLF, потому что смешение обычно возникает при дописывании
        // CRLF в CRLF-документ.
        return crlf >= lf ? Windows : Unix;
    }
}
