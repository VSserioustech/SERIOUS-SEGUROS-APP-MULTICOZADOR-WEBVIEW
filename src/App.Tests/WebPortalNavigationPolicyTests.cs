using App.Application.Configuration;
using App.Application.Models;
using App.Infrastructure.Navigation;
using Microsoft.Extensions.Options;

namespace App.Tests;

public sealed class WebPortalNavigationPolicyTests
{
    private static readonly WebPortalNavigationPolicy Policy = new(
        Options.Create(new WebPortalOptions
        {
            StartUrl = "https://portal-qa.seriouseguros.com.mx/",
            AllowedHosts = ["portal-qa.seriouseguros.com.mx"]
        }));

    [Theory]
    [InlineData("https://portal-qa.seriouseguros.com.mx/")]
    [InlineData("https://portal-qa.seriouseguros.com.mx/login")]
    [InlineData("https://PORTAL-QA.SERIOUSEGUROS.COM.MX/dashboard")]
    public void Evaluate_AllowsConfiguredHttpsHostInsideApp(string value)
    {
        var result = Policy.Evaluate(new Uri(value));

        Assert.Equal(PortalNavigationTarget.InApp, result);
    }

    [Theory]
    [InlineData("https://serioustech.com.mx/")]
    [InlineData("mailto:soporte@serioustech.com.mx")]
    [InlineData("tel:+525500000000")]
    public void Evaluate_OpensSafeExternalDestinationsOutsideApp(string value)
    {
        var result = Policy.Evaluate(new Uri(value));

        Assert.Equal(PortalNavigationTarget.External, result);
    }

    [Theory]
    [InlineData("http://portal-qa.seriouseguros.com.mx/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void Evaluate_BlocksUnsafeSchemes(string value)
    {
        var result = Policy.Evaluate(new Uri(value));

        Assert.Equal(PortalNavigationTarget.Blocked, result);
    }

    [Fact]
    public void Constructor_RequiresAtLeastOneAllowedHost()
    {
        var options = Options.Create(new WebPortalOptions());

        var exception = Assert.Throws<InvalidOperationException>(
            () => new WebPortalNavigationPolicy(options));

        Assert.Contains("AllowedHosts", exception.Message);
    }
}
