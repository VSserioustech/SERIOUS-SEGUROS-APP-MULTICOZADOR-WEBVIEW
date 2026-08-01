namespace App.Mobile.Services;

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
    bool IsDefault);
