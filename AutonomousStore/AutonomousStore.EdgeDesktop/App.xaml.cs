using System.Windows;
using AutonomousStore.EdgeDesktop.Configuration;
using AutonomousStore.EdgeDesktop.Services;
using AutonomousStore.EdgeDesktop.ViewModels;
using AutonomousStore.Hardware.Interfaces;
using AutonomousStore.Hardware.Mocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AutonomousStore.EdgeDesktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<ApiSettings>(context.Configuration.GetSection(ApiSettings.SectionName));

                var apiSettings = context.Configuration
                    .GetSection(ApiSettings.SectionName)
                    .Get<ApiSettings>() ?? new ApiSettings();

                services.AddHttpClient<IProductApiService, ProductApiService>(client =>
                {
                    client.BaseAddress = new Uri(apiSettings.BaseUrl.TrimEnd('/') + "/");
                });

                services.AddHttpClient<ISessionApiService, SessionApiService>(client =>
                {
                    client.BaseAddress = new Uri(apiSettings.BaseUrl.TrimEnd('/') + "/");
                });

                services.AddSingleton<ICartService, CartService>();
                services.AddSingleton<IRfidReader, MockRfidReader>();
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
