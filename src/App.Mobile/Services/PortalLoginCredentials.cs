namespace App.Mobile.Services;

public sealed record PortalLoginCredentials(
    string TenantCode,
    string Email,
    string Password);
