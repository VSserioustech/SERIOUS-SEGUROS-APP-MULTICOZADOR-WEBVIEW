using App.Application.Configuration;
using App.Application.Interfaces;
using App.Application.Models;
using App.Mobile.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace App.Mobile;

public partial class MainPage : ContentPage, ISystemBarsPage, INativeResumeAwarePage
{
    private static readonly TimeSpan MinimumResumeNotificationInterval = TimeSpan.FromMilliseconds(750);
    private readonly IWebPortalNavigationPolicy _navigationPolicy;
    private readonly IPortalDownloadPolicy _downloadPolicy;
    private readonly IPortalCredentialStore _credentialStore;
    private readonly IPortalFileDownloader _fileDownloader;
    private readonly IWhitelabelState _whitelabelState;
    private readonly WhitelabelConfig? _whitelabelConfig;
    private readonly Uri _startUri;
    private DateTimeOffset _lastResumeNotificationUtc = DateTimeOffset.MinValue;

    public MainPage(
        IOptions<WebPortalOptions> options,
        IWebPortalNavigationPolicy navigationPolicy,
        IPortalDownloadPolicy downloadPolicy,
        IPortalCredentialStore credentialStore,
        IPortalFileDownloader fileDownloader,
        IWhitelabelState whitelabelState)
    {
        InitializeComponent();

        _navigationPolicy = navigationPolicy;
        _downloadPolicy = downloadPolicy;
        _credentialStore = credentialStore;
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
            await InstallPortalAutoLoginAsync();
            await InstallPortalBrandingPatchAsync();
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
        brandImage.Source = WhitelabelLogoSource.FromFallbackAsset(config.LauncherIconKey);
        _ = ApplyBrandLogoAsync(config);
        var primaryColor = NormalizeHexColor(config.PrimaryColor, "#0F172A");
        toolbarGrid.BackgroundColor = Color.FromArgb(primaryColor);
        environmentLabel.Text = $"{config.EmpresaId.ToUpperInvariant()} · Portal seguro";
        ApplySystemBars();
    }

    private async Task ApplyBrandLogoAsync(WhitelabelConfig config)
    {
        try
        {
            var logoSource = await WhitelabelLogoSource.CreateAsync(config.LogoUrl, config.LauncherIconKey);
            if (_whitelabelConfig?.EmpresaId == config.EmpresaId)
            {
                brandImage.Source = logoSource;
            }
        }
        catch (Exception)
        {
            if (_whitelabelConfig?.EmpresaId == config.EmpresaId)
            {
                brandImage.Source = WhitelabelLogoSource.FromFallbackAsset(config.LauncherIconKey);
            }
        }
    }

    public void ApplySystemBars()
    {
#if ANDROID
        var primaryColor = NormalizeHexColor(_whitelabelConfig?.PrimaryColor ?? "#0F172A", "#0F172A");
        MainActivity.ApplySystemBarColors(primaryColor, primaryColor);
#endif
    }

