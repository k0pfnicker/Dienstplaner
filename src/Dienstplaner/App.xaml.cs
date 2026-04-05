using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Dienstplaner.ViewModels;
using Dienstplaner.Domain.Services;
using Dienstplaner.Application.Interfaces;
using Dienstplaner.Infrastructure;
using System.Globalization;

namespace Dienstplaner;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        ServiceProvider = services.BuildServiceProvider();

        // Ensure German culture for parsing days and months
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        CultureInfo.CurrentUICulture = new CultureInfo("de-DE");

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddTransient<MainWindow>();
        services.AddTransient<MainViewModel>();
        
        services.AddSingleton<ShiftCodeInterpreter>();
        services.AddTransient<IScheduleImporter, WordScheduleImporter>();
    }
}
