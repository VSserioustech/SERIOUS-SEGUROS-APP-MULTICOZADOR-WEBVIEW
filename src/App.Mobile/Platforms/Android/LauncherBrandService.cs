using Android.Content;
using Android.Content.PM;
using App.Mobile.Services;

namespace App.Mobile.Platforms.Android;

public sealed class LauncherBrandService : ILauncherBrandService
{
    private const string DefaultAlias = "LauncherDefault";
    private const string SeriousAlias = "LauncherSerious";
    private const string AliAlias = "LauncherAli";
    private const string CbeAlias = "LauncherCbe";

    private static readonly string[] AllAliases =
    [
        DefaultAlias,
        SeriousAlias,
        AliAlias,
        CbeAlias
    ];

    public void Apply(string? launcherIconKey)
    {
        var aliasName = NormalizeAlias(launcherIconKey);
        SetEnabledAlias(aliasName);
    }

    public void ApplyDefault()
    {
        SetEnabledAlias(DefaultAlias);
    }

    private static string NormalizeAlias(string? launcherIconKey)
    {
        return launcherIconKey?.Trim().ToLowerInvariant() switch
        {
            "serious" or "serious-seguros" or "serious-tech" or "serioustech" => SeriousAlias,
            "ali" or "ali-asociados" or "ali-seguros" => AliAlias,
            "cbe" or "cbe+" or "cbe-plus" => CbeAlias,
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

        // Enable the desired launcher entry first so the app never disappears from the launcher.
        SetAliasState(packageManager, packageName, enabledAlias, enabled: true);

        foreach (var alias in AllAliases.Where(alias => !string.Equals(alias, enabledAlias, StringComparison.Ordinal)))
        {
            SetAliasState(packageManager, packageName, alias, enabled: false);
        }
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
