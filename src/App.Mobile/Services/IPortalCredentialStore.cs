namespace App.Mobile.Services;

public interface IPortalCredentialStore
{
    Task SaveAsync(string tenantCode, string email, string password);

    Task<PortalLoginCredentials?> GetAsync(TenantLoginSession? session);

    void Clear();
}
