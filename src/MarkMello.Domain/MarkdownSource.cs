namespace MarkMello.Domain;

/// <summary>
/// Сырой исходник markdown файла, прочитанный с диска.
/// Передаётся в markdown pipeline (parse → render) на дальнейших этапах.
/// </summary>
/// <param name="Path">Полный путь к файлу.</param>
/// <param name="FileName">Имя файла без пути.</param>
/// <param name="Content">Содержимое файла как строка.</param>
public sealed record MarkdownSource(string Path, string FileName, string Content);
