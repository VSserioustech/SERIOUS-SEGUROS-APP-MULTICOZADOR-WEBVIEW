namespace App.Mobile.Services;

public interface IWhitelabelState
{
    WhitelabelConfig? Current { get; }

    TenantLoginSession? TenantSession { get; }

    TenantWhiteLabelProfile? TenantProfile { get; }

    bool HasTenant { get; }

    bool HasSelection { get; }

    Task LoadAsync();

    Task<WhitelabelConfig?> LoadSelectionAsync();

    Task SaveTenantAsync(TenantLoginSession session, TenantWhiteLabelProfile profile);

    Task SaveAsync(WhitelabelConfig config);

    void ClearSelection();

    void ClearTenant();
}
