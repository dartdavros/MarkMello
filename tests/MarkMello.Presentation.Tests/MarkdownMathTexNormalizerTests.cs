using Avalonia.Media;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MarkdownMathTexNormalizerTests
{
    private readonly AvaloniaHeadlessFixture _fixture;

    public MarkdownMathTexNormalizerTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

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
    public void NormBarsBecomeEngineAlias()
    {
        // \lVert / \rVert are standard amsmath, but the engine only knows
        // \Vert; without the alias the whole sample.md matrix block fell back
        // to raw source.
        var normalized = MarkdownMathTexNormalizer.Normalize(@"\lVert v \rVert");

        Assert.Equal(@"\Vert v \Vert", normalized);
    }

    [Fact]
    public void AbsoluteValueBarsBecomeEngineAlias()
    {
        var normalized = MarkdownMathTexNormalizer.Normalize(@"\lvert x \rvert");

        Assert.Equal(@"\vert x \vert", normalized);
    }

    [Fact]
    public Task SampleMatrixAndNormFormulaRenders()
        => _fixture.Session.Dispatch(() =>
        {
            var layout = MarkdownMathRenderer.Measure(
                @"M = \begin{pmatrix} a & b \\ c & d \end{pmatrix}, \quad \lVert v \rVert = \sqrt{x^2 + y^2}",
                16,
                Brushes.Black,
                isDisplay: true);

            Assert.NotNull(layout);
            Assert.True(layout!.IsValid, layout.ErrorMessage);
        }, CancellationToken.None);

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
