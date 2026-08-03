using System.Text.Json;

namespace App.Mobile.Services;

public sealed class WhitelabelState : IWhitelabelState
{
    private const string PreferenceKey = "Whitelabel.Current.v2";
    private const string TenantSessionPreferenceKey = "Whitelabel.TenantSession.v1";
    private const string TenantProfilePreferenceKey = "Whitelabel.TenantProfile.v1";
    private readonly ILauncherBrandService _launcherBrandService;

    public WhitelabelState(ILauncherBrandService launcherBrandService)
    {
        _launcherBrandService = launcherBrandService;
    }

    public WhitelabelConfig? Current { get; private set; }

    public TenantLoginSession? TenantSession { get; private set; }

    public TenantWhiteLabelProfile? TenantProfile { get; private set; }

    public bool HasTenant => TenantSession is not null && TenantProfile is not null;

    public bool HasSelection => Current is not null;

    public Task LoadAsync()
    {
        LoadTenantFromPreferences();
        LoadSelectionFromPreferences();
        ApplyLauncherBrand();
        return Task.CompletedTask;
    }

    public Task<WhitelabelConfig?> LoadSelectionAsync()
    {
        if (Current is not null)
        {
            return Task.FromResult<WhitelabelConfig?>(Current);
        }

        LoadSelectionFromPreferences();
        return Task.FromResult(Current);
    }

    public Task SaveTenantAsync(TenantLoginSession session, TenantWhiteLabelProfile profile)
    {
        TenantSession = session;
        TenantProfile = profile;
        Preferences.Default.Set(TenantSessionPreferenceKey, JsonSerializer.Serialize(session));
        Preferences.Default.Set(TenantProfilePreferenceKey, JsonSerializer.Serialize(profile));
        return Task.CompletedTask;
    }

    public Task SaveAsync(WhitelabelConfig config)
    {
        Current = config;
        Preferences.Default.Set(PreferenceKey, JsonSerializer.Serialize(config));
        _launcherBrandService.Apply(config.LauncherIconKey);
        return Task.CompletedTask;
    }

    public void ClearSelection()
    {
        Current = null;
        Preferences.Default.Remove(PreferenceKey);
        _launcherBrandService.ApplyDefault();
    }

    public void ClearTenant()
    {
        ClearSelection();
        TenantSession = null;
        TenantProfile = null;
        Preferences.Default.Remove(TenantSessionPreferenceKey);
        Preferences.Default.Remove(TenantProfilePreferenceKey);
    }

    private void LoadTenantFromPreferences()
    {
        if (TenantSession is not null && TenantProfile is not null)
        {
            return;
        }

        var rawSession = Preferences.Default.Get(TenantSessionPreferenceKey, string.Empty);
        var rawProfile = Preferences.Default.Get(TenantProfilePreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(rawSession) || string.IsNullOrWhiteSpace(rawProfile))
        {
            TenantSession = null;
            TenantProfile = null;
            return;
        }

        try
        {
            TenantSession = JsonSerializer.Deserialize<TenantLoginSession>(rawSession);
            TenantProfile = JsonSerializer.Deserialize<TenantWhiteLabelProfile>(rawProfile);
        }
        catch
        {
            Preferences.Default.Remove(TenantSessionPreferenceKey);
            Preferences.Default.Remove(TenantProfilePreferenceKey);
            TenantSession = null;
            TenantProfile = null;
        }
    }

    private void LoadSelectionFromPreferences()
    {
        if (Current is not null)
        {
            return;
        }

        var raw = Preferences.Default.Get(PreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            Current = JsonSerializer.Deserialize<WhitelabelConfig>(raw);
            ApplyLauncherBrand();
        }
        catch
        {
            Preferences.Default.Remove(PreferenceKey);
            Current = null;
            _launcherBrandService.ApplyDefault();
        }
    }

    private void ApplyLauncherBrand()
    {
        if (Current is null)
        {
            _launcherBrandService.ApplyDefault();
            return;
        }

        _launcherBrandService.Apply(Current.LauncherIconKey);
    }
}
