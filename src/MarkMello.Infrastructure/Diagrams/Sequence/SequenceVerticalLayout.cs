using Naiad;
using Naiad.Diagrams.Sequence;

namespace MarkMello.Infrastructure.Diagrams.Sequence;

internal sealed class SequenceVerticalLayout
{
    public SequenceVerticalLayout(SequenceModel model, SequenceDiagramSettings settings,
        RenderOptions options, double textHeight)
    {
        var titleOffset = string.IsNullOrEmpty(model.Title) ? 0 : 30;
        HeaderY = options.Padding + titleOffset;
        var headerHeight = model.Participants.Any(static participant => participant.Type == ParticipantType.Actor)
            ? Math.Max(40, 55 + textHeight + 4) : 40;
        LifelineStartY = HeaderY + headerHeight;
        var oldY = HeaderY + 40 + 50;
        var newY = LifelineStartY + Math.Max(50, settings.MessageMargin);
        foreach (var element in model.Elements)
        {
            if (element is Activation)
            {
                continue;
            }

            Rows.Add(new SequenceRow(element, oldY, newY));
            oldY += 50;
            newY += element is Note ? 40 + Math.Max(settings.MessageMargin, textHeight + 8)
                : element is Message message && message.FromId == message.ToId
                    ? Math.Max(50, settings.MessageMargin + 30)
                    : Math.Max(settings.MessageMargin, textHeight + 16);
        }

        OriginalFooterY = oldY;
        FooterY = newY;
        Height = FooterY + headerHeight + options.Padding;
    }

    public List<SequenceRow> Rows { get; } = [];
    public double HeaderY { get; }
    public double LifelineStartY { get; }
    public double OriginalFooterY { get; }
    public double FooterY { get; }
    public double Height { get; }

    public double MapY(double y)
    {
        if (y >= OriginalFooterY - 1)
        {
            return FooterY + y - OriginalFooterY;
        }

        if (Rows.Count == 0)
        {
            return y;
        }

        var row = Rows.MinBy(row => Math.Abs(row.OriginalY - y))!;
        return row.Y + y - row.OriginalY;
    }
}

internal sealed record SequenceRow(SequenceElement Element, double OriginalY, double Y);
