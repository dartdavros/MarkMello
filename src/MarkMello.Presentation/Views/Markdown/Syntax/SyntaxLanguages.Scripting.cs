using static MarkMello.Presentation.Views.Markdown.Syntax.SyntaxLanguage;

namespace MarkMello.Presentation.Views.Markdown.Syntax;

/// <summary>Скриптовые языки и оболочки.</summary>
internal static partial class SyntaxLanguages
{
    private static string[] HashComments => ["#"];

    private static readonly SyntaxLanguage Python = new()
    {
        Aliases = ["py", "python", "python3", "py3", "pyi"],
        Keywords = Words("and as assert async await break case class continue def del elif else except finally for from global if import in is lambda match nonlocal not or pass raise return try while with yield"),
        Types = Words("bool bytes complex dict float frozenset int list object set str tuple type"),
        Constants = Words("True False None self cls"),
        LineComments = HashComments,
        TripleQuotes = true,
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage Bash = new()
    {
        Aliases = ["sh", "bash", "shell", "zsh", "shellscript", "console", "terminal"],
        Keywords = Words("case do done elif else esac export fi for function if in local readonly return select then until while declare unset source alias"),
        Constants = Words("true false"),
        LineComments = HashComments,
        Quotes = "\"'`",
        RawSingleQuotes = true,
        HashCommentNeedsSeparator = true,
        VariableSigil = '$',
        IdentifierExtras = "-",
        DashParameters = true
    };

    private static readonly SyntaxLanguage PowerShell = new()
    {
        Aliases = ["ps", "ps1", "psm1", "powershell", "pwsh"],
        Keywords = Words("begin break catch class continue data do dynamicparam else elseif end enum exit filter finally for foreach from function if in param process return switch throw trap try until using while workflow"),
        Constants = Words("$true $false $null"),
        LineComments = HashComments,
        BlockComments = [("<#", "#>")],

        // В PowerShell экранирует обратный апостроф, а «\» — обычный символ пути.
        EscapeChar = '`',
        RawSingleQuotes = true,
        DoubledQuoteEscapes = true,
        HashCommentNeedsSeparator = true,
        VariableSigil = '$',
        IdentifierExtras = "-",
        DashParameters = true,
        CaseInsensitive = true
    };

    private static readonly SyntaxLanguage Ruby = new()
    {
        Aliases = ["rb", "ruby"],
        Keywords = Words("alias and begin break case class def defined? do else elsif end ensure for if in module next not or redo rescue retry return self super then undef unless until when while yield require attr_reader attr_writer attr_accessor"),
        Constants = Words("true false nil"),
        LineComments = HashComments,
        Quotes = "\"'`",
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage Php = new()
    {
        Aliases = ["php"],
        Keywords = Words("abstract and as break case catch class clone const continue declare default do echo else elseif enum extends final finally fn for foreach function global if implements include interface instanceof match namespace new or print private protected public readonly require require_once return static switch throw trait try use var while yield"),
        Types = Words("array bool callable float int iterable mixed object string void"),
        Constants = Words("true false null TRUE FALSE NULL"),
        LineComments = ["//", "#"],
        BlockComments = CLikeBlockComments,
        VariableSigil = '$',
        CapitalizedIsType = true
    };

    private static readonly SyntaxLanguage Lua = new()
    {
        Aliases = ["lua"],
        Keywords = Words("and break do else elseif end for function goto if in local not or repeat return then until while"),
        Constants = Words("true false nil"),
        LineComments = ["--"],
        BlockComments = [("--[[", "]]")]
    };

    private static readonly SyntaxLanguage Dockerfile = new()
    {
        Aliases = ["dockerfile", "docker", "containerfile"],
        Keywords = Words("from run cmd label maintainer expose env add copy entrypoint volume user workdir arg onbuild stopsignal healthcheck shell as"),
        LineComments = HashComments,
        HashCommentNeedsSeparator = true,
        VariableSigil = '$',
        DashParameters = true,
        CaseInsensitive = true
    };
}
