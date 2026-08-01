using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

var catalog = WhitelabelCatalog.CreateDemo();

app.MapGet("/", () => Results.Redirect("/api/mobile/whitelabel"));

app.MapGet("/api/mobile/whitelabel", () =>
{
    var tenants = catalog
        .GroupBy(item => item.TenantId, StringComparer.OrdinalIgnoreCase)
        .Select(group => new TenantSummary(
            group.Key,
            group
                .OrderBy(item => item.NombreAplicacion)
                .Select(item => new CompanySummary(
                    item.EmpresaId,
                    item.NombreAplicacion,
                    item.LauncherIconKey,
                    item.IsDefault))
                .ToArray()))
        .OrderBy(item => item.TenantId)
        .ToArray();

    return Results.Ok(new WhitelabelCatalogResponse(
        Version: "demo-2026.07.23",
        DefaultTenantId: "serioustech",
        DefaultEmpresaId: "serious",
        Tenants: tenants));
});

app.MapGet("/api/mobile/whitelabel/{empresaId}", (string empresaId) =>
{
    var match = catalog.FirstOrDefault(item =>
        string.Equals(item.EmpresaId, empresaId, StringComparison.OrdinalIgnoreCase));

    return match is null
        ? Results.NotFound(CreateNotFound(empresaId))
        : Results.Ok(match);
});

app.MapGet("/api/mobile/whitelabel/resolve", (string? tenantId, string? empresaId) =>
{
    var normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? "serioustech" : tenantId.Trim();
    var normalizedEmpresaId = string.IsNullOrWhiteSpace(empresaId) ? "serious" : empresaId.Trim();

    var match = catalog.FirstOrDefault(item =>
        string.Equals(item.TenantId, normalizedTenantId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(item.EmpresaId, normalizedEmpresaId, StringComparison.OrdinalIgnoreCase));

    if (match is not null)
    {
        return Results.Ok(match);
    }

    var fallback = catalog.First(item => item.IsDefault);
    return Results.Ok(fallback with
    {
        Metadata = fallback.Metadata with
        {
            RequestedTenantId = normalizedTenantId,
            RequestedEmpresaId = normalizedEmpresaId,
            FallbackReason = "No existe configuración para el tenant/empresa solicitado; se aplica identidad default."
        }
    });
});

app.Run();

static WhitelabelNotFoundResponse CreateNotFound(string empresaId) =>
    new(
        Error: "WHITELABEL_NOT_FOUND",
        Message: $"No existe configuración demo para empresaId '{empresaId}'.",
        FallbackEmpresaId: "serious");

public sealed record WhitelabelConfig(
    string TenantId,
    string EmpresaId,
    string Url,
    string Version,
    string NombreAplicacion,
    string LauncherIconKey,
    string LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    bool IsDefault,
    WhitelabelMetadata Metadata);

public sealed record WhitelabelMetadata(
    string? RequestedTenantId = null,
    string? RequestedEmpresaId = null,
    string? FallbackReason = null);

public sealed record WhitelabelCatalogResponse(
    string Version,
    string DefaultTenantId,
    string DefaultEmpresaId,
    IReadOnlyCollection<TenantSummary> Tenants);

public sealed record TenantSummary(
    string TenantId,
    IReadOnlyCollection<CompanySummary> Empresas);

public sealed record CompanySummary(
    string EmpresaId,
    string NombreAplicacion,
    string LauncherIconKey,
    bool IsDefault);

public sealed record WhitelabelNotFoundResponse(
    string Error,
    string Message,
    string FallbackEmpresaId);

public static class WhitelabelCatalog
{
    public static IReadOnlyCollection<WhitelabelConfig> CreateDemo() =>
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
            IsDefault: true,
            Metadata: new()),
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
            IsDefault: false,
            Metadata: new()),
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
            IsDefault: false,
            Metadata: new())
    ];
}
