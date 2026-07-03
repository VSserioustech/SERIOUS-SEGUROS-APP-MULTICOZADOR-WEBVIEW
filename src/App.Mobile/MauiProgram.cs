using System.Reflection;
using App.Application;
using App.Application.Configuration;
using App.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace App.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        ConfigureAppSettings(builder);
        PlatformWebViewConfiguration.Configure();

        builder.Services.Configure<WebPortalOptions>(
            builder.Configuration.GetSection(WebPortalOptions.SectionName));
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureAppSettings(MauiAppBuilder builder)
    {
        var environmentName = Environment.GetEnvironmentVariable("APP_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environmentName))
        {
#if DEBUG
            environmentName = "Development";
#else
            environmentName = "Production";
#endif
        }

        AddEmbeddedJson(builder.Configuration, "appsettings.json");
        AddEmbeddedJson(builder.Configuration, $"appsettings.{environmentName}.json");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:EnvironmentName"] = environmentName
        });
    }

    private static void AddEmbeddedJson(ConfigurationManager configuration, string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{typeof(MauiProgram).Namespace}.Configuration.{fileName}";
        using var resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream is null)
        {
            return;
        }

        var stream = new MemoryStream();
        resourceStream.CopyTo(stream);
        stream.Position = 0;
        configuration.AddJsonStream(stream);
    }
}
