namespace Forex.Wpf;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());

                // appsettings.Production.json - exe ichiga embedded (default ApiBaseUrl).
                // Eng past ustuvorlik: quyidagi loose fayllar (dev) buni ustidan yozadi.
                var prodStream = typeof(App).Assembly
                    .GetManifestResourceStream("Forex.Wpf.appsettings.Production.json");
                if (prodStream is not null)
                    config.AddJsonStream(prodStream);

                // appsettings.json / appsettings.Development.json - faqat development uchun
                // (publish'ga kirmaydi; lokal ishga tushirishda default'ni ustidan yozadi)
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationServices(context.Configuration);
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 0. Mavzu hozircha muzlatilgan — doim yorug' rejim
        Common.Services.ThemeService.Apply(false);

        // 1. Hostni ishga tushiramiz
        await AppHost!.StartAsync();

        // 2. DI konteynerdan MainWindow ni olamiz
        var mainWindow = AppHost.Services.GetRequiredService<Windows.MainWindow>();

        // 3. Oynani ko'rsatamiz
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        AppHost.Dispose();
        base.OnExit(e);
    }
}
