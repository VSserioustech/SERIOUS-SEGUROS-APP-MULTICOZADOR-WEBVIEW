namespace App.Mobile.Services;

public sealed class WhitelabelOptions
{
    public const string SectionName = "Whitelabel";

    public string ApiBaseUrl { get; set; } = "http://10.0.2.2:5144";

    public string DefaultTenantId { get; set; } = "serioustech";

    public string DefaultEmpresaId { get; set; } = "serious";
}
