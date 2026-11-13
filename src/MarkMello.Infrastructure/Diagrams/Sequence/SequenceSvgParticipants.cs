using System.Xml.Linq;

namespace MarkMello.Infrastructure.Diagrams.Sequence;

internal static class SequenceSvgParticipants
{
    public static HashSet<XElement> Rewrite(IReadOnlyList<XElement> elements, SequenceLayoutPlan plan)
    {
        var handled = new HashSet<XElement>();
        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            var kind = element.Name.LocalName;
            if ((string?)element.Attribute("fill") != "#ECECFF" || kind is not ("rect" or "circle"))
            {
                continue;
            }

            // Naiad emits a box + label, or a stick figure + label, in this order.
            var actor = kind == "circle";
            var oldCenter = actor ? SequenceSvgGeometry.Number(element, "cx")
                : SequenceSvgGeometry.Number(element, "x") + SequenceLayoutPlan.OriginalParticipantWidth / 2;
            var participant = Array.FindIndex(plan.OriginalPositions, position => Math.Abs(position - oldCenter) < 0.01);
            if (participant < 0)
            {
                continue;
            }

            var oldY = actor ? SequenceSvgGeometry.Number(element, "cy") - 15
                : SequenceSvgGeometry.Number(element, "y");
            var newY = oldY >= plan.OriginalFooterY ? plan.FooterY : plan.HeaderY;
            var deltaX = plan.Position(participant) - oldCenter;
            var deltaY = newY - oldY;
            var count = actor ? 6 : 2;
            foreach (var part in elements.Skip(index).Take(count))
            {
                SequenceSvgGeometry.Transform(part, x => x + deltaX, y => y + deltaY);
                handled.Add(part);
            }

            if (actor)
            {
                elements[index + count - 1].SetAttributeValue("dominant-baseline", "hanging");
            }
            else
            {
                SequenceSvgGeometry.Set(element, "x", plan.Position(participant) - plan.ParticipantWidth(participant) / 2);
                SequenceSvgGeometry.Set(element, "width", plan.ParticipantWidth(participant));
            }
        }

        foreach (var line in elements.Where(static element => element.Name.LocalName == "line"
            && (string?)element.Attribute("stroke") == "#999"))
        {
            SequenceSvgGeometry.Set(line, "x1", plan.MapX(SequenceSvgGeometry.Number(line, "x1")));
            SequenceSvgGeometry.Set(line, "x2", plan.MapX(SequenceSvgGeometry.Number(line, "x2")));
            SequenceSvgGeometry.Set(line, "y1", plan.LifelineStartY);
            SequenceSvgGeometry.Set(line, "y2", plan.FooterY);
            handled.Add(line);
        }

        return handled;
    }
}
