namespace App.Mobile.Services;

public sealed class LauncherBrandService : ILauncherBrandService
{
    public void Apply(string? launcherIconKey)
    {
        ApplyDefault();
    }

    public void ApplyDefault()
    {
    }
}
