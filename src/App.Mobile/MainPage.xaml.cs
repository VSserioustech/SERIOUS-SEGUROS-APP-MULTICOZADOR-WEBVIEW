using App.Application.Configuration;
using App.Application.Interfaces;
using App.Application.Models;
using App.Mobile.Services;
using Microsoft.Extensions.Options;

namespace App.Mobile;

public partial class MainPage : ContentPage, ISystemBarsPage
{
    private readonly IWebPortalNavigationPolicy _navigationPolicy;
    private readonly IPortalDownloadPolicy _downloadPolicy;
    private readonly IPortalFileDownloader _fileDownloader;
    private readonly IWhitelabelState _whitelabelState;
    private readonly WhitelabelConfig? _whitelabelConfig;
    private readonly Uri _startUri;

    public MainPage(
        IOptions<WebPortalOptions> options,
        IWebPortalNavigationPolicy navigationPolicy,
        IPortalDownloadPolicy downloadPolicy,
        IPortalFileDownloader fileDownloader,
        IWhitelabelState whitelabelState)
    {
        InitializeComponent();

        _navigationPolicy = navigationPolicy;
        _downloadPolicy = downloadPolicy;
        _fileDownloader = fileDownloader;
        _whitelabelState = whitelabelState;
        _whitelabelConfig = _whitelabelState.Current;
        _startUri = CreateStartUri(_whitelabelConfig?.Url ?? options.Value.StartUrl);
        ApplyWhitelabel(_whitelabelConfig);
        UpdateBrowserControls();
        portalWebView.Source = _startUri.AbsoluteUri;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplySystemBars();
    }

