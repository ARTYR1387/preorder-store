using Avalonia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PreOrderStore.Data;
using PreOrderStore.Infrastructure;
using PreOrderStore.Services;
using PreOrderStore.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PreOrderStore;

internal static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PreOrderStore");
        Directory.CreateDirectory(dataDirectory);

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddDbContext<StoreDbContext>(options =>
                    options.UseSqlite($"Data Source={Path.Combine(dataDirectory, "preorder-store.db")}"));
                services.AddHttpClient<IProductProvider, DummyJsonProductProvider>(client =>
                {
                    client.BaseAddress = new Uri("https://dummyjson.com/");
                    client.Timeout = TimeSpan.FromSeconds(20);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("PreOrderStore/1.0");
                });
                services.AddSingleton<UserSession>();
                services.AddScoped<AuthService>();
                services.AddScoped<CatalogSyncService>();
                services.AddScoped<StoreService>();
                services.AddScoped<DemoBankPaymentService>();
                services.AddScoped<MainViewModel>();
            })
            .Build();

        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StoreDbContext>().Database.MigrateAsync();
        App.Services = scope.ServiceProvider;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
#if DEBUG
        .WithDeveloperTools()
#endif
        .WithInterFont()
        .LogToTrace();
}
