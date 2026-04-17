using Avalonia.Styling;
using Avalonia.Threading;
using MarkMello.Application.Abstractions;
using MarkMello.Domain;

namespace MarkMello.Presentation.Services;

/// <summary>
/// Применяет тему к <see cref="global::Avalonia.Application.RequestedThemeVariant"/>.
/// Маршалинг на UI-поток, потому что вызов может прийти из любого контекста.
/// </summary>
public sealed class AvaloniaThemeService : IThemeService
{
    public void Apply(ThemeMode mode)
    {
        var variant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyCore(variant);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ApplyCore(variant));
        }
    }

    private static void ApplyCore(ThemeVariant variant)
    {
        var app = global::Avalonia.Application.Current;
        if (app is not null)
        {
            app.RequestedThemeVariant = variant;
        }
    }
}