    private async void OnPortalNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var destination))
        {
            e.Cancel = true;
            await ShowBlockedNavigationAsync();
            return;
        }

        if (_downloadPolicy.IsDownload(destination))
        {
            e.Cancel = true;
            await DownloadPortalFileAsync(destination);
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

    private async void OnPortalNavigated(object? sender, WebNavigatedEventArgs e)
    {
        loadingOverlay.IsVisible = false;
        UpdateBrowserControls();

        if (e.Result == WebNavigationResult.Success)
        {
            errorOverlay.IsVisible = false;
            await InstallPortalVisualFixesAsync();
#if ANDROID
            await InstallAndroidBlobCaptureAsync();
#endif
            return;
        }

        ShowError("Revisa tu conexión e inténtalo nuevamente.");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        NavigateBackInBrowser();
    }

    private void OnForwardClicked(object? sender, EventArgs e)
    {
        if (portalWebView.CanGoForward)
        {
            portalWebView.GoForward();
            UpdateBrowserControls();
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

    private async void OnChangeCompanyClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            "Cambiar empresa",
            "Se cerrará la configuración actual y volverás al selector de empresa.",
            "Cambiar",
            "Cancelar");

        if (!confirmed)
        {
            return;
        }

        _whitelabelState.ClearSelection();
        Microsoft.Maui.Controls.Application.Current?.Windows[0].Page =
            Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<WhitelabelWizardPage>();
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
        loadingMessageLabel.Text = "Conectando con el portal…";
        loadingOverlay.IsVisible = true;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!portalWebView.CanGoBack)
        {
            return base.OnBackButtonPressed();
        }

        MainThread.BeginInvokeOnMainThread(NavigateBackInBrowser);
        return true;
    }

    private void NavigateBackInBrowser()
    {
        if (!portalWebView.CanGoBack)
        {
            UpdateBrowserControls();
            return;
        }

        portalWebView.GoBack();
        UpdateBrowserControls();
    }

    private void UpdateBrowserControls()
    {
        SetBrowserButtonState(backButton, portalWebView.CanGoBack);
        SetBrowserButtonState(forwardButton, portalWebView.CanGoForward);
    }

    private void ApplyWhitelabel(WhitelabelConfig? config)
    {
        if (config is null)
        {
            return;
        }

        titleLabel.Text = config.NombreAplicacion;
        brandImage.Source = GetLogoAsset(config.LauncherIconKey);
        var primaryColor = NormalizeHexColor(config.PrimaryColor, "#0F172A");
        toolbarGrid.BackgroundColor = Color.FromArgb(primaryColor);
        environmentLabel.Text = $"{config.EmpresaId.ToUpperInvariant()} · Portal seguro";
        ApplySystemBars();
    }

    public void ApplySystemBars()
    {
#if ANDROID
        var primaryColor = NormalizeHexColor(_whitelabelConfig?.PrimaryColor ?? "#0F172A", "#0F172A");
        MainActivity.ApplySystemBarColors(primaryColor, primaryColor);
#endif
    }

    private static string NormalizeHexColor(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var candidate = value.Trim();
        return candidate.StartsWith('#') ? candidate : "#" + candidate;
    }

    private static string GetLogoAsset(string launcherIconKey) =>
        launcherIconKey.Trim().ToLowerInvariant() switch
        {
            "ali" => "ali_mark.svg",
            "cbe" => "cbe_mark.svg",
            "oak" => "oak_mark.svg",
            _ => "serioustech_mark.svg"
        };

    private static void SetBrowserButtonState(ImageButton button, bool isEnabled)
    {
        button.IsEnabled = isEnabled;
        button.Opacity = isEnabled ? 1.0 : 0.38;
    }

    private async Task DownloadPortalFileAsync(Uri destination)
    {
        errorOverlay.IsVisible = false;
        loadingMessageLabel.Text = "Preparando descarga de cotización…";
        loadingOverlay.IsVisible = true;

        try
        {
            await _fileDownloader.DownloadAndShareAsync(destination);
        }
        catch (Exception)
        {
            await DisplayAlertAsync(
                "Descarga no disponible",
                "No fue posible descargar la cotización. Inténtalo nuevamente desde Historial.",
                "Aceptar");
        }
        finally
        {
            loadingOverlay.IsVisible = false;
        }
    }

    private async Task InstallPortalVisualFixesAsync()
    {
        const string script = """
            (function () {
                if (window.__seriousMobileVisualFixesInstalled) {
                    if (window.__seriousMobileApplyIconContrastFix) {
                        window.clearTimeout(window.__seriousMobileIconFixTimer);
                        window.__seriousMobileIconFixTimer = window.setTimeout(window.__seriousMobileApplyIconContrastFix, 250);
                    }
                    return true;
                }

                window.__seriousMobileVisualFixesInstalled = true;

                function isDarkTheme() {
                    return document.documentElement.classList.contains('masa-theme-dark')
                        || document.body.classList.contains('masa-theme-dark')
                        || document.documentElement.style.colorScheme === 'dark'
                        || document.body.style.colorScheme === 'dark';
                }

                function parseRgb(value) {
                    var match = String(value || '').match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
                    if (!match) {
                        return null;
                    }

                    return {
                        r: Number(match[1]),
                        g: Number(match[2]),
                        b: Number(match[3])
                    };
                }

                function isLightColor(value) {
                    var rgb = parseRgb(value);
                    return rgb && rgb.r >= 235 && rgb.g >= 235 && rgb.b >= 235;
                }

                function findLightIconContainer(element) {
                    var current = element.parentElement;
                    var depth = 0;

                    while (current && depth < 5) {
                        var style = window.getComputedStyle(current);
                        var radius = parseFloat(style.borderTopLeftRadius || '0');
                        var width = current.offsetWidth || 0;
                        var height = current.offsetHeight || 0;

                        if (isLightColor(style.backgroundColor) && radius >= 8 && width <= 96 && height <= 96) {
                            return current;
                        }

                        current = current.parentElement;
                        depth++;
                    }

                    return null;
                }

                function patchFontIcon(icon) {
                    var container = findLightIconContainer(icon);
                    if (!container) {
                        return;
                    }

                    container.style.setProperty('background-color', '#ede9fe', 'important');
                    container.style.setProperty('box-shadow', 'none', 'important');
                    icon.style.setProperty('color', '#5b21b6', 'important');
                    icon.style.setProperty('-webkit-text-fill-color', '#5b21b6', 'important');
                }

                function patchSvgIcon(svg) {
                    var container = findLightIconContainer(svg);
                    if (!container) {
                        return;
                    }

                    container.style.setProperty('background-color', '#ede9fe', 'important');
                    container.style.setProperty('box-shadow', 'none', 'important');
                    svg.style.setProperty('color', '#5b21b6', 'important');

                    svg.querySelectorAll('path, circle, rect, line, polyline, polygon').forEach(function (part) {
                        var computed = window.getComputedStyle(part);
                        if (isLightColor(computed.fill) || part.getAttribute('fill') === 'currentColor') {
                            part.style.setProperty('fill', '#5b21b6', 'important');
                        }

                        if (isLightColor(computed.stroke) || part.getAttribute('stroke') === 'currentColor') {
                            part.style.setProperty('stroke', '#5b21b6', 'important');
                        }
                    });
                }

                window.__seriousMobileApplyIconContrastFix = function () {
                    if (!isDarkTheme()) {
                        return;
                    }

                    document.querySelectorAll('i[class*="mdi"], span[class*="mdi"], .v-icon').forEach(patchFontIcon);
                    document.querySelectorAll('svg').forEach(patchSvgIcon);
                };

                window.__seriousMobileApplyIconContrastFix();

                window.setTimeout(window.__seriousMobileApplyIconContrastFix, 250);
                window.setTimeout(window.__seriousMobileApplyIconContrastFix, 1000);

                return true;
            })();
            """;

        try
        {
            await portalWebView.EvaluateJavaScriptAsync(script);
        }
        catch
        {
            // The portal can reject script evaluation while Blazor is reconnecting or navigating.
        }
    }

    private async Task ApplyPortalWhitelabelLogoAsync()
    {
        var logoSource = GetConfiguredPortalLogoSource(_whitelabelConfig, _startUri);
        var fallbackLogoSource = BuildLogoDataUri(_whitelabelConfig);
        if (string.IsNullOrWhiteSpace(logoSource) && string.IsNullOrWhiteSpace(fallbackLogoSource))
        {
            return;
        }

        var script = $$"""
            (function () {
                var logo = '{{logoSource}}';
                var fallbackLogo = '{{fallbackLogoSource}}';
                var finalLogo = fallbackLogo || logo;

                function patchTenantLogo() {
                    document.querySelectorAll('img').forEach(function (image) {
                        var box = image.getBoundingClientRect();
                        var src = image.getAttribute('src') || '';
                        var alt = image.getAttribute('alt') || '';
                        var hasLogoSize = box.width >= 12 && box.width <= 180 && box.height >= 12 && box.height <= 180;
                        var looksLikeLogo = /logo|brand|white-label/i.test(src + ' ' + alt);
                        var failed = image.complete && image.naturalWidth === 0;
                        var empty = !image.getAttribute('src');

                        if (hasLogoSize && (looksLikeLogo || failed || empty)) {
                            if (image.dataset && image.dataset.seriousMobileLogoPatched === finalLogo) {
                                return;
                            }

                            image.onerror = function () {
                                if (fallbackLogo && image.src !== fallbackLogo) {
                                    image.src = fallbackLogo;
                                }
                            };

                            if (image.src !== finalLogo) {
                                image.src = finalLogo;
                            }

                            if (image.dataset) {
                                image.dataset.seriousMobileLogoPatched = finalLogo;
                            }

                            image.style.setProperty('object-fit', 'contain', 'important');
                            image.style.setProperty('object-position', 'center', 'important');
                            image.style.setProperty('width', '52px', 'important');
                            image.style.setProperty('height', '52px', 'important');
                            image.style.setProperty('max-width', '52px', 'important');
                            image.style.setProperty('max-height', '52px', 'important');
                            image.style.setProperty('padding', '2px', 'important');
                            image.style.setProperty('display', 'block', 'important');

                            var parent = image.parentElement;
                            if (parent) {
                                parent.style.setProperty('width', '64px', 'important');
                                parent.style.setProperty('height', '64px', 'important');
                                parent.style.setProperty('min-width', '64px', 'important');
                                parent.style.setProperty('min-height', '64px', 'important');
                                parent.style.setProperty('display', 'flex', 'important');
                                parent.style.setProperty('align-items', 'center', 'important');
                                parent.style.setProperty('justify-content', 'center', 'important');
                                parent.style.setProperty('overflow', 'hidden', 'important');
                                parent.style.setProperty('background-color', '#ffffff', 'important');
                                parent.style.setProperty('border-radius', '16px', 'important');
                            }
                        }
                    });
                }

                patchTenantLogo();
                window.setTimeout(patchTenantLogo, 250);
                window.setTimeout(patchTenantLogo, 1000);

                return true;
            })();
            """;

        try
        {
            await portalWebView.EvaluateJavaScriptAsync(script);
        }
        catch
        {
            // The portal may reject script evaluation while it is still rendering.
        }
    }

    private static string GetConfiguredPortalLogoSource(WhitelabelConfig? config, Uri startUri)
    {
        if (string.IsNullOrWhiteSpace(config?.LogoUrl))
        {
            return string.Empty;
        }

        var logoUrl = config.LogoUrl.Trim();
        if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsoluteUri;
        }

        return Uri.TryCreate(startUri, logoUrl, out var relativeUri)
            ? relativeUri.AbsoluteUri
            : string.Empty;
    }

