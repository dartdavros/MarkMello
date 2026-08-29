using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MarkMello.Application.Abstractions;
using MarkMello.Presentation.Editing;
using MarkMello.Presentation.Localization;
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

        // TopLevel accessor: к моменту, когда FilePicker реально вызывается, окно уже создано.
        // Берём активное, а не MainWindow: со вторым окном picker обязан принадлежать тому,
        // в котором пользователь работает (ADR-0007 Rule 11).
        services.AddSingleton<Func<TopLevel?>>(_ => static () =>
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime
                is not IClassicDesktopStyleApplicationLifetime lifetime)
            {
                return null;
            }

            return lifetime.Windows.FirstOrDefault(static window => window.IsActive)
                ?? lifetime.MainWindow;
        });

        services.AddSingleton<IFilePicker, FilePicker>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Каждая editor-сессия получает свой планировщик preview: он держит
        // DispatcherTimer и номер поколения, которые нельзя делить между сессиями.
        services.AddSingleton<Func<IEditorPreviewScheduler>>(
            _ => static () => new DebouncedEditorPreviewScheduler());

        // Окно и его shell — по экземпляру на окно: вкладки и папка не общие.
        // Сервисы без состояния окна (тема, локализация, настройки) остаются singleton.
        services.AddTransient<ShellViewModel>();
        services.AddTransient<MainWindow>();
        services.AddSingleton<IWindowLauncher, WindowLauncher>();

        return services;
    }
}
