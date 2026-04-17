using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
