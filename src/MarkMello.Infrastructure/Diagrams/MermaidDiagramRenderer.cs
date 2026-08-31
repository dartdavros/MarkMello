using MarkMello.Application.Abstractions;
using MarkMello.Domain;
using MermaidSharp;

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
    public MarkdownDiagramKind Kind => MarkdownDiagramKind.Mermaid;

    public DiagramRenderResult Render(DiagramRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = request.Source ?? string.Empty;

        try
        {
            // Options are built per call: edit-mode preview renders off the UI
            // thread, so nothing here may be shared mutable state.
            var svg = Mermaid.Render(source, new RenderOptions());
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
