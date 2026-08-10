using App.Application.Interfaces;
using App.Infrastructure.Downloads;
using App.Infrastructure.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPortalDownloadPolicy, PortalDownloadPolicy>();
        services.AddSingleton<IWebPortalNavigationPolicy, WebPortalNavigationPolicy>();
        return services;
    }
}
