using App.Application.Configuration;
using App.Application.Interfaces;
using App.Application.Models;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.Navigation;

public sealed class WebPortalNavigationPolicy : IWebPortalNavigationPolicy
{
    private readonly HashSet<string> _allowedHosts;

    public WebPortalNavigationPolicy(IOptions<WebPortalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _allowedHosts = options.Value.AllowedHosts
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(NormalizeHost)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_allowedHosts.Count == 0)
        {
            throw new InvalidOperationException(
                $"{WebPortalOptions.SectionName}:AllowedHosts must contain at least one host.");
        }
    }

    public PortalNavigationTarget Evaluate(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.IsAbsoluteUri)
        {
            return PortalNavigationTarget.Blocked;
        }

        if (destination.Scheme is "mailto" or "tel")
        {
            return PortalNavigationTarget.External;
        }

        if (!string.Equals(destination.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return PortalNavigationTarget.Blocked;
        }

        return _allowedHosts.Contains(NormalizeHost(destination.Host))
            ? PortalNavigationTarget.InApp
            : PortalNavigationTarget.External;
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.');
}