#if ANDROID
    private async Task InstallAndroidBlobCaptureAsync()
    {
        const string script = """
            (function () {
                if (window.__mauiBlobCaptureInstalled) {
                    return true;
                }

                window.__mauiBlobCaptureInstalled = true;
                window.__mauiBlobDownloads = window.__mauiBlobDownloads || {};
                window.__mauiLastDownloadContext = window.__mauiLastDownloadContext || {};

                function sanitizePart(value) {
                    return String(value || '')
                        .normalize('NFD')
                        .replace(/[\u0300-\u036f]/g, '')
                        .replace(/[^a-zA-Z0-9_-]+/g, '-')
                        .replace(/^-+|-+$/g, '')
                        .slice(0, 40);
                }

                function findBankToken(text) {
                    var known = [
                        'BAN', 'GS', 'GNP', 'AXA', 'HDI', 'ANA', 'CHUBB', 'MAPFRE',
                        'ZURICH', 'QUALITAS', 'AFIRME', 'SURA', 'ATLAS', 'ELPOTOSI',
                        'POTOSI', 'INBURSA', 'BX', 'GENERAL', 'BBVA', 'BANORTE',
                        'SANTANDER', 'BANAMEX', 'HSBC', 'SCOTIABANK', 'ZRH', 'AFI'
                    ];

                    var upperText = String(text || '').toUpperCase();
                    for (var index = 0; index < known.length; index++) {
                        if (new RegExp('(^|\\s)' + known[index] + '(\\s|$)').test(upperText)) {
                            return known[index];
                        }
                    }

                    var tokens = upperText.match(/\b[A-Z]{2,8}\b/g) || [];
                    var ignored = {
                        PDF: true,
                        ZIP: true,
                        XLS: true,
                        XLSX: true,
                        DESC: true,
                        DESCARGA: true,
                        DESCARGAR: true,
                        EMITIR: true,
                        POLIZA: true,
                        POLIZA: true,
                        COTIZACION: true,
                        COTIZACIÓN: true,
                        SERIOUSTECH: true,
                        COTIZADA: true,
                        HISTORIAL: true,
                        ADMINISTRADOR: true,
                        GLOBAL: true
                    };

                    for (var tokenIndex = 0; tokenIndex < tokens.length; tokenIndex++) {
                        if (!ignored[tokens[tokenIndex]]) {
                            return tokens[tokenIndex];
                        }
                    }

                    return '';
                }

                function findContextFromElement(element) {
                    var current = element;
                    var depth = 0;
                    while (current && depth < 12) {
                        var text = current.innerText || current.textContent || '';
                        var folioMatch = String(text).match(/\b\d{6,}\b/);
                        var bank = findBankToken(text);

                        if (folioMatch || bank) {
                            return {
                                bank: sanitizePart(bank),
                                folio: sanitizePart(folioMatch ? folioMatch[0] : ''),
                                capturedAt: new Date().toISOString()
                            };
                        }

                        current = current.parentElement;
                        depth++;
                    }

                    return null;
                }

                function rememberDownloadContext(event) {
                    var context = findContextFromElement(event.target);
                    var previous = window.__mauiLastDownloadContext || {};
                    if (context && context.bank) {
                        window.__mauiLastDownloadContext = context;
                        return;
                    }

                    if (context && context.folio && !previous.bank) {
                        window.__mauiLastDownloadContext = context;
                    }
                }

                document.addEventListener('pointerdown', rememberDownloadContext, true);
                document.addEventListener('click', rememberDownloadContext, true);

                var originalCreateObjectURL = URL.createObjectURL.bind(URL);
                URL.createObjectURL = function (object) {
                    var objectUrl = originalCreateObjectURL(object);
                    if (object instanceof Blob) {
                        window.__mauiBlobDownloads[objectUrl] = {
                            blob: object,
                            context: window.__mauiLastDownloadContext || {}
                        };
                    }

                    return objectUrl;
                };

                var originalRevokeObjectURL = URL.revokeObjectURL.bind(URL);
                URL.revokeObjectURL = function (objectUrl) {
                    setTimeout(function () {
                        delete window.__mauiBlobDownloads[objectUrl];
                        originalRevokeObjectURL(objectUrl);
                    }, 30000);
                };

                return true;
            })();
            """;

        try
        {
            await portalWebView.EvaluateJavaScriptAsync(script);
        }
        catch
        {
            // The portal can block script evaluation during intermediate navigations.
        }
    }