    public void OnNativeResume()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastResumeNotificationUtc < MinimumResumeNotificationInterval)
        {
            return;
        }

        _lastResumeNotificationUtc = now;
        _ = NotifyPortalResumeAsync();
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

    private static void SetBrowserButtonState(ImageButton button, bool isEnabled)
    {
        button.IsEnabled = isEnabled;
        button.Opacity = isEnabled ? 1.0 : 0.38;
    }

    private static string GetPortalDisplayName(WhitelabelConfig config) =>
        string.IsNullOrWhiteSpace(config.NombreAplicacion)
            ? config.EmpresaId
            : config.NombreAplicacion.Trim();

    private static string GetPortalSubtitle(WhitelabelConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.EmpresaId))
        {
            return GetPortalDisplayName(config);
        }

        return config.EmpresaId
            .Trim()
            .Replace('-', ' ')
            .ToUpperInvariant();
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

    private async Task NotifyPortalResumeAsync()
    {
        const string script = """
            (function () {
                var resumeDetail = {
                    source: 'serious-mobile-android',
                    resumedAt: new Date().toISOString(),
                    href: window.location.href
                };

                window.__seriousMobileLastResume = resumeDetail;

                function dispatch(target, eventName) {
                    try {
                        target.dispatchEvent(new Event(eventName));
                    } catch (error) {
                        return false;
                    }

                    return true;
                }

                try {
                    document.dispatchEvent(new CustomEvent('serious-mobile-resume', { detail: resumeDetail }));
                } catch (error) {
                    dispatch(document, 'serious-mobile-resume');
                }

                dispatch(document, 'visibilitychange');
                dispatch(window, 'focus');
                dispatch(window, 'online');
                dispatch(window, 'resize');

                if (window.visualViewport) {
                    dispatch(window.visualViewport, 'resize');
                }

                window.requestAnimationFrame(function () {
                    dispatch(window, 'resize');

                    if (window.visualViewport) {
                        dispatch(window.visualViewport, 'resize');
                    }
                });

                return true;
            })();
            """;

        try
        {
            await portalWebView.EvaluateJavaScriptAsync(script);
        }
        catch
        {
            // The WebView can reject JavaScript while Android is resuming or the portal is reconnecting.
        }
    }

    private async Task InstallPortalAutoLoginAsync()
    {
        var credentials = await _credentialStore.GetAsync(_whitelabelState.TenantSession);
        if (credentials is null)
        {
            return;
        }

        var email = JsonSerializer.Serialize(credentials.Email);
        var password = JsonSerializer.Serialize(credentials.Password);
        var script = $$"""
            (function () {
                if (window.__seriousMobilePortalLoginSubmitted) {
                    return true;
                }

                var email = {{email}};
                var password = {{password}};
                var maxLoginAttemptsMs = 15000;
                var startedAt = Date.now();

                function isVisible(element) {
                    if (!element) {
                        return false;
                    }

                    var box = element.getBoundingClientRect();
                    var style = window.getComputedStyle(element);
                    return box.width > 0
                        && box.height > 0
                        && style.visibility !== 'hidden'
                        && style.display !== 'none';
                }

                function valueContains(input, values) {
                    var source = [
                        input.type,
                        input.name,
                        input.id,
                        input.autocomplete,
                        input.placeholder,
                        input.getAttribute('aria-label')
                    ].join(' ').toLowerCase();

                    return values.some(function (value) {
                        return source.indexOf(value) >= 0;
                    });
                }

                function findEmailInput() {
                    var inputs = Array.prototype.filter.call(
                        document.querySelectorAll('input'),
                        function (input) {
                            return isVisible(input)
                                && input.type !== 'password'
                                && input.type !== 'hidden'
                                && input.type !== 'checkbox'
                                && input.type !== 'radio';
                        });

                    return inputs.find(function (input) {
                        return valueContains(input, ['email', 'correo', 'user', 'usuario', 'login', 'username']);
                    }) || inputs.find(function (input) {
                        return input.type === 'email' || input.type === 'text';
                    }) || null;
                }

                function findSubmitButton(passwordInput) {
                    var form = passwordInput && passwordInput.closest('form');
                    var candidates = form
                        ? form.querySelectorAll('button, input[type="submit"], [role="button"]')
                        : document.querySelectorAll('button, input[type="submit"], [role="button"]');

                    return Array.prototype.find.call(candidates, function (button) {
                        var text = (button.innerText || button.value || button.textContent || '').trim().toLowerCase();
                        return isVisible(button)
                            && !button.disabled
                            && (text.indexOf('entrar') >= 0
                                || text.indexOf('ingresar') >= 0
                                || text.indexOf('iniciar') >= 0
                                || text.indexOf('acceder') >= 0
                                || text.indexOf('login') >= 0
                                || text.indexOf('sign in') >= 0
                                || button.type === 'submit');
                    });
                }

                function setInputValue(input, value) {
                    var setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                    setter.call(input, value);
                    input.dispatchEvent(new Event('input', { bubbles: true }));
                    input.dispatchEvent(new Event('change', { bubbles: true }));
                    input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true }));
                    input.dispatchEvent(new Event('blur', { bubbles: true }));
                }

                function trySubmitLogin() {
                    if (Date.now() - startedAt > maxLoginAttemptsMs) {
                        stopAutoLoginWatch();
                        return false;
                    }

                    var passwordInput = Array.prototype.find.call(
                        document.querySelectorAll('input[type="password"]'),
                        isVisible);
                    var emailInput = findEmailInput();

                    if (!emailInput || !passwordInput) {
                        return false;
                    }

                    setInputValue(emailInput, email);
                    setInputValue(passwordInput, password);

                    window.__seriousMobilePortalLoginSubmitted = true;

                    var submitButton = findSubmitButton(passwordInput);
                    if (submitButton) {
                        stopAutoLoginWatch();
                        submitButton.click();
                        return true;
                    }

                    var form = passwordInput.closest('form') || emailInput.closest('form');
                    if (form && typeof form.requestSubmit === 'function') {
                        stopAutoLoginWatch();
                        form.requestSubmit();
                        return true;
                    }

                    if (form) {
                        stopAutoLoginWatch();
                        form.submit();
                        return true;
                    }

                    window.__seriousMobilePortalLoginSubmitted = false;
                    return false;
                }

                function stopAutoLoginWatch() {
                    window.clearInterval(window.__seriousMobilePortalLoginInterval);
                    window.clearTimeout(window.__seriousMobilePortalLoginTimer);

                    if (window.__seriousMobilePortalLoginObserver) {
                        window.__seriousMobilePortalLoginObserver.disconnect();
                        window.__seriousMobilePortalLoginObserver = null;
                    }
                }

                if (trySubmitLogin()) {
                    return true;
                }

                window.clearInterval(window.__seriousMobilePortalLoginInterval);
                window.clearTimeout(window.__seriousMobilePortalLoginTimer);

                window.__seriousMobilePortalLoginInterval = window.setInterval(trySubmitLogin, 500);
                window.__seriousMobilePortalLoginTimer = window.setTimeout(stopAutoLoginWatch, maxLoginAttemptsMs + 1000);

                if (window.MutationObserver) {
                    if (window.__seriousMobilePortalLoginObserver) {
                        window.__seriousMobilePortalLoginObserver.disconnect();
                    }

                    window.__seriousMobilePortalLoginObserver = new MutationObserver(function () {
                        window.clearTimeout(window.__seriousMobilePortalLoginMutationTimer);
                        window.__seriousMobilePortalLoginMutationTimer = window.setTimeout(trySubmitLogin, 150);
                    });

                    window.__seriousMobilePortalLoginObserver.observe(document.body || document.documentElement, {
                        childList: true,
                        subtree: true,
                        attributes: true,
                        attributeFilter: ['class', 'style', 'disabled']
                    });
                }

                return true;
            })();
            """;

        try
        {
            await portalWebView.EvaluateJavaScriptAsync(script);
        }
        catch
        {
            // The portal can reject script evaluation while the login view is still rendering.
        }
    }

    private async Task InstallPortalBrandingPatchAsync()
    {
        if (_whitelabelConfig is null)
        {
            return;
        }

        var displayName = JsonSerializer.Serialize(GetPortalDisplayName(_whitelabelConfig));
        var subtitle = JsonSerializer.Serialize(GetPortalSubtitle(_whitelabelConfig));
        var logoUrl = JsonSerializer.Serialize(_whitelabelConfig.LogoUrl);
        var fallbackLogo = JsonSerializer.Serialize(BuildPortalBrandLogoDataUri(_whitelabelConfig.LauncherIconKey));

        var script = $$"""
            (function () {
                window.__seriousMobileBranding = {
                    displayName: {{displayName}},
                    subtitle: {{subtitle}},
                    logoUrl: {{logoUrl}},
                    fallbackLogo: {{fallbackLogo}}
                };

                function isTargetText(value) {
                    var text = String(value || '').trim().toLowerCase();
                    return text === 'serious tech'
                        || text === 'serious seguros'
                        || text === 'serious';
                }

                function isSideMenuArea(element) {
                    var box = element.getBoundingClientRect();
                    if (!box || box.width <= 0 || box.height <= 0) {
                        return false;
                    }

                    var inLeftBrandArea = box.left >= -10 && box.left <= 430 && box.top >= 0 && box.top <= 280;
                    if (!inLeftBrandArea) {
                        return false;
                    }

                    return !!element.closest('aside, nav, .v-navigation-drawer, [class*="drawer"], [class*="sidebar"], [class*="menu"]')
                        || box.left < 260;
                }

                function patchText(element, value) {
                    if (!element || !value || element.textContent === value) {
                        return false;
                    }

                    element.textContent = value;
                    element.setAttribute('data-serious-mobile-branding', 'true');
                    return true;
                }

                function findBrandContainer(element) {
                    var current = element;
                    var depth = 0;
                    while (current && depth < 5) {
                        var image = current.querySelector && current.querySelector('img');
                        if (image) {
                            return current;
                        }

                        current = current.parentElement;
                        depth++;
                    }

                    return element.parentElement;
                }

                function patchLogo(container) {
                    var branding = window.__seriousMobileBranding || {};
                    var logoSource = branding.fallbackLogo || branding.logoUrl;
                    if (!container || !logoSource) {
                        return;
                    }

                    var image = container.querySelector && container.querySelector('img');
                    if (!image || image.getAttribute('data-serious-mobile-logo') === logoSource) {
                        return false;
                    }

                    image.setAttribute('data-serious-mobile-logo', logoSource);
                    image.src = logoSource;
                    return true;
                }

                function getBrandingCandidates() {
                    var scoped = Array.prototype.slice.call(document.querySelectorAll('aside *, nav *, .v-navigation-drawer *, [class*="drawer"] *, [class*="sidebar"] *, [class*="menu"] *'));
                    var visibleTopLeft = Array.prototype.slice.call(document.querySelectorAll('body *')).filter(function (element) {
                        if (element.children && element.children.length > 0) {
                            return false;
                        }

                        return isSideMenuArea(element);
                    });

                    return scoped.concat(visibleTopLeft).filter(function (element, index, source) {
                        return source.indexOf(element) === index;
                    });
                }

                function applyBranding() {
                    var branding = window.__seriousMobileBranding || {};
                    if (!branding.displayName) {
                        return false;
                    }

                    var changed = false;
                    var patchedContainer = null;
                    var elements = getBrandingCandidates();

                    elements.forEach(function (element) {
                        var text = String(element.textContent || '').trim();
                        var hasElementChildren = element.children && element.children.length > 0;
                        if (hasElementChildren || !isTargetText(text) || !isSideMenuArea(element)) {
                            return;
                        }

                        changed = patchText(element, branding.displayName) || changed;
                        patchedContainer = patchedContainer || findBrandContainer(element);

                        var sibling = element.nextElementSibling;
                        if (sibling && !sibling.children.length && isSideMenuArea(sibling)) {
                            changed = patchText(sibling, branding.subtitle || branding.displayName) || changed;
                        }
                    });

                    changed = patchLogo(patchedContainer) || changed;
                    return changed;
                }

                function startBrandingWatch() {
                    var startedAt = Date.now();
                    window.clearInterval(window.__seriousMobileBrandingInterval);

                    applyBranding();
                    window.__seriousMobileBrandingInterval = window.setInterval(function () {
                        applyBranding();

                        if (Date.now() - startedAt > 12000) {
                            window.clearInterval(window.__seriousMobileBrandingInterval);
                            window.__seriousMobileBrandingInterval = null;
                        }
                    }, 500);
                }

                startBrandingWatch();

                if (!window.__seriousMobileBrandingObserver && window.MutationObserver) {
                    window.__seriousMobileBrandingObserver = new MutationObserver(function () {
                        window.clearTimeout(window.__seriousMobileBrandingTimer);
                        window.__seriousMobileBrandingTimer = window.setTimeout(startBrandingWatch, 120);
                    });

                    window.__seriousMobileBrandingObserver.observe(document.body || document.documentElement, {
                        childList: true,
                        subtree: true,
                        characterData: true,
                        attributes: true,
                        attributeFilter: ['class', 'style', 'src']
                    });
                }

                return true;
            })();
            """;

        try
        {
            await portalWebView.EvaluateJavaScriptAsync(script);
        }
        catch
        {
            // The portal may reject script evaluation while Blazor is reconnecting or navigating.
        }
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

    private static string BuildPortalBrandLogoDataUri(string launcherIconKey)
    {
        var svg = launcherIconKey.Trim().ToLowerInvariant() switch
        {
            "ali" => """
                <svg width="96" height="96" viewBox="0 0 96 96" xmlns="http://www.w3.org/2000/svg">
                  <rect width="96" height="96" rx="20" fill="#ffffff"/>
                  <path d="M22 57C23 39 31 26 44 17C39 33 38 48 42 64C35 64 28 61 22 57Z" fill="#2B8AAF"/>
                  <path d="M39 64C36 45 39 29 49 14C54 31 54 49 47 67C44 66 41 65 39 64Z" fill="#F6D11A"/>
                  <path d="M47 67C55 47 56 30 51 13C64 25 68 43 59 64C56 67 52 68 47 67Z" fill="#F58220"/>
                  <text x="59" y="62" font-family="Arial, Helvetica, sans-serif" font-size="31" font-weight="700" fill="#222222">ali</text>
                </svg>
                """,
            "cbe" => """
                <svg width="96" height="96" viewBox="0 0 96 96" xmlns="http://www.w3.org/2000/svg">
                  <rect width="96" height="96" rx="20" fill="#0F766E"/>
                  <circle cx="73" cy="24" r="10" fill="#F59E0B"/>
                  <text x="48" y="56" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="26" font-weight="800" fill="#FFFFFF">CBE</text>
                  <text x="70" y="38" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="15" font-weight="800" fill="#FFFFFF">+</text>
                </svg>
                """,
            "oak" => """
                <svg width="96" height="96" viewBox="0 0 96 96" xmlns="http://www.w3.org/2000/svg">
                  <rect width="96" height="96" rx="20" fill="#ffffff"/>
                  <text x="48" y="53" text-anchor="middle" font-family="Georgia, serif" font-size="34" font-weight="800" fill="#282D64">OAK</text>
                  <text x="48" y="70" text-anchor="middle" font-family="Georgia, serif" font-size="10" font-weight="800" fill="#282D64">RISK</text>
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

        return "data:image/svg+xml;charset=utf-8," + Uri.EscapeDataString(svg);
    }
}
