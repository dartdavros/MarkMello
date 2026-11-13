namespace MarkMello.Application.Abstractions;

/// <summary>The user's shared choice for automatic discovery; manual checks are independent.</summary>
public interface IAutomaticUpdatePreferences
{
    bool Enabled { get; }
    event EventHandler? Changed;
    bool SetEnabled(bool enabled);
}
