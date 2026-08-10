using App.Mobile.Services;

namespace App.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWhitelabelState _whitelabelState;

    public App(
        IServiceProvider serviceProvider,
        IWhitelabelState whitelabelState)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _whitelabelState = whitelabelState;

        RequestedThemeChanged += OnRequestedThemeChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _whitelabelState.LoadAsync().GetAwaiter().GetResult();

        Page page = !_whitelabelState.HasTenant
            ? _serviceProvider.GetRequiredService<TenantLoginPage>()
            : !_whitelabelState.HasSelection
                ? _serviceProvider.GetRequiredService<WhitelabelWizardPage>()
                : _serviceProvider.GetRequiredService<MainPage>();

        return new Window(page) { Title = _whitelabelState.Current?.NombreAplicacion ?? "Serious" };
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Windows.FirstOrDefault()?.Page is ISystemBarsPage systemBarsPage)
            {
                systemBarsPage.ApplySystemBars();
            }
        });
    }
}
