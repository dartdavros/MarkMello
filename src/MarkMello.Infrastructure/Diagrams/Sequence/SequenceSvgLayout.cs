using System.Text.RegularExpressions;
using System.Xml.Linq;
using MarkMello.Application.Abstractions;
using Naiad;
using Naiad.Diagrams.Sequence;

namespace MarkMello.Infrastructure.Diagrams.Sequence;

/// <summary>
/// Fits Naiad's sequence geometry to native text measurements while retaining its parser and SVG primitives.
/// Labels are measured by the same native font engine that displays the resulting SVG.
/// </summary>
internal static partial class SequenceSvgLayout
{
    public static string Apply(string source, string svg, RenderOptions options, IDiagramTextMeasurer measurer)
    {
        var (diagramSource, settings) = SequenceDiagramSettings.Read(source);
        if (!diagramSource.StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return svg;
        }

        var parsed = new SequenceParser().Parse(diagramSource);
        if (!parsed.Success || parsed.Value.Participants.Count == 0)
        {
            return svg;
        }

        var plan = new SequenceLayoutPlan(parsed.Value, settings, options, measurer);
        var document = XDocument.Parse(svg);
        var root = document.Root!;
        var elements = root.Elements().ToList();
        var handled = SequenceSvgParticipants.Rewrite(elements, plan);
        RewriteNotes(elements, plan, options, measurer, handled);
        foreach (var element in elements.Where(element => !handled.Contains(element)))
        {
            RewriteElement(element, plan);
        }

        root.SetAttributeValue("viewBox", $"0 0 {SequenceSvgGeometry.Format(plan.Width)} {SequenceSvgGeometry.Format(plan.Height)}");
        root.SetAttributeValue("style", $"max-width: {SequenceSvgGeometry.Format(plan.Width)}px;");
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static void RewriteNotes(List<XElement> elements, SequenceLayoutPlan plan,
        RenderOptions options, IDiagramTextMeasurer measurer, HashSet<XElement> handled)
    {
        using var notes = plan.Rows.Where(static row => row.Element is Note).GetEnumerator();
        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            if (element.Name.LocalName != "path" || (string?)element.Attribute("fill") != "#FFFFCC"
                || !notes.MoveNext())
            {
                continue;
            }

            var row = notes.Current;
            var note = (Note)row.Element;
            var label = measurer.Measure(note.Text, options.FontFamily, options.FontSize);
            var (x, width) = plan.NoteBounds(note, label.Width);
            var y = row.Y;
            // Retain Naiad's folded note, making its span match the referenced lifelines.
            element.SetAttributeValue("d", FormattableString.Invariant(
                $"M{x:0.###},{y:0.###} L{x + width - 8:0.###},{y:0.###} L{x + width:0.###},{y + 8:0.###} L{x + width:0.###},{y + 40:0.###} L{x:0.###},{y + 40:0.###} Z"));
            var foldVertical = elements[index + 1];
            SequenceSvgGeometry.Set(foldVertical, "x1", x + width - 8);
            SequenceSvgGeometry.Set(foldVertical, "x2", x + width - 8);
            SequenceSvgGeometry.Set(foldVertical, "y1", y);
            SequenceSvgGeometry.Set(foldVertical, "y2", y + 8);
            var foldHorizontal = elements[index + 2];
            SequenceSvgGeometry.Set(foldHorizontal, "x1", x + width - 8);
            SequenceSvgGeometry.Set(foldHorizontal, "x2", x + width);
            SequenceSvgGeometry.Set(foldHorizontal, "y1", y + 8);
            SequenceSvgGeometry.Set(foldHorizontal, "y2", y + 8);
            var text = elements[index + 3];
            SequenceSvgGeometry.Set(text, "x", x + width / 2);
            SequenceSvgGeometry.Set(text, "y", y + 20);
            foreach (var part in elements.Skip(index).Take(4))
            {
                handled.Add(part);
            }
        }
    }

    private static void RewriteElement(XElement element, SequenceLayoutPlan plan)
    {
        var kind = element.Name.LocalName;
        if (kind == "text" && !string.IsNullOrEmpty(plan.Model.Title)
            && element.Value == plan.Model.Title && SequenceSvgGeometry.Number(element, "y") == 20)
        {
            SequenceSvgGeometry.Set(element, "x", plan.Width / 2);
            return;
        }

        var oldY = (double?)null;
        if (element.Attribute("y") is not null)
        {
            oldY = SequenceSvgGeometry.Number(element, "y");
        }
        else if (element.Attribute("d") is { } path)
        {
            var match = PathStartPattern().Match(path.Value);
            if (match.Success)
            {
                oldY = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        Func<double, double> mapX = x => plan.MapX(x, preserveEndpoint: kind != "text");
        Func<double, double> mapY = plan.MapY;
        if (oldY.HasValue && plan.Rows.Count > 0)
        {
            var row = plan.Rows.MinBy(row => Math.Abs(row.OriginalY - oldY.Value))!;
            if (row.Element is Message message && message.FromId == message.ToId)
            {
                var participant = plan.Model.Participants.FindIndex(participant => participant.Id == message.FromId);
                mapX = x => plan.Position(participant) + x - plan.OriginalPositions[participant];
                mapY = y => row.Y + y - row.OriginalY;
            }
        }

        if (kind == "rect" && element.Attribute("height") is not null)
        {
            var y = SequenceSvgGeometry.Number(element, "y");
            var height = SequenceSvgGeometry.Number(element, "height");
            SequenceSvgGeometry.Set(element, "height", plan.MapY(y + height) - plan.MapY(y));
        }

        SequenceSvgGeometry.Transform(element, mapX, mapY);
    }

    [GeneratedRegex(@"^M-?\d+(?:\.\d+)?,(-?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex PathStartPattern();
}
