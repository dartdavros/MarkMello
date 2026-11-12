using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

public sealed class MarkdownMathTexNormalizerTests
{
    [Fact]
    public void DfracBecomesDisplayStyleFraction()
    {
        var normalized = MarkdownMathTexNormalizer.Normalize(@"\dfrac{a}{b}");

        Assert.Equal(@"{\displaystyle\frac{a}{b}}", normalized);
    }

    [Fact]
    public void TfracBecomesTextStyleFraction()
    {
        var normalized = MarkdownMathTexNormalizer.Normalize(@"\tfrac{a}{b}");

        Assert.Equal(@"{\textstyle\frac{a}{b}}", normalized);
    }

    [Fact]
    public void NestedDfracKeepsBothArguments()
    {
        var normalized = MarkdownMathTexNormalizer.Normalize(@"\dfrac{\dfrac{a}{b}}{c}");

        Assert.Equal(@"{\displaystyle\frac{{\displaystyle\frac{a}{b}}}{c}}", normalized);
    }

    [Fact]
    public void PlusBetweenFractionsGetsThinSpaces()
    {
        var normalized = MarkdownMathTexNormalizer.Normalize(@"\frac{a}{b}+\frac{c}{d}");

        Assert.Equal(@"\frac{a}{b}\,\,+\,\,\frac{c}{d}", normalized);
    }

    [Fact]
    public void PlainSumKeepsNormalSpacing()
    {
        Assert.Equal("a + b", MarkdownMathTexNormalizer.Normalize("a + b"));
        Assert.Equal("x - y", MarkdownMathTexNormalizer.Normalize("x - y"));
    }

    [Fact]
    public void UnaryMinusInExponentIsNotTouched()
    {
        Assert.Equal("x^{-1}", MarkdownMathTexNormalizer.Normalize("x^{-1}"));
    }

    [Fact]
    public void UserReportedSurfFormulaBecomesValidAndSpaced()
    {
        const string latex = @"R_{surf} = \frac{n_s p_s - n_{is}^2}{\frac{n_s + n_{1s}}{S_p} + \frac{p_s + p_{1s}}{S_n}}";

        var normalized = MarkdownMathTexNormalizer.Normalize(latex);

        Assert.Equal(
            @"R_{surf} = \frac{n_s p_s - n_{is}^2}{\frac{n_s + n_{1s}}{S_p} \,\,+\,\,\frac{p_s + p_{1s}}{S_n}}",
            normalized);
    }

    [Fact]
    public void UserReportedDfracFormulaIsRewritten()
    {
        const string latex = @"R_s = \frac{n_s p_s - n_i^2}{\dfrac{n_s + n_i}{S_p} + \dfrac{p_s + n_i}{S_n}}";

        var normalized = MarkdownMathTexNormalizer.Normalize(latex);

        Assert.Equal(
            @"R_s = \frac{n_s p_s - n_i^2}{{\displaystyle\frac{n_s + n_i}{S_p}} \,\,+\,\,{\displaystyle\frac{p_s + n_i}{S_n}}}",
            normalized);
    }
}
