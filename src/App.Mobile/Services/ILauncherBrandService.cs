namespace App.Mobile.Services;

public interface ILauncherBrandService
{
    void Apply(string? launcherIconKey);

    void ApplyDefault();
}
