using MarkMello.Presentation.Views.Markdown;
using MarkMello.Presentation.Views.Markdown.Syntax;

namespace MarkMello.Presentation.Tests;

/// <summary>Подсветка блоков кода (#46): роли фрагментов для типичных языков.</summary>
public sealed class CodeSyntaxHighlighterTests
{
    [Fact]
    public void PythonRolesMatchTheReadingExpectation()
    {
        const string code = "def hello(name):\n    # greet\n    print(f\"Hi, {name}!\")\n    return None, 42";

        var roles = Roles(code, "python");

        Assert.Contains(("def", SyntaxTokenKind.Keyword), roles);
        Assert.Contains(("hello", SyntaxTokenKind.Function), roles);
        Assert.Contains(("# greet", SyntaxTokenKind.Comment), roles);
        Assert.Contains(("print", SyntaxTokenKind.Function), roles);
        Assert.Contains(("f\"Hi, {name}!\"", SyntaxTokenKind.String), roles);
        Assert.Contains(("None", SyntaxTokenKind.Number), roles);
        Assert.Contains(("42", SyntaxTokenKind.Number), roles);
    }

    [Fact]
    public void CSharpVerbatimStringKeepsDoubledQuotesInside()
    {
        const string code = "#region Paths\nvar path = @\"C:\\a \"\"b\"\"\"; // tail\nstring s = new Uri(path);";

        var roles = Roles(code, "cs");

        Assert.Contains(("#region", SyntaxTokenKind.Keyword), roles);
        Assert.Contains(("@\"C:\\a \"\"b\"\"\"", SyntaxTokenKind.String), roles);
        Assert.Contains(("// tail", SyntaxTokenKind.Comment), roles);
        Assert.Contains(("string", SyntaxTokenKind.Type), roles);
        Assert.Contains(("Uri", SyntaxTokenKind.Function), roles);
    }

    [Fact]
    public void JsonKeysDifferFromStringValues()
    {
        var roles = Roles("{ \"name\": \"mello\", \"ok\": true }", "json");

        Assert.Contains(("\"name\"", SyntaxTokenKind.Type), roles);
        Assert.Contains(("\"mello\"", SyntaxTokenKind.String), roles);
        Assert.Contains(("true", SyntaxTokenKind.Number), roles);
    }

    [Fact]
    public void YamlHashInsideValueIsNotAComment()
    {
        var roles = Roles("- name: build#1\n  enabled: yes # flag", "yaml");

        Assert.Contains(("name", SyntaxTokenKind.Type), roles);
        Assert.Contains(("enabled", SyntaxTokenKind.Type), roles);
        Assert.Contains(("yes", SyntaxTokenKind.Number), roles);
        Assert.Contains(("# flag", SyntaxTokenKind.Comment), roles);
        Assert.DoesNotContain(roles, role => role.Text.StartsWith("#1", StringComparison.Ordinal));
    }

    [Fact]
    public void ShellVariablesAndParametersAreHighlighted()
    {
        var roles = Roles("if [ $# -gt 0 ]; then\n  ls -la \"$HOME\" # list\nfi", "bash");

        Assert.Contains(("if", SyntaxTokenKind.Keyword), roles);
        Assert.Contains(("$#", SyntaxTokenKind.Type), roles);
        Assert.Contains(("-la", SyntaxTokenKind.Type), roles);
        Assert.Contains(("\"$HOME\"", SyntaxTokenKind.String), roles);
        Assert.Contains(("# list", SyntaxTokenKind.Comment), roles);
    }

    [Fact]
    public void PowerShellIsCaseInsensitiveAndKnowsCmdlets()
    {
        var roles = Roles("IF ($true) { Get-ChildItem -Path $dir }", "powershell");

        Assert.Contains(("IF", SyntaxTokenKind.Keyword), roles);
        Assert.Contains(("$true", SyntaxTokenKind.Number), roles);
        Assert.Contains(("Get-ChildItem", SyntaxTokenKind.Function), roles);
        Assert.Contains(("-Path", SyntaxTokenKind.Type), roles);
        Assert.Contains(("$dir", SyntaxTokenKind.Type), roles);
    }

    [Fact]
    public void PowerShellBackslashDoesNotEscapeTheClosingQuote()
    {
        // Пример из ревью #53: «\» в PowerShell — обычный символ пути, экранирует «`».
        var roles = Roles("Write-Host \"C:\\\"; Get-ChildItem \"a`\"b\"", "powershell");

        Assert.Contains(("\"C:\\\"", SyntaxTokenKind.String), roles);
        Assert.Contains(("Get-ChildItem", SyntaxTokenKind.Function), roles);
        Assert.Contains(("\"a`\"b\"", SyntaxTokenKind.String), roles);
    }

