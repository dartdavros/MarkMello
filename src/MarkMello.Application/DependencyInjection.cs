using MarkMello.Application.Abstractions;
using MarkMello.Application.Diagrams;
using MarkMello.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace MarkMello.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<OpenDocumentUseCase>();
        services.AddSingleton<SaveDocumentUseCase>();
        services.AddSingleton<ExportPdfUseCase>();
        services.AddSingleton<RenderMarkdownDocumentUseCase>();
        services.AddSingleton<IDiagramRenderService, DiagramRenderService>();

        return services;
    }
}
