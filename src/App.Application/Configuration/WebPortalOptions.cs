namespace App.Application.Configuration;

public sealed class WebPortalOptions
{
    public const string SectionName = "WebPortal";

    public string StartUrl { get; set; } = string.Empty;

    public string[] AllowedHosts { get; set; } = [];
}
