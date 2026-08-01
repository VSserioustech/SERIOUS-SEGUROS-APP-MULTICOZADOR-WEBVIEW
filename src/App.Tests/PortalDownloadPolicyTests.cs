using App.Application.Configuration;
using App.Infrastructure.Downloads;
using Microsoft.Extensions.Options;

namespace App.Tests;

public sealed class PortalDownloadPolicyTests
{
    private static readonly PortalDownloadPolicy Policy = new(
        Options.Create(new WebPortalOptions
        {
            StartUrl = "https://portal-qa.seriouseguros.com.mx/",
            AllowedHosts = ["portal-qa.seriouseguros.com.mx"]
        }));

    [Theory]
    [InlineData("https://portal-qa.seriouseguros.com.mx/historial/cotizacion/123.pdf")]
    [InlineData("https://portal-qa.seriouseguros.com.mx/api/cotizaciones/123/download?format=zip")]
    [InlineData("https://portal-qa.seriouseguros.com.mx/api/download?file=cotizacion.pdf")]
    public void IsDownload_DetectsPortalPdfAndZipDownloads(string value)
    {
        Assert.True(Policy.IsDownload(new Uri(value)));
    }

    [Theory]
    [InlineData("https://portal-qa.seriouseguros.com.mx/historial")]
    [InlineData("https://serioustech.com.mx/cotizacion.pdf")]
    [InlineData("http://portal-qa.seriouseguros.com.mx/cotizacion.pdf")]
    public void IsDownload_IgnoresRegularExternalOrUnsafeNavigation(string value)
    {
        Assert.False(Policy.IsDownload(new Uri(value)));
    }
}
