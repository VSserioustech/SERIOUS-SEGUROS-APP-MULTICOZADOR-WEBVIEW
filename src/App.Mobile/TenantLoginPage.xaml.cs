using App.Mobile.Services;

namespace App.Mobile;

public partial class TenantLoginPage : ContentPage, ISystemBarsPage
{
    private readonly IWhitelabelClient _client;
    private readonly IWhitelabelState _state;
    private readonly IServiceProvider _serviceProvider;

    public TenantLoginPage(
        IWhitelabelClient client,
        IWhitelabelState state,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _client = client;
        _state = state;
        _serviceProvider = serviceProvider;

        tenantCodeEntry.Text = "8657";
#if DEBUG
        emailEntry.Text = "global.admin@serioustech.com.mx";
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplySystemBars();
    }

    public void ApplySystemBars()
    {

#if ANDROID
        MainActivity.ApplyDefaultPrePortalSystemBars();
#endif
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var tenantCode = tenantCodeEntry.Text?.Trim() ?? string.Empty;
        var email = emailEntry.Text?.Trim() ?? string.Empty;
        var password = passwordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(tenantCode) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlertAsync("Tenant", "Captura tenantCode, email y password.", "Aceptar");
            return;
        }

        await LoginAsync(tenantCode, email, password);
    }

    private async Task LoginAsync(string tenantCode, string email, string password)
    {
        SetBusy(true, "Validando tenant...");

        try
        {
            var session = await _client.LoginByCodeAsync(tenantCode, email, password);
            SetBusy(true, "Leyendo perfil white label...");
            var profile = await _client.GetWhiteLabelProfileAsync(session);
            await _state.SaveTenantAsync(session, profile);

            var wizardPage = _serviceProvider.GetRequiredService<WhitelabelWizardPage>();
            Microsoft.Maui.Controls.Application.Current?.Windows[0].Page = wizardPage;
        }
        catch (Exception)
        {
            await DisplayAlertAsync(
                "No fue posible ingresar",
                "Revisa el tenantCode y las credenciales. Si el login funciona pero el perfil falla, la app usará mock cuando backend permita token válido.",
                "Aceptar");
        }
        finally
        {
            SetBusy(false, "Listo para ingresar.");
        }
    }

    private async void OnQrClicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "Lectura QR",
            "POC preparado. Se conectará cuando el portal genere el QR con tenantCode y usuario.",
            "Aceptar");
    }

    private void SetBusy(bool isBusy, string message)
    {
        loginButton.IsEnabled = !isBusy;
        tenantCodeEntry.IsEnabled = !isBusy;
        emailEntry.IsEnabled = !isBusy;
        passwordEntry.IsEnabled = !isBusy;
        statusLabel.Text = message;
    }
}
