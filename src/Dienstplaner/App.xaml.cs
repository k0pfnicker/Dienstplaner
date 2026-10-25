using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Dienstplaner.Domain.Services;
using Dienstplaner.Infrastructure;
using Dienstplaner.ViewModels;

namespace Dienstplaner;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        ServiceProvider = services.BuildServiceProvider();

        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        CultureInfo.CurrentUICulture = new CultureInfo("de-DE");

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddTransient<MainWindow>();
        services.AddTransient<MainViewModel>();
        
        services.AddSingleton<ScheduleSolver>();
        services.AddSingleton<PdfScheduleImporter>();
        services.AddSingleton<WordScheduleImporter>();
        services.AddSingleton<PdfScheduleExporter>();
        services.AddSingleton<DocScheduleExporter>();
    }
}
