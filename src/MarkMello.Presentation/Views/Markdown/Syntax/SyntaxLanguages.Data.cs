using static MarkMello.Presentation.Views.Markdown.Syntax.SyntaxLanguage;

namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>Форматы данных и конфигов, SQL и CSS.</summary>
internal static partial class SyntaxLanguages
{
    private static readonly SyntaxLanguage Json = new()
    {
        Aliases = ["json", "jsonc", "json5", "geojson", "webmanifest"],
        Constants = Words("true false null"),

        // Комментарии в чистом JSON запрещены, но в jsonc и json5 встречаются постоянно.
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        Quotes = "\"",
        HighlightKeys = true
    };

    private static readonly SyntaxLanguage Yaml = new()
    {
        Aliases = ["yaml", "yml"],
        Constants = Words("true false null yes no on off True False Null Yes No On Off TRUE FALSE NULL ~"),
        LineComments = HashComments,
        RawSingleQuotes = true,
        DoubledQuoteEscapes = true,
        HashCommentNeedsSeparator = true,
        HighlightKeys = true,
        IdentifierExtras = "-."
    };

    private static readonly SyntaxLanguage Toml = new()
    {
        Aliases = ["toml", "ini", "cfg", "conf", "properties", "editorconfig", "env", "dotenv", "gitconfig"],
        Constants = Words("true false"),
        LineComments = ["#", ";"],

        // Литеральные строки TOML ('…') без экранирования; в значениях INI «#» бывает частью текста.
        RawSingleQuotes = true,
        HashCommentNeedsSeparator = true,
        HighlightKeys = true,
        SectionHeaders = true,
        IdentifierExtras = "-."
    };

    private static readonly SyntaxLanguage Sql = new()
    {
        Aliases = ["sql", "mysql", "postgresql", "postgres", "psql", "sqlite", "tsql", "plsql"],
        Keywords = Words("add all alter and any as asc begin between by case check column commit constraint create cross database default delete desc distinct drop else end exists foreign from full group having if in index inner insert intersect into is join key left like limit not null offset on or order outer primary procedure references returning right rollback select set table then top transaction trigger truncate union unique update values view when where with"),
        Types = Words("bigint binary bit blob bool boolean char date datetime decimal double float int integer json jsonb money nchar numeric nvarchar real serial smallint text time timestamp tinyint uuid varbinary varchar"),
        Constants = Words("true false"),
        LineComments = ["--"],
        BlockComments = CLikeBlockComments,
        Quotes = "'\"`",

        // В стандартном SQL «\» не экранирует, кавычка внутри строки удваивается: 'it''s'.
        EscapeChar = null,
        DoubledQuoteEscapes = true,
        CaseInsensitive = true
    };

    private static readonly SyntaxLanguage Css = new()
    {
        Aliases = ["css", "scss", "sass", "less", "pcss", "postcss"],
        Keywords = Words("important inherit initial unset revert auto none"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        IdentifierExtras = "-",
        HashColors = true,
        HighlightKeys = true
    };
}
