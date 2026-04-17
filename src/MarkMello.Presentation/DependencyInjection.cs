using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MarkMello.Application.Abstractions;
using MarkMello.Presentation.Services;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TopLevel accessor: к моменту, когда FilePicker реально вызывается, MainWindow уже создан.
        // На этапе DI build окно ещё не существует — поэтому только Func, не значение.
        services.AddSingleton<Func<TopLevel?>>(_ => static () =>
        {
            var lifetime = global::Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime;
            return lifetime?.MainWindow;
        });

        services.AddSingleton<IFilePicker, FilePicker>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
