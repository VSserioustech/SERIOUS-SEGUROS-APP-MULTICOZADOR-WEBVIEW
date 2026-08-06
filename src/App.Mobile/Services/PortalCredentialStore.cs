namespace App.Mobile.Services;

public sealed class PortalCredentialStore : IPortalCredentialStore
{
    private const string TenantCodeKey = "PortalLogin.TenantCode.v1";
    private const string EmailKey = "PortalLogin.Email.v1";
    private const string PasswordKey = "PortalLogin.Password.v1";

    public async Task SaveAsync(string tenantCode, string email, string password)
    {
        await SecureStorage.Default.SetAsync(TenantCodeKey, tenantCode.Trim());
        await SecureStorage.Default.SetAsync(EmailKey, email.Trim());
        await SecureStorage.Default.SetAsync(PasswordKey, password);
    }

    public async Task<PortalLoginCredentials?> GetAsync(TenantLoginSession? session)
    {
        if (session is null)
        {
            return null;
        }

        var tenantCode = await SecureStorage.Default.GetAsync(TenantCodeKey);
        var email = await SecureStorage.Default.GetAsync(EmailKey);
        var password = await SecureStorage.Default.GetAsync(PasswordKey);

        if (string.IsNullOrWhiteSpace(tenantCode) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            !string.Equals(tenantCode, session.TenantCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(email, session.Email, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new PortalLoginCredentials(tenantCode, email, password);
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(TenantCodeKey);
        SecureStorage.Default.Remove(EmailKey);
        SecureStorage.Default.Remove(PasswordKey);
    }
}
