namespace App.Mobile.Services;

public sealed record TenantWhiteLabelProfile(
    TenantInfo Tenant,
    IReadOnlyList<TenantCompanyProfile> Companies,
    int TotalCompanies,
    DateTimeOffset? GeneratedAt)
{
    public static TenantWhiteLabelProfile CreateFallback(string tenantCode, string tenantId = "11111111-1111-4111-8111-111111111111") =>
        new(
            Tenant: new TenantInfo(
                Id: tenantId,
                Key: "serious-tech",
                Name: "Serious Tech",
                IsActive: true),
            Companies:
            [
                new TenantCompanyProfile(
                    Id: "92000000-0000-0000-0000-000000000001",
                    Key: "serious-seguros",
                    Name: "Serious Seguros",
                    BrokerName: "Serious Tech Insurance",
                    IsActive: true,
                    WhiteLabel: new CompanyWhiteLabelProfile(
                        Configured: true,
                        AppName: "Serious Seguros",
                        Tagline: "Tu seguro fácil y rápido",
                        LogoUrl: null,
                        LanguageCode: "es",
                        Theme: new CompanyThemeProfile(
                            PrimaryColor: "#1F87B3",
                            SecondaryColor: "#F58220",
                            LightPalette: null,
                            DarkPalette: null),
                        Support: null,
                        Domains:
                        [
                            new CompanyDomainProfile(
                                Id: "93000000-0000-0000-0000-000000000001",
                                DomainName: "portal-qa.seriouseguros.com.mx",
                                LogoFileName: "logo.svg",
                                IsPrimary: true,
                                IsActive: true)
                        ],
                        UpdatedAt: DateTimeOffset.Parse("2026-07-29T18:30:00Z"))),
                new TenantCompanyProfile(
                    Id: "92000000-0000-0000-0000-000000000002",
                    Key: "ali-asociados",
                    Name: "ALI Asociados",
                    BrokerName: "ALI Asociados Agente de Seguros",
                    IsActive: true,
                    WhiteLabel: new CompanyWhiteLabelProfile(
                        Configured: true,
                        AppName: "ALI Seguros",
                        Tagline: "Protección para cada etapa",
                        LogoUrl: null,
                        LanguageCode: "es",
                        Theme: new CompanyThemeProfile(
                            PrimaryColor: "#153E75",
                            SecondaryColor: "#38A169",
                            LightPalette: null,
                            DarkPalette: null),
                        Support: null,
                        Domains:
                        [
                            new CompanyDomainProfile(
                                Id: "93000000-0000-0000-0000-000000000003",
                                DomainName: "ali-qa.seriouseguros.com.mx",
                                LogoFileName: "logo.svg",
                                IsPrimary: true,
                                IsActive: true)
                        ],
                        UpdatedAt: DateTimeOffset.Parse("2026-07-28T22:10:00Z"))),
                new TenantCompanyProfile(
                    Id: "92000000-0000-0000-0000-000000000003",
                    Key: "cbe",
                    Name: "CBE",
                    BrokerName: "CBE+",
                    IsActive: true,
                    WhiteLabel: new CompanyWhiteLabelProfile(
                        Configured: true,
                        AppName: "CBE",
                        Tagline: "Multicotizador corporativo",
                        LogoUrl: null,
                        LanguageCode: "es",
                        Theme: new CompanyThemeProfile(
                            PrimaryColor: "#1E3A8A",
                            SecondaryColor: "#0F766E",
                            LightPalette: null,
                            DarkPalette: null),
                        Support: null,
                        Domains:
                        [
                            new CompanyDomainProfile(
                                Id: "93000000-0000-0000-0000-000000000004",
                                DomainName: "cbe-qa.seriouseguros.com.mx",
                                LogoFileName: "logo.svg",
                                IsPrimary: true,
                                IsActive: true)
                        ],
                        UpdatedAt: DateTimeOffset.Parse("2026-07-29T19:00:00Z")))
            ],
            TotalCompanies: 3,
            GeneratedAt: DateTimeOffset.UtcNow);
}

public sealed record TenantInfo(
    string Id,
    string Key,
    string Name,
    bool IsActive);

public sealed record TenantCompanyProfile(
    string Id,
    string Key,
    string Name,
    string? BrokerName,
    bool IsActive,
    CompanyWhiteLabelProfile WhiteLabel);

public sealed record CompanyWhiteLabelProfile(
    bool Configured,
    string? AppName,
    string? Tagline,
    string? LogoUrl,
    string? LanguageCode,
    CompanyThemeProfile? Theme,
    CompanySupportProfile? Support,
    IReadOnlyList<CompanyDomainProfile> Domains,
    DateTimeOffset? UpdatedAt);

public sealed record CompanyThemeProfile(
    string? PrimaryColor,
    string? SecondaryColor,
    PaletteProfile? LightPalette,
    PaletteProfile? DarkPalette);

public sealed record PaletteProfile(
    string? CanvasColor,
    string? SurfaceColor,
    string? HeadingColor,
    string? BodyColor);

public sealed record CompanySupportProfile(
    string? Label,
    string? Url);

public sealed record CompanyDomainProfile(
    string Id,
    string DomainName,
    string LogoFileName,
    bool IsPrimary,
    bool IsActive);
