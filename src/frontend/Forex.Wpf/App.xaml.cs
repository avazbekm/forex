namespace Forex.Wpf;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
#if DEBUG
        const string environment = "Development";
#else
        const string environment = "Production";
#endif

        AppHost = Host.CreateDefaultBuilder()
            .UseEnvironment(environment)
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = false;
                options.ValidateOnBuild = false;
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());

                // appsettings.Production.json - exe ichiga embedded (default ApiBaseUrl).
                // Eng past ustuvorlik (Sources[0]): har qanday loose fayl buni ustidan yozadi.
                var prodStream = typeof(App).Assembly
                    .GetManifestResourceStream("Forex.Wpf.appsettings.Production.json");
                if (prodStream is not null)
                    config.Sources.Insert(0, new JsonStreamConfigurationSource { Stream = prodStream });

                // Joriy muhitga mos fayl (eng yuqori ustuvorlik). Development'da ishga tushirilsa
                // appsettings.Development.json olinadi, Production'da embedded default qoladi.
                var env = context.HostingEnvironment.EnvironmentName;
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationServices(context.Configuration);
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 0. Saqlangan mavzuni qo'llaymiz (yorug'/tungi)
        Common.Services.ThemeService.Apply(Common.Services.AppPreferences.Instance.DarkTheme);

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
