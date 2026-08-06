using System.Reflection;
using App.Application;
using App.Application.Configuration;
using App.Infrastructure;
using App.Mobile.Services;
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
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<WebView, Platforms.Android.CustomWebViewHandler>();
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        ConfigureAppSettings(builder);
        PlatformWebViewConfiguration.Configure();

        builder.Services.Configure<WebPortalOptions>(
            builder.Configuration.GetSection(WebPortalOptions.SectionName));
        builder.Services.Configure<WhitelabelOptions>(
            builder.Configuration.GetSection(WhitelabelOptions.SectionName));
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure();
        builder.Services.AddSingleton<IWebViewCookieProvider, WebViewCookieProvider>();
        builder.Services.AddSingleton<IPortalFileDownloader, PortalFileDownloader>();
        builder.Services.AddSingleton<IPortalCredentialStore, PortalCredentialStore>();
#if ANDROID
        builder.Services.AddSingleton<ILauncherBrandService, Platforms.Android.LauncherBrandService>();
#else
        builder.Services.AddSingleton<ILauncherBrandService, LauncherBrandService>();
#endif
        builder.Services.AddSingleton<IWhitelabelState, WhitelabelState>();
        builder.Services.AddSingleton<IWhitelabelClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WhitelabelOptions>>().Value;
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(8)
            };

            return new WhitelabelClient(
                httpClient,
                serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WhitelabelOptions>>());
        });
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<TenantLoginPage>();
        builder.Services.AddTransient<WhitelabelWizardPage>();

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
