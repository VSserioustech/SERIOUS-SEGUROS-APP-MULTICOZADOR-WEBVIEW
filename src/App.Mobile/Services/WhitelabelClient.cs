using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace App.Mobile.Services;

public sealed class WhitelabelClient : IWhitelabelClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly WhitelabelConfig[] FallbackCompanies =
    [
        new(
            TenantId: "serioustech",
            EmpresaId: "serious",
            Url: "https://portal-qa.seriouseguros.com.mx/",
            Version: "1.0.0",
            NombreAplicacion: "Serious Seguros Portal",
            LauncherIconKey: "serious",
            LogoUrl: "https://portal-qa.seriouseguros.com.mx/brand/serioustech-mark.svg",
            PrimaryColor: "#0F172A",
            SecondaryColor: "#175CD3",
            IsDefault: true),
        new(
            TenantId: "serioustech",
            EmpresaId: "ali",
            Url: "https://ali-qa.seriouseguros.com.mx/",
            Version: "1.0.0",
            NombreAplicacion: "Ali Asociados",
            LauncherIconKey: "ali",
            LogoUrl: "https://ali-qa.seriouseguros.com.mx/brand/serioustech-mark.svg",
            PrimaryColor: "#3B0764",
            SecondaryColor: "#F59E0B",
            IsDefault: false),
        new(
            TenantId: "serioustech",
            EmpresaId: "cbe",
            Url: "https://cbe-qa.seriouseguros.com.mx/",
            Version: "1.0.0",
            NombreAplicacion: "CBE",
            LauncherIconKey: "cbe",
            LogoUrl: "https://cbe-qa.seriouseguros.com.mx/brand/serioustech-mark.svg",
            PrimaryColor: "#1E3A8A",
            SecondaryColor: "#22C55E",
            IsDefault: false)
    ];

    private readonly HttpClient _httpClient;
    private readonly WhitelabelOptions _options;

    public WhitelabelClient(HttpClient httpClient, IOptions<WhitelabelOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<TenantLoginSession> LoginByCodeAsync(
        string tenantCode,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var request = new LoginByCodeRequest(tenantCode.Trim(), email.Trim(), password);
        var response = await _httpClient.PostAsJsonAsync(
            "api/core/auth/login/by-code",
            request,
            JsonOptions,
            cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<LoginByCodeResponse>(JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode || payload?.Success != true || payload.Data is null)
        {
            throw new InvalidOperationException(payload?.Message ?? "No fue posible iniciar sesión con el tenant.");
        }

        return new TenantLoginSession(
            TenantCode: tenantCode.Trim(),
            AccessToken: payload.Data.AccessToken,
            TokenType: string.IsNullOrWhiteSpace(payload.Data.TokenType) ? "Bearer" : payload.Data.TokenType,
            ExpiresAt: payload.Data.ExpiresAt,
            TenantId: payload.Data.TenantId,
            UserId: payload.Data.UserId,
            Email: payload.Data.Email,
            DisplayName: payload.Data.DisplayName,
            Roles: payload.Data.Roles ?? [],
            Permissions: payload.Data.Permissions ?? []);
    }

    public async Task<TenantWhiteLabelProfile> GetWhiteLabelProfileAsync(
        TenantLoginSession session,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/tenant/white-label-profile");
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                string.IsNullOrWhiteSpace(session.TokenType) ? "Bearer" : session.TokenType,
                session.AccessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var profile = await response.Content.ReadFromJsonAsync<TenantWhiteLabelProfile>(JsonOptions, cancellationToken);
                if (profile is not null)
                {
                    return profile;
                }
            }
        }
        catch
        {
            // Fallback below keeps QA mobile flow usable while backend token validation is aligned.
        }

        return TenantWhiteLabelProfile.CreateFallback(session.TenantCode, session.TenantId);
    }

    public async Task<IReadOnlyList<WhitelabelConfig>> GetAvailableCompaniesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var catalog = await _httpClient.GetFromJsonAsync<WhitelabelCatalogResponse>(
                "api/mobile/whitelabel",
                cancellationToken);

            var companies = catalog?.Tenants
                .SelectMany(tenant => tenant.Empresas.Select(company => new WhitelabelConfig(
                    TenantId: tenant.TenantId,
                    EmpresaId: company.EmpresaId,
                    Url: string.Empty,
                    Version: catalog.Version,
                    NombreAplicacion: company.NombreAplicacion,
                    LauncherIconKey: company.LauncherIconKey,
                    LogoUrl: string.Empty,
                    PrimaryColor: "#0F172A",
                    SecondaryColor: "#175CD3",
                    IsDefault: company.IsDefault)))
                .ToArray();

            return companies is { Length: > 0 } ? companies : FallbackCompanies;
        }
        catch
        {
            return FallbackCompanies;
        }
    }

    public async Task<WhitelabelConfig> GetCompanyAsync(string empresaId, CancellationToken cancellationToken = default)
    {
        var normalizedEmpresaId = string.IsNullOrWhiteSpace(empresaId)
            ? _options.DefaultEmpresaId
            : empresaId.Trim();

        try
        {
            var config = await _httpClient.GetFromJsonAsync<WhitelabelConfig>(
                $"api/mobile/whitelabel/{Uri.EscapeDataString(normalizedEmpresaId)}",
                cancellationToken);

            if (config is not null && Uri.TryCreate(config.Url, UriKind.Absolute, out _))
            {
                return config;
            }
        }
        catch
        {
            // Fallback below keeps the wizard usable when the demo API is not running.
        }

        return FallbackCompanies.FirstOrDefault(company =>
            string.Equals(company.EmpresaId, normalizedEmpresaId, StringComparison.OrdinalIgnoreCase)) ??
            FallbackCompanies.First(company => company.IsDefault);
    }

    public WhitelabelConfig CreateConfig(TenantWhiteLabelProfile profile, TenantCompanyProfile company)
    {
        var whiteLabel = company.WhiteLabel;
        var domain = whiteLabel.Domains
            .Where(item => item.IsActive && !string.IsNullOrWhiteSpace(item.DomainName))
            .OrderByDescending(item => item.IsPrimary)
            .FirstOrDefault();

        var domainName = domain?.DomainName.Trim();
        var url = !string.IsNullOrWhiteSpace(domainName)
            ? $"https://{domainName}/"
            : "https://portal-qa.seriouseguros.com.mx/";

        var logoUrl = BuildLogoUrl(whiteLabel.LogoUrl, domain);
        var configured = whiteLabel.Configured;
        var appName = configured
            ? FirstNonEmpty(whiteLabel.AppName, company.Name, profile.Tenant.Name)
            : FirstNonEmpty(company.Name, profile.Tenant.Name, "Serious Seguros Portal");

        return new WhitelabelConfig(
            TenantId: profile.Tenant.Id,
            EmpresaId: company.Key,
            Url: url,
            Version: whiteLabel.UpdatedAt?.ToString("O") ?? "tenant-profile",
            NombreAplicacion: appName,
            LauncherIconKey: GuessIconKey(company),
            LogoUrl: configured ? logoUrl : string.Empty,
            PrimaryColor: configured ? FirstNonEmpty(whiteLabel.Theme?.PrimaryColor, "#0F172A") : "#0F172A",
            SecondaryColor: configured ? FirstNonEmpty(whiteLabel.Theme?.SecondaryColor, "#175CD3") : "#175CD3",
            IsDefault: domain?.IsPrimary == true);
    }

    private static string BuildLogoUrl(string? configuredLogoUrl, CompanyDomainProfile? domain)
    {
        var domainName = domain?.DomainName.Trim();
        if (!string.IsNullOrWhiteSpace(configuredLogoUrl))
        {
            var logo = configuredLogoUrl.Trim();
            if (Uri.TryCreate(logo, UriKind.Absolute, out var absoluteLogo))
            {
                return absoluteLogo.AbsoluteUri;
            }

            if (logo.StartsWith('/') && !string.IsNullOrWhiteSpace(domainName))
            {
                return $"https://{domainName}{logo}";
            }
        }

        if (domain is null ||
            string.IsNullOrWhiteSpace(domainName) ||
            string.IsNullOrWhiteSpace(domain.LogoFileName))
        {
            return string.Empty;
        }

        var logoFileName = domain.LogoFileName.Trim().TrimStart('/');
        return $"https://{domainName}/brand/white-label/{domainName}/{logoFileName}";
    }

    private static string GuessIconKey(TenantCompanyProfile company)
    {
        var key = company.Key.Trim().ToLowerInvariant();
        if (key.Contains("ali", StringComparison.OrdinalIgnoreCase))
        {
            return "ali";
        }

        if (key.Contains("cbe", StringComparison.OrdinalIgnoreCase))
        {
            return "cbe";
        }

        return "serious";
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record LoginByCodeRequest(
        string TenantCode,
        string Email,
        string Password);

    private sealed record LoginByCodeResponse(
        bool Success,
        string? Message,
        LoginByCodeData? Data,
        IReadOnlyList<string>? Errors);

    private sealed record LoginByCodeData(
        string AccessToken,
        string TokenType,
        DateTimeOffset? ExpiresAt,
        string TenantId,
        string UserId,
        string Email,
        string DisplayName,
        IReadOnlyList<string>? Roles,
        IReadOnlyList<string>? Permissions);

    private sealed record WhitelabelCatalogResponse(
        string Version,
        string DefaultTenantId,
        string DefaultEmpresaId,
        IReadOnlyCollection<TenantSummary> Tenants);

    private sealed record TenantSummary(
        string TenantId,
        IReadOnlyCollection<CompanySummary> Empresas);

    private sealed record CompanySummary(
        string EmpresaId,
        string NombreAplicacion,
        string LauncherIconKey,
        bool IsDefault);
}
