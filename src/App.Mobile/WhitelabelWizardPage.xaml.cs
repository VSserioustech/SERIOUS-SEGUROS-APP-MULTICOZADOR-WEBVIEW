using App.Mobile.Services;

namespace App.Mobile;

public partial class WhitelabelWizardPage : ContentPage, ISystemBarsPage
{
    private readonly IWhitelabelClient _client;
    private readonly IWhitelabelState _state;
    private readonly IServiceProvider _serviceProvider;
    private CompanyViewModel[] _companies = [];

    public WhitelabelWizardPage(
        IWhitelabelClient client,
        IWhitelabelState state,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _client = client;
        _state = state;
        _serviceProvider = serviceProvider;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplySystemBars();

        await RefreshAndLoadCompaniesAsync();
    }

    public void ApplySystemBars()
    {

#if ANDROID
        MainActivity.ApplyDefaultPrePortalSystemBars();
#endif
    }

    private async Task RefreshAndLoadCompaniesAsync()
    {
        var session = _state.TenantSession;
        if (session is null)
        {
            GoToTenantLogin();
            return;
        }

        statusLabel.Text = "Actualizando empresas del tenant...";
        companiesPicker.IsEnabled = false;
        enterButton.IsEnabled = false;

        TenantWhiteLabelProfile? profile = null;
        try
        {
            profile = await _client.GetWhiteLabelProfileAsync(session);
            await _state.SaveTenantAsync(session, profile);
        }
        catch
        {
            profile = _state.TenantProfile;
        }

        if (profile is null || session is null)
        {
            GoToTenantLogin();
            return;
        }

        tenantLabel.Text = $"{profile.Tenant.Name} · tenantCode {session.TenantCode}";

        _companies = profile.Companies
            .Where(company => company.IsActive)
            .OrderBy(company => company.Name)
            .Select(company =>
            {
                var config = _client.CreateConfig(profile, company);
                return CompanyViewModel.From(company, config);
            })
            .ToArray();

        companiesPicker.ItemsSource = _companies;
        companiesPicker.IsEnabled = _companies.Length > 0;
        statusLabel.Text = _companies.Length == 0
            ? "Este tenant no tiene empresas activas."
            : $"{_companies.Length} empresa(s) disponibles.";

        if (_companies.Length == 1)
        {
            companiesPicker.SelectedIndex = 0;
        }
    }

    private async void OnCompanySelected(object? sender, EventArgs e)
    {
        var company = companiesPicker.SelectedItem as CompanyViewModel;
        enterButton.IsEnabled = company is not null;
        previewCard.IsVisible = company is not null;

        if (company is null)
        {
            return;
        }

        companyLogoImage.Source = WhitelabelLogoSource.FromFallbackAsset(company.Config.LauncherIconKey);
        companyNameLabel.Text = company.DisplayName;
        companyUrlLabel.Text = company.Config.Url;
        companyStatusLabel.Text = company.IsConfigured
            ? "White label configurado"
            : "Sin configuración: se usará identidad default";
        enterButton.BackgroundColor = Color.FromArgb(NormalizeHexColor(company.Config.PrimaryColor, "#175CD3"));
        var logoSource = await WhitelabelLogoSource.CreateAsync(
            company.Config.LogoUrl,
            company.Config.LauncherIconKey);
        if (companiesPicker.SelectedItem is CompanyViewModel current && current.Config.EmpresaId == company.Config.EmpresaId)
        {
            companyLogoImage.Source = logoSource;
        }
    }

    private async void OnEnterClicked(object? sender, EventArgs e)
    {
        if (companiesPicker.SelectedItem is not CompanyViewModel company)
        {
            await DisplayAlertAsync("Empresa", "Selecciona una empresa para continuar.", "Aceptar");
            return;
        }

        await _state.SaveAsync(company.Config);
        var mainPage = _serviceProvider.GetRequiredService<MainPage>();
        Microsoft.Maui.Controls.Application.Current?.Windows[0].Page = mainPage;
    }

    private async void OnExitTenantClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            "Salir de tenant",
            "Se borrará el token, perfil, empresa seleccionada y configuración local de white label.",
            "Salir",
            "Cancelar");

        if (!confirmed)
        {
            return;
        }

        _state.ClearTenant();
        GoToTenantLogin();
    }

    private void GoToTenantLogin()
    {
        var loginPage = _serviceProvider.GetRequiredService<TenantLoginPage>();
        Microsoft.Maui.Controls.Application.Current?.Windows[0].Page = loginPage;
    }

    private static string NormalizeHexColor(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var candidate = value.Trim();
        return candidate.StartsWith('#') ? candidate : "#" + candidate;
    }

    private sealed record CompanyViewModel(
        string DisplayName,
        bool IsConfigured,
        WhitelabelConfig Config)
    {
        public static CompanyViewModel From(TenantCompanyProfile company, WhitelabelConfig config) =>
            new(
                DisplayName: company.WhiteLabel.Configured
                    ? config.NombreAplicacion
                    : $"{company.Name} (default)",
                IsConfigured: company.WhiteLabel.Configured,
                Config: config);
    }
}
