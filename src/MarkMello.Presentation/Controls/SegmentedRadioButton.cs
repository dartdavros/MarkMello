using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace MarkMello.Presentation.Controls;

/// <summary>Exclusive selection using the existing segmented ToggleButton appearance.</summary>
public sealed class SegmentedRadioButton : RadioButton
{
    protected override Type StyleKeyOverride => typeof(ToggleButton);
}