    [Fact]
    public void SingleQuotedShellStringsHaveNoEscapes()
    {
        var roles = Roles("echo 'C:\\' && ls -la", "bash");

        Assert.Contains(("'C:\\'", SyntaxTokenKind.String), roles);
        Assert.Contains(("-la", SyntaxTokenKind.Type), roles);
    }

    [Fact]
    public void SqlStringsUseDoubledQuotesInsteadOfBackslash()
    {
        var roles = Roles("SELECT 'C:\\', 'it''s' FROM t", "sql");

        Assert.Contains(("'C:\\'", SyntaxTokenKind.String), roles);
        Assert.Contains(("'it''s'", SyntaxTokenKind.String), roles);
        Assert.Contains(("FROM", SyntaxTokenKind.Keyword), roles);
    }

    [Fact]
    public void PythonCommentMayFollowCodeWithoutSpace()
    {
        // Пример из ревью #53: разделитель перед «#» нужен оболочкам и YAML, но не Python.
        var roles = Roles("print(1)# return True", "python");

        Assert.Contains(("# return True", SyntaxTokenKind.Comment), roles);
        Assert.DoesNotContain(("return", SyntaxTokenKind.Keyword), roles);
        Assert.DoesNotContain(("True", SyntaxTokenKind.Number), roles);
    }

    [Fact]
    public void MarkupHighlightsTagsAttributesAndComments()
    {
        var roles = Roles("<!-- note -->\n<a href=\"/x\" data-id='1'>&amp;</a>", "html");

        Assert.Contains(("<!-- note -->", SyntaxTokenKind.Comment), roles);
        Assert.Contains(("a", SyntaxTokenKind.Keyword), roles);
        Assert.Contains(("href", SyntaxTokenKind.Type), roles);
        Assert.Contains(("\"/x\"", SyntaxTokenKind.String), roles);
        Assert.Contains(("data-id", SyntaxTokenKind.Type), roles);
        Assert.Contains(("&amp;", SyntaxTokenKind.Number), roles);
    }

    [Fact]
    public void UnknownOrMissingLanguageStaysPlain()
    {
        Assert.Empty(CodeSyntaxHighlighter.Tokenize("def x(): pass", "brainfuck"));
        Assert.Empty(CodeSyntaxHighlighter.Tokenize("def x(): pass", null));
        Assert.Empty(CodeSyntaxHighlighter.Tokenize("def x(): pass", "  "));
    }

    [Theory]
    [InlineData("python", "python")]
    [InlineData("python title=\"app.py\"", "python")]
    [InlineData("{.python}", "python")]
    [InlineData("language-js", "js")]
    [InlineData("ts,linenos", "ts")]
    public void LanguageNameIsTheFirstWordOfInfoString(string info, string expected)
        => Assert.Equal(expected, CodeSyntaxHighlighter.GetLanguageName(info));

    [Fact]
    public void UnterminatedStringStopsAtLineEnd()
    {
        var roles = Roles("x = \"open\ny = 1", "python");

        Assert.Contains(("\"open", SyntaxTokenKind.String), roles);
        Assert.Contains(("1", SyntaxTokenKind.Number), roles);
    }

    [Theory]
    [InlineData("cs", "class A { /* unterminated")]
    [InlineData("js", "const t = `multi\nline ${x}`; // done")]
    [InlineData("sql", "SELECT id, 'it''s' FROM t -- c")]
    [InlineData("toml", "[server]\nport = 8080 # web")]
    [InlineData("css", "a:hover { color: #fff; margin: 0 1.5em; } @media x {}")]
    [InlineData("diff", "@@ -1 +1 @@\n-old\n+new")]
    [InlineData("rust", "fn main<'a>(s: &'a str) -> i32 { 0x1F }")]
    public void TokensAreOrderedAndStayInsideTheCode(string language, string code)
    {
        var tokens = CodeSyntaxHighlighter.Tokenize(code, language);

        Assert.NotEmpty(tokens);
        var previousEnd = 0;
        foreach (var token in tokens)
        {
            Assert.True(token.Start >= previousEnd, $"overlap at {token.Start}");
            Assert.True(token.Length > 0);
            Assert.True(token.Start + token.Length <= code.Length);
            previousEnd = token.Start + token.Length;
        }

        // Текст блока не меняется — меняется только раскраска.
        Assert.Equal(code, MarkdownStyledText.FromCode(code, tokens).Text);
    }

    private static List<(string Text, SyntaxTokenKind Kind)> Roles(string code, string language)
        => CodeSyntaxHighlighter.Tokenize(code, language)
            .Select(token => (code.Substring(token.Start, token.Length), token.Kind))
            .ToList();
}
