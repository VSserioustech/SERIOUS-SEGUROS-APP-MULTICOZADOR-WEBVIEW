namespace App.Mobile.Services;

public static class WhitelabelLogoSource
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public static ImageSource FromFallbackAsset(string? launcherIconKey) =>
        GetFallbackLogoAsset(launcherIconKey);

    public static async Task<ImageSource> CreateAsync(
        string? logoUrl,
        string? launcherIconKey,
        CancellationToken cancellationToken = default)
    {
        var fallbackLogoAsset = GetFallbackLogoAsset(launcherIconKey);
        if (!Uri.TryCreate(logoUrl?.Trim(), UriKind.Absolute, out var logoUri))
        {
            return fallbackLogoAsset;
        }

        if (IsSvgLogo(logoUri))
        {
            return fallbackLogoAsset;
        }

        try
        {
            using var response = await HttpClient.GetAsync(logoUri, cancellationToken);
            if (!response.IsSuccessStatusCode || IsSvgContent(response.Content.Headers.ContentType?.MediaType))
            {
                return fallbackLogoAsset;
            }

            var logoBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (logoBytes.Length == 0)
            {
                return fallbackLogoAsset;
            }

            return ImageSource.FromStream(() => new MemoryStream(logoBytes));
        }
        catch (HttpRequestException)
        {
            return fallbackLogoAsset;
        }
        catch (TaskCanceledException)
        {
            return fallbackLogoAsset;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return fallbackLogoAsset;
        }
    }

    private static string GetFallbackLogoAsset(string? launcherIconKey) =>
        launcherIconKey?.Trim().ToLowerInvariant() switch
        {
            "ali" => "whitelabel_ali.png",
            "cbe" => "whitelabel_cbe_full.png",
            "oak" => "whitelabel_oak.png",
            _ => "whitelabel_serious.png"
        };

    private static bool IsSvgLogo(Uri logoUri) =>
        logoUri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

    private static bool IsSvgContent(string? mediaType) =>
        string.Equals(mediaType, "image/svg+xml", StringComparison.OrdinalIgnoreCase);
}
