using MarkMello.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<OpenDocumentUseCase>();
        services.AddSingleton<RenderMarkdownDocumentUseCase>();

        return services;
    }
}