#endif

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

    private static string BuildLogoDataUri(WhitelabelConfig? config)
    {
        if (config is null)
        {
            return string.Empty;
        }

        var svg = GetFallbackLogoSvg(config.LauncherIconKey);

        return "data:image/svg+xml;charset=utf-8," + Uri.EscapeDataString(svg);
    }

    private static string GetFallbackLogoSvg(string launcherIconKey)
    {
        return launcherIconKey.Trim().ToLowerInvariant() switch
        {
            "ali" => """
                <svg width="128" height="128" viewBox="0 0 128 128" xmlns="http://www.w3.org/2000/svg">
                  <rect width="128" height="128" rx="28" fill="#F59E0B"/>
                  <path d="M34 84L58 32H72L96 84H80L76 74H54L50 84H34ZM59 60H71L65 45L59 60Z" fill="#FFFFFF"/>
                  <text x="64" y="106" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="17" font-weight="800" fill="#FFFFFF">ALI</text>
                </svg>
                """,
            "cbe" => """
                <svg width="128" height="128" viewBox="0 0 128 128" xmlns="http://www.w3.org/2000/svg">
                  <rect width="128" height="128" rx="28" fill="#0F766E"/>
                  <circle cx="98" cy="30" r="14" fill="#F59E0B"/>
                  <text x="64" y="72" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="34" font-weight="800" fill="#FFFFFF">CBE</text>
                  <text x="93" y="49" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="20" font-weight="800" fill="#FFFFFF">+</text>
                </svg>
                """,
            "oak" => """
                <svg width="128" height="128" viewBox="0 0 128 128" xmlns="http://www.w3.org/2000/svg">
                  <rect width="128" height="128" rx="28" fill="#14532D"/>
                  <path d="M64 18C84 18 100 33 100 53C100 78 79 95 64 106C49 95 28 78 28 53C28 33 44 18 64 18Z" fill="#22C55E"/>
                  <path d="M64 34C76 34 86 44 86 56C86 72 73 84 64 91C55 84 42 72 42 56C42 44 52 34 64 34Z" fill="#DCFCE7"/>
                  <path d="M59 55H69V92H59V55Z" fill="#14532D"/>
                  <path d="M64 62L82 49L87 56L64 74V62Z" fill="#14532D"/>
                  <path d="M64 62L46 49L41 56L64 74V62Z" fill="#14532D"/>
                </svg>
                """,
            _ => """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" role="img" aria-label="SeriousTech">
                  <rect width="96" height="96" rx="22" fill="#0f172a"/>
                  <path d="M25 60c0-16 10-29 25-29 11 0 20 5 24 14" fill="none" stroke="#38bdf8" stroke-width="8" stroke-linecap="round"/>
                  <path d="M28 61c7 11 20 15 32 10 8-3 13-9 16-16" fill="none" stroke="#22c55e" stroke-width="8" stroke-linecap="round"/>
                  <path d="M34 43c5-9 19-12 28-4 5 4 7 10 6 16" fill="none" stroke="#f59e0b" stroke-width="7" stroke-linecap="round"/>
                  <circle cx="48" cy="52" r="9" fill="#ffffff"/>
                  <circle cx="48" cy="52" r="4" fill="#1d4ed8"/>
                </svg>
                """
        };
    }
}
