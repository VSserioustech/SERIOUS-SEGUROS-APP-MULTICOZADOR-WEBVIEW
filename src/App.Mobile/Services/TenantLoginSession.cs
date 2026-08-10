namespace App.Mobile.Services;

public sealed record TenantLoginSession(
    string TenantCode,
    string AccessToken,
    string TokenType,
    DateTimeOffset? ExpiresAt,
    string TenantId,
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
