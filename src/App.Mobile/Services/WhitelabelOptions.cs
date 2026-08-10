namespace App.Mobile.Services;

public sealed class WhitelabelOptions
{
    public const string SectionName = "Whitelabel";

    public string ApiBaseUrl { get; set; } = "https://movil-qa.seriouseguros.com.mx";

    public string DefaultTenantId { get; set; } = "serioustech";

    public string DefaultEmpresaId { get; set; } = "serious";
}
