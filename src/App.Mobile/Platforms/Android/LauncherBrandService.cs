using Android.Content;
using Android.Content.PM;
using App.Mobile.Services;

namespace App.Mobile.Platforms.Android;

public sealed class LauncherBrandService : ILauncherBrandService
{
    private const string PendingLauncherIconKeyPreferenceKey = "Whitelabel.PendingLauncherIconKey.v1";
    private const string DefaultAlias = "LauncherDefault";
    private const string DefaultIconKey = "default";
    private const string SeriousAlias = "LauncherSerious";
    private const string AliAlias = "LauncherAli";
    private const string CbeAlias = "LauncherCbe";
    private const string OakAlias = "LauncherOak";

    private static readonly string[] AllAliases =
    [
        DefaultAlias,
        SeriousAlias,
        AliAlias,
        CbeAlias,
        OakAlias
    ];

    public void Apply(string? launcherIconKey)
    {
        QueueAliasChange(launcherIconKey);
    }

    public void ApplyDefault()
    {
        QueueAliasChange(DefaultIconKey);
    }

    public static void ApplyQueuedIfAny()
    {
        var pendingIconKey = Preferences.Default.Get(PendingLauncherIconKeyPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(pendingIconKey))
        {
            return;
        }

        var aliasName = NormalizeAlias(pendingIconKey);
        SetEnabledAlias(aliasName);
        Preferences.Default.Remove(PendingLauncherIconKeyPreferenceKey);
    }

    private static void QueueAliasChange(string? launcherIconKey)
    {
        Preferences.Default.Set(
            PendingLauncherIconKeyPreferenceKey,
            string.IsNullOrWhiteSpace(launcherIconKey) ? DefaultIconKey : launcherIconKey.Trim());
    }

    private static string NormalizeAlias(string? launcherIconKey)
    {
        return launcherIconKey?.Trim().ToLowerInvariant() switch
        {
            DefaultIconKey => DefaultAlias,
            "serious" or "serious-seguros" or "serious-tech" or "serioustech" => SeriousAlias,
            "ali" or "ali-asociados" or "ali-seguros" => AliAlias,
            "cbe" or "cbe+" or "cbe-plus" => CbeAlias,
            "oak" or "oak-seguros" or "oak-insurance" => OakAlias,
            _ => DefaultAlias
        };
    }

    private static void SetEnabledAlias(string enabledAlias)
    {
        var context = Platform.AppContext;
        if (context is null)
        {
            return;
        }

        var packageManager = context.PackageManager;
        if (packageManager is null)
        {
            return;
        }

        var packageName = context.PackageName;
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return;
        }

        if (IsAliasStateAlreadyApplied(packageManager, packageName, enabledAlias))
        {
            return;
        }

        // Enable the desired launcher entry first so the app never disappears from the launcher.
        SetAliasState(packageManager, packageName, enabledAlias, enabled: true);

        foreach (var alias in AllAliases.Where(alias => !string.Equals(alias, enabledAlias, StringComparison.Ordinal)))
        {
            SetAliasState(packageManager, packageName, alias, enabled: false);
        }
    }

    private static bool IsAliasStateAlreadyApplied(
        PackageManager packageManager,
        string packageName,
        string enabledAlias)
    {
        foreach (var alias in AllAliases)
        {
            var componentName = new ComponentName(packageName, $"{packageName}.{alias}");
            var state = packageManager.GetComponentEnabledSetting(componentName);
            var isEnabled = state is ComponentEnabledState.Enabled ||
                (state is ComponentEnabledState.Default && string.Equals(alias, DefaultAlias, StringComparison.Ordinal));

            if (string.Equals(alias, enabledAlias, StringComparison.Ordinal))
            {
                if (!isEnabled)
                {
                    return false;
                }

                continue;
            }

            if (isEnabled)
            {
                return false;
            }
        }

        return true;
    }

    private static void SetAliasState(
        PackageManager packageManager,
        string packageName,
        string aliasName,
        bool enabled)
    {
        var componentName = new ComponentName(packageName, $"{packageName}.{aliasName}");
        var state = enabled
            ? ComponentEnabledState.Enabled
            : ComponentEnabledState.Disabled;

        packageManager.SetComponentEnabledSetting(
            componentName,
            state,
            ComponentEnableOption.DontKillApp);
    }
}
