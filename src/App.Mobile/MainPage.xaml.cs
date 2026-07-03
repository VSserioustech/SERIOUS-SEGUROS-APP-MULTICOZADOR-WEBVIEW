using App.Application.Configuration;
using App.Application.Interfaces;
using App.Application.Models;
using Microsoft.Extensions.Options;

namespace App.Mobile;

public partial class MainPage : ContentPage
{
    private readonly IWebPortalNavigationPolicy _navigationPolicy;
    private readonly Uri _startUri;

    public MainPage(
        IOptions<WebPortalOptions> options,
        IWebPortalNavigationPolicy navigationPolicy)
    {
        InitializeComponent();

        _navigationPolicy = navigationPolicy;
        _startUri = CreateStartUri(options.Value.StartUrl);
        portalWebView.Source = _startUri.AbsoluteUri;
    }

    private async void OnPortalNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var destination))
        {
            e.Cancel = true;
            await ShowBlockedNavigationAsync();
            return;
        }

        switch (_navigationPolicy.Evaluate(destination))
        {
            case PortalNavigationTarget.InApp:
                ShowLoading();
                break;
            case PortalNavigationTarget.External:
                e.Cancel = true;
                if (!await Launcher.Default.TryOpenAsync(destination))
                {
                    await DisplayAlertAsync(
                        "Enlace externo",
                        "No fue posible abrir este enlace en el dispositivo.",
                        "Aceptar");
                }

                break;
            default:
                e.Cancel = true;
                await ShowBlockedNavigationAsync();
                break;
        }
    }

    private void OnPortalNavigated(object? sender, WebNavigatedEventArgs e)
    {
        loadingOverlay.IsVisible = false;

        if (e.Result == WebNavigationResult.Success)
        {
            errorOverlay.IsVisible = false;
            return;
        }

        ShowError("Revisa tu conexión e inténtalo nuevamente.");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (portalWebView.CanGoBack)
        {
            portalWebView.GoBack();
        }
    }

    private void OnHomeClicked(object? sender, EventArgs e)
    {
        NavigateHome();
    }

    private void OnReloadClicked(object? sender, EventArgs e)
    {
        ShowLoading();
        portalWebView.Reload();
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        NavigateHome();
    }

    private void NavigateHome()
    {
        ShowLoading();
        portalWebView.Source = _startUri.AbsoluteUri;
    }

    private void ShowLoading()
    {
        errorOverlay.IsVisible = false;
        loadingOverlay.IsVisible = true;
    }

    private void ShowError(string message)
    {
        loadingOverlay.IsVisible = false;
        errorMessageLabel.Text = message;
        errorOverlay.IsVisible = true;
    }

    private async Task ShowBlockedNavigationAsync()
    {
        await DisplayAlertAsync(
            "Navegación bloqueada",
            "La aplicación bloqueó un destino no seguro.",
            "Aceptar");
    }

    private static Uri CreateStartUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        throw new InvalidOperationException(
            $"{WebPortalOptions.SectionName}:StartUrl must be an absolute HTTPS URL.");
    }
}
