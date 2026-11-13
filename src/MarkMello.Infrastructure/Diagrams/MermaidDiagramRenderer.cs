using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MarkMello.Infrastructure.Diagrams.Sequence;
using Naiad;

namespace MarkMello.Infrastructure.Diagrams;

/// <summary>
/// Mandatory <see cref="IDiagramRenderer"/> for <see cref="MarkdownDiagramKind.Mermaid"/>.
/// Wraps the Naiad managed library: in-process, no browser/Node/network/external
/// process — see ADR-0005 Decision 4 and the M0 spike note in
/// <c>tests/m0-naiad-spike.md</c>.
///
/// Failure policy: a backend exception raised for an individual diagram is
/// converted to <see cref="DiagramRenderResult.Failure"/> so one bad fence
/// does not crash the document. Composition errors (missing/duplicate
/// renderer) live outside this class and surface from
/// <c>DiagramRenderService</c>.
/// </summary>
public sealed class MermaidDiagramRenderer : IDiagramRenderer
{
    private readonly IDiagramTextMeasurer _textMeasurer;

    public MermaidDiagramRenderer(IDiagramTextMeasurer textMeasurer)
    {
        ArgumentNullException.ThrowIfNull(textMeasurer);
        _textMeasurer = textMeasurer;
    }

    public MarkdownDiagramKind Kind => MarkdownDiagramKind.Mermaid;

    public DiagramRenderResult Render(DiagramRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = request.Source ?? string.Empty;

        try
        {
            // Options are built per call: edit-mode preview renders off the UI
            // thread, so nothing here may be shared mutable state.
            var options = new RenderOptions();
            var svg = Mermaid.Render(source, options);
            svg = SequenceSvgLayout.Apply(source, svg, options, _textMeasurer);
            if (!string.IsNullOrEmpty(svg))
            {
                svg = MermaidSvgCanvas.AddBackground(svg);
            }
            return string.IsNullOrEmpty(svg)
                ? new DiagramRenderResult.Failure("Mermaid produced empty SVG output.", source)
                : new DiagramRenderResult.Success(svg);
        }
        catch (MermaidException ex)
        {
            return new DiagramRenderResult.Failure(ex.Message, source);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Backend safety net: Naiad may surface internal parser/layout
            // failures through non-Mermaid exception types. We do NOT swallow
            // composition or environment errors (those propagate as
            // OutOfMemoryException/StackOverflowException), only diagram-
            // specific failures.
            return new DiagramRenderResult.Failure(ex.Message, source);
        }
    }
}
