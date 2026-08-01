namespace App.Mobile.Services;

public interface IWhitelabelClient
{
    Task<TenantLoginSession> LoginByCodeAsync(
        string tenantCode,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<TenantWhiteLabelProfile> GetWhiteLabelProfileAsync(
        TenantLoginSession session,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhitelabelConfig>> GetAvailableCompaniesAsync(CancellationToken cancellationToken = default);

    Task<WhitelabelConfig> GetCompanyAsync(string empresaId, CancellationToken cancellationToken = default);

    WhitelabelConfig CreateConfig(TenantWhiteLabelProfile profile, TenantCompanyProfile company);
}
