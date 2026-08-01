using App.Application.Configuration;
using App.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Downloads;

public sealed class PortalDownloadPolicy : IPortalDownloadPolicy
{
    private static readonly string[] DownloadExtensions = [".pdf", ".zip"];

    private static readonly string[] DownloadKeywords =
    [
        "download",
        "descarga",
        "cotizacion",
        "quote"
    ];

    private readonly HashSet<string> _allowedHosts;

    public PortalDownloadPolicy(IOptions<WebPortalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _allowedHosts = options.Value.AllowedHosts
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(NormalizeHost)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsDownload(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.IsAbsoluteUri ||
            !string.Equals(destination.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !_allowedHosts.Contains(NormalizeHost(destination.Host)))
        {
            return false;
        }

        var path = Uri.UnescapeDataString(destination.AbsolutePath);
        if (DownloadExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var searchableUrl = Uri.UnescapeDataString(destination.PathAndQuery);
        return DownloadKeywords.Any(keyword =>
            searchableUrl.Contains(keyword, StringComparison.OrdinalIgnoreCase)) &&
            (DownloadExtensions.Any(extension =>
                searchableUrl.Contains(extension, StringComparison.OrdinalIgnoreCase)) ||
            searchableUrl.Contains("format=pdf", StringComparison.OrdinalIgnoreCase) ||
            searchableUrl.Contains("format=zip", StringComparison.OrdinalIgnoreCase) ||
            searchableUrl.Contains("type=pdf", StringComparison.OrdinalIgnoreCase) ||
            searchableUrl.Contains("type=zip", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.');
}
