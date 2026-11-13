using System.Globalization;
using System.Text;
using System.Xml.Linq;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Infrastructure.Diagrams;
using MarkMello.Presentation.Services;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class MermaidSubgraphDirectionTests(AvaloniaHeadlessFixture fixture)
{
    [Fact]
    public Task Issue48SourceRendersThroughTheNativeSvgReader()
    {
        return fixture.RunAsync(() =>
        {
            var success = Render(MermaidSubgraphExamples.Issue48);
            Assert.True(AotSafeSvgImage.TryLoad(Encoding.UTF8.GetBytes(success.Svg), out var image));
            var labels = image.EnumerateTextContents();
            foreach (var label in MermaidSubgraphExamples.Issue48Labels)
            {
                Assert.Contains(label, labels);
            }
            Assert.Contains("#dbeafe", success.Svg, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("#dcfce7", success.Svg, StringComparison.OrdinalIgnoreCase);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("TB", "LR", 0, 1)]
    [InlineData("TD", "LR", 0, 1)]
    [InlineData("BT", "LR", 0, -1)]
    [InlineData("LR", "TB", 1, 0)]
    [InlineData("RL", "TB", -1, 0)]
    public Task SubgraphDirectionControlsLayoutIndependentlyOfTheParent(
        string direction, string parentDirection, int dx, int dy)
    {
        return fixture.RunAsync(() =>
        {
            var svg = Render($"""
                flowchart {parentDirection}
                    subgraph Inner[Inner]
                        direction {direction}
                        First --> Second
                    end
                    Before --> Inner --> After
                """).Svg;
            AssertDirection(svg, "First", "Second", dx, dy);
            AssertDirection(svg, "Before", "After", dy == 0 ? 0 : 1, dx == 0 ? 0 : 1);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task NestedSubgraphsKeepTheirOwnDirections()
    {
        return fixture.RunAsync(() =>
        {
            var svg = Render(MermaidSubgraphExamples.NestedDirections).Svg;
            AssertDirection(svg, "i1", "f1", -1, 0);
            AssertDirection(svg, "i2", "f2", 0, -1);
            AssertDirection(svg, "A", "B", 1, 0);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task LinkingAnInnerNodeOutsideTheSubgraphInheritsTheParentDirection()
    {
        return fixture.RunAsync(() =>
        {
            // Mermaid deliberately ignores the local direction when an inner node has an external link.
            var svg = Render("""
                flowchart LR
                    subgraph Inner
                        direction TB
                        First --> Second
                    end
                    Outside --> First
                """).Svg;
            AssertDirection(svg, "First", "Second", 1, 0);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task InvalidSubgraphDirectionReturnsAControlledFailureWithOriginalSource()
    {
        return fixture.RunAsync(() =>
        {
            const string source = "flowchart LR\nsubgraph Inner\ndirection XX\nFirst --> Second\nend";
            var renderer = new MermaidDiagramRenderer(new AvaloniaDiagramTextMeasurer());
            var failure = Assert.IsType<DiagramRenderResult.Failure>(renderer.Render(new DiagramRenderRequest(source)));
            Assert.Equal(source, failure.Source);
            Assert.False(string.IsNullOrWhiteSpace(failure.Message));
            return Task.CompletedTask;
        });
    }

    private static DiagramRenderResult.Success Render(string source)
    {
        var renderer = new MermaidDiagramRenderer(new AvaloniaDiagramTextMeasurer());
        var result = renderer.Render(new DiagramRenderRequest(source));
        Assert.True(result is DiagramRenderResult.Success, (result as DiagramRenderResult.Failure)?.Message);
        return Assert.IsType<DiagramRenderResult.Success>(result);
    }

    private static void AssertDirection(string svg, string from, string to, int dx, int dy)
    {
        var document = XDocument.Parse(svg);
        var start = NodeCenter(document, from);
        var end = NodeCenter(document, to);
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        Assert.True(deltaX * dx + deltaY * dy > 1,
            $"Expected {from} -> {to} in direction ({dx}, {dy}); actual delta ({deltaX}, {deltaY}).");
        Assert.InRange(Math.Abs(deltaX * dy - deltaY * dx), 0, 1);
    }

    private static (double X, double Y) NodeCenter(XDocument document, string label)
    {
        var element = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "foreignObject" && (string?)element.Attribute("class") == "nodeLabel"
            && element.Descendants().Any(child => child.Name.LocalName == "p" && child.Value == label));
        return (Number(element, "x") + Number(element, "width") / 2,
            Number(element, "y") + Number(element, "height") / 2);
    }

    private static double Number(XElement element, string name) =>
        double.Parse(element.Attribute(name)!.Value, CultureInfo.InvariantCulture);
}
