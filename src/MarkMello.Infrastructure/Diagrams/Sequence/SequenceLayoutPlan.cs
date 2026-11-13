using MarkMello.Application.Abstractions;
using Naiad;
using Naiad.Diagrams.Sequence;

namespace MarkMello.Infrastructure.Diagrams.Sequence;

internal sealed class SequenceLayoutPlan
{
    public const double OriginalParticipantWidth = 100;
    private readonly Dictionary<string, int> _indices;
    private readonly double[] _positions;
    private readonly double[] _widths;
    private readonly Dictionary<Message, string> _messageLabels;
    private readonly SequenceVerticalLayout _vertical;

    public SequenceLayoutPlan(SequenceModel model, SequenceDiagramSettings settings,
        RenderOptions options, IDiagramTextMeasurer measurer)
    {
        Model = model;
        _messageLabels = model.Elements.OfType<Message>().Select((message, index) =>
            (Message: message, Label: model.AutoNumber ? $"{index + 1}. {message.Text}" : message.Text ?? string.Empty))
            .ToDictionary(static item => item.Message, static item => item.Label);
        OriginalPositions = model.Participants.Select((_, index) => options.Padding + 50 + index * 150).ToArray();
        _indices = model.Participants.Select((participant, index) => (participant.Id, index))
            .ToDictionary(static item => item.Id, static item => item.index, StringComparer.Ordinal);
        _widths = model.Participants.Select(participant => Math.Max(settings.Width,
            measurer.Measure(participant.DisplayName, options.FontFamily, options.FontSize).Width + 20)).ToArray();
        _positions = new double[_widths.Length];
        var gaps = Enumerable.Range(1, _widths.Length - 1)
            .Select(index => (_widths[index - 1] + _widths[index]) / 2 + settings.ActorMargin).ToArray();

        foreach (var message in model.Elements.OfType<Message>().Where(static message => message.FromId != message.ToId))
        {
            var from = _indices[message.FromId];
            var to = _indices[message.ToId];
            var left = Math.Min(from, to);
            var right = Math.Max(from, to);
            var labelWidth = measurer.Measure(_messageLabels[message], options.FontFamily, options.FontSize).Width + 24;
            var extra = Math.Max(0, labelWidth - gaps[left..right].Sum()) / (right - left);
            for (var index = left; index < right; index++)
            {
                gaps[index] += extra;
            }
        }

        _positions[0] = options.Padding + _widths[0] / 2;
        for (var index = 1; index < _positions.Length; index++)
        {
            _positions[index] = _positions[index - 1] + gaps[index - 1];
        }

        var textHeight = measurer.Measure("Ag", options.FontFamily, options.FontSize).Height;
        _vertical = new SequenceVerticalLayout(model, settings, options, textHeight);

        var minX = options.Padding;
        var maxX = _positions[^1] + _widths[^1] / 2;
        foreach (var note in model.Elements.OfType<Note>())
        {
            var bounds = NoteBounds(note, measurer.Measure(note.Text, options.FontFamily, options.FontSize).Width);
            minX = Math.Min(minX, bounds.X);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
        }

        foreach (var message in model.Elements.OfType<Message>().Where(static message => message.FromId == message.ToId))
        {
            maxX = Math.Max(maxX, Position(message.FromId) + 45
                + measurer.Measure(_messageLabels[message], options.FontFamily, options.FontSize).Width);
        }

        var shift = Math.Max(0, options.Padding - minX);
        for (var index = 0; index < _positions.Length; index++)
        {
            _positions[index] += shift;
        }

        Width = maxX + shift + options.Padding;
        if (!string.IsNullOrEmpty(model.Title))
        {
            Width = Math.Max(Width, measurer.Measure(model.Title, options.FontFamily, 16).Width + options.Padding * 2);
        }
    }

    public SequenceModel Model { get; }
    public double[] OriginalPositions { get; }
    public List<SequenceRow> Rows => _vertical.Rows;
    public double HeaderY => _vertical.HeaderY;
    public double LifelineStartY => _vertical.LifelineStartY;
    public double OriginalFooterY => _vertical.OriginalFooterY;
    public double FooterY => _vertical.FooterY;
    public double Width { get; }
    public double Height => _vertical.Height;

    public double Position(string id) => _positions[_indices[id]];
    public double Position(int index) => _positions[index];
    public double ParticipantWidth(int index) => _widths[index];

    public (double X, double Width) NoteBounds(Note note, double textWidth)
    {
        var center = Position(note.ParticipantId);
        var participantWidth = _widths[_indices[note.ParticipantId]];
        var width = Math.Max(120, textWidth + 24);
        if (note.Position == NotePosition.LeftOf)
        {
            return (center - participantWidth / 2 - 10 - width, width);
        }

        if (note.Position == NotePosition.RightOf)
        {
            return (center + participantWidth / 2 + 10, width);
        }

        if (note.OverParticipantId2 is { } second)
        {
            var other = Position(second);
            var otherWidth = _widths[_indices[second]];
            var left = Math.Min(center - participantWidth / 2, other - otherWidth / 2);
            var right = Math.Max(center + participantWidth / 2, other + otherWidth / 2);
            width = Math.Max(width, right - left);
            center = (left + right) / 2;
        }

        return (center - width / 2, width);
    }

    public double MapX(double x, bool preserveEndpoint = false)
    {
        for (var index = 0; index < OriginalPositions.Length; index++)
        {
            if (Math.Abs(x - OriginalPositions[index]) <= (preserveEndpoint ? 16 : 0.01))
            {
                return _positions[index] + x - OriginalPositions[index];
            }
        }

        for (var index = 1; index < OriginalPositions.Length; index++)
        {
            if (x <= OriginalPositions[index] && x >= OriginalPositions[index - 1])
            {
                var ratio = (x - OriginalPositions[index - 1]) / 150;
                return _positions[index - 1] + ratio * (_positions[index] - _positions[index - 1]);
            }
        }

        var nearest = x < OriginalPositions[0] ? 0 : OriginalPositions.Length - 1;
        return _positions[nearest] + x - OriginalPositions[nearest];
    }

    public double MapY(double y) => _vertical.MapY(y);
}
