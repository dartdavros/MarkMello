using System.Collections.Frozen;
using static MarkMello.Presentation.Views.Markdown.Syntax.SyntaxLanguage;

namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>
/// Реестр языков по имени из info string блока кода (```python). Описания разложены
/// по семействам в partial-файлах: C-подобные здесь, скриптовые и форматы данных рядом.
/// </summary>
internal static partial class SyntaxLanguages
{
    // Свойства, а не поля: порядок инициализации статических полей между partial-файлами
    // не определён, и описание языка из соседнего файла могло бы увидеть здесь null.
    private static string[] CLikeComments => ["//"];

    private static (string, string)[] CLikeBlockComments => [("/*", "*/")];

    // Индекс строится при первом поиске, когда все описания уже инициализированы.
    private static FrozenDictionary<string, SyntaxLanguage>? _byAlias;

    public static SyntaxLanguage? Find(string name)
        => (_byAlias ??= BuildIndex()).TryGetValue(name, out var language) ? language : null;

    private static FrozenDictionary<string, SyntaxLanguage> BuildIndex()
    {
        SyntaxLanguage[] all =
        [
            CSharp, JavaScript, TypeScript, Java, Kotlin, Go, Rust, C, Cpp, Swift,
            Python, Bash, PowerShell, Ruby, Php, Lua,
            Json, Yaml, Toml, Sql, Css, Dockerfile
        ];

        var index = new Dictionary<string, SyntaxLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in all)
        {
            foreach (var alias in language.Aliases)
            {
                index[alias] = language;
            }
        }

        return index.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly SyntaxLanguage CSharp = new()
    {
        Aliases = ["cs", "csharp", "c#"],
        Keywords = Words("abstract as async await base break case catch checked class const continue default delegate do else enum event explicit extern finally fixed for foreach get goto if implicit in init interface internal is lock namespace new operator out override params partial private protected public readonly record ref required return sealed set sizeof stackalloc static struct switch this throw try typeof unchecked unsafe using value virtual volatile when where while with yield"),
        Types = Words("bool byte char decimal double dynamic float int long nint nuint object sbyte short string uint ulong ushort var void"),
        Constants = Words("true false null"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        CapitalizedIsType = true,
        HashDirectives = true
    };

    private static readonly FrozenSet<string> JavaScriptKeywords = Words("async await break case catch class const continue debugger default delete do else export extends finally for from function get if import in instanceof let new of return set static super switch this throw try typeof var void while with yield");

    private static readonly SyntaxLanguage JavaScript = new()
    {
        Aliases = ["js", "javascript", "jsx", "mjs", "cjs", "node"],
        Keywords = JavaScriptKeywords,
        Constants = Words("true false null undefined NaN Infinity"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        Quotes = "\"'`",
        MultilineBacktick = true,
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage TypeScript = new()
    {
        Aliases = ["ts", "typescript", "tsx", "mts", "cts"],
        Keywords = JavaScriptKeywords.Union(Words("abstract as declare enum implements infer interface is keyof namespace private protected public readonly satisfies type")).ToFrozenSet(StringComparer.Ordinal),
        Types = Words("any bigint boolean never number object string symbol unknown void"),
        Constants = Words("true false null undefined NaN Infinity"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        Quotes = "\"'`",
        MultilineBacktick = true,
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage Java = new()
    {
        Aliases = ["java"],
        Keywords = Words("abstract assert break case catch class const continue default do else enum extends final finally for goto if implements import instanceof interface native new package permits private protected public record return sealed static strictfp super switch synchronized this throw throws transient try var volatile while yield"),
        Types = Words("boolean byte char double float int long short void"),
        Constants = Words("true false null"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        TripleQuotes = true,
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage Kotlin = new()
    {
        Aliases = ["kt", "kotlin", "kts"],
        Keywords = Words("abstract as break by catch class companion const constructor continue data do else enum external final finally for fun get if import in init inline interface internal is lateinit object open operator out override package private protected public reified return sealed set super suspend this throw try typealias val var vararg when where while"),
        Types = Words("Any Boolean Byte Char Double Float Int Long Nothing Short String Unit"),
        Constants = Words("true false null"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        TripleQuotes = true,
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage Go = new()
    {
        Aliases = ["go", "golang"],
        Keywords = Words("break case chan const continue default defer else fallthrough for func go goto if import interface map package range return select struct switch type var"),
        Types = Words("any bool byte complex64 complex128 error float32 float64 int int8 int16 int32 int64 rune string uint uint8 uint16 uint32 uint64 uintptr"),
        Constants = Words("true false nil iota"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        Quotes = "\"'`",
        MultilineBacktick = true,
        RawBackticks = true
    };

    private static readonly SyntaxLanguage Rust = new()
    {
        Aliases = ["rs", "rust"],
        Keywords = Words("as async await break const continue crate dyn else enum extern fn for if impl in let loop match mod move mut pub ref return self Self static struct super trait type unsafe use where while"),
        Types = Words("bool char f32 f64 i8 i16 i32 i64 i128 isize str u8 u16 u32 u64 u128 usize"),
        Constants = Words("true false None Some Ok Err"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,

        // Апостроф в Rust — ещё и время жизни ('a), поэтому строки только в двойных кавычках.
        Quotes = "\"",
        CapitalizedIsType = true
    };

    private static readonly FrozenSet<string> CKeywords = Words("auto break case const continue default do else enum extern for goto if inline register restrict return sizeof static struct switch typedef union volatile while");

    private static readonly SyntaxLanguage C = new()
    {
        Aliases = ["c", "h"],
        Keywords = CKeywords,
        Types = Words("bool char double float int long short signed size_t unsigned void"),
        Constants = Words("true false NULL"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        HashDirectives = true
    };

    private static readonly SyntaxLanguage Cpp = new()
    {
        Aliases = ["cpp", "c++", "cc", "cxx", "hpp", "hxx"],
        Keywords = CKeywords.Union(Words("alignas alignof catch class concept consteval constexpr constinit co_await co_return co_yield decltype delete explicit export friend mutable namespace new noexcept operator override private protected public requires static_assert template this throw try typename using virtual")).ToFrozenSet(StringComparer.Ordinal),
        Types = Words("auto bool char char8_t char16_t char32_t double float int long short signed size_t unsigned void wchar_t"),
        Constants = Words("true false nullptr NULL"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        HashDirectives = true,
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage Swift = new()
    {
        Aliases = ["swift"],
        Keywords = Words("actor as associatedtype async await break case catch class continue default defer deinit do else enum extension fallthrough fileprivate final for func guard if import in init inout internal is lazy let mutating open operator override private protocol public repeat rethrows return self Self some static struct subscript super switch throw throws try typealias var where while"),
        Types = Words("Any Bool Character Double Float Int String Void"),
        Constants = Words("true false nil"),
        LineComments = CLikeComments,
        BlockComments = CLikeBlockComments,
        Quotes = "\"",
        TripleQuotes = true,
        CapitalizedIsType = true
    };
}
