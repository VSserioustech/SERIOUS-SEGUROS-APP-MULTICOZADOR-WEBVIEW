using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace App.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int NotificationsPermissionRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(
                [global::Android.Manifest.Permission.PostNotifications],
                NotificationsPermissionRequestCode);
        }
    }

    public static void ApplySystemBarColors(string hexColor)
    {
        ApplySystemBarColors(hexColor, hexColor);
    }

    public static void ApplySystemBarColors(string statusBarHexColor, string navigationBarHexColor)
    {
        var activity = Platform.CurrentActivity as MainActivity;
        var window = activity?.Window;

        if (window is null ||
            string.IsNullOrWhiteSpace(statusBarHexColor) ||
            string.IsNullOrWhiteSpace(navigationBarHexColor))
        {
            return;
        }

        try
        {
            var statusBarColor = Android.Graphics.Color.ParseColor(NormalizeHexColor(statusBarHexColor));
            var navigationBarColor = Android.Graphics.Color.ParseColor(NormalizeHexColor(navigationBarHexColor));

            window.ClearFlags(WindowManagerFlags.TranslucentStatus);
            window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                window.SetDecorFitsSystemWindows(true);
            }

            window.DecorView.SetBackgroundColor(statusBarColor);
            window.SetStatusBarColor(statusBarColor);
            window.SetNavigationBarColor(navigationBarColor);

            var lightStatusBar = IsLightColor(statusBarColor);
            var lightNavigationBar = IsLightColor(navigationBarColor);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                var appearance = 0;

                if (lightStatusBar)
                {
                    appearance |= (int)WindowInsetsControllerAppearance.LightStatusBars;
                }

                if (lightNavigationBar)
                {
                    appearance |= (int)WindowInsetsControllerAppearance.LightNavigationBars;
                }

                window.InsetsController?.SetSystemBarsAppearance(
                    appearance,
                    (int)(WindowInsetsControllerAppearance.LightStatusBars | WindowInsetsControllerAppearance.LightNavigationBars));
            }

            ApplyLegacySystemBarIconContrast(window, lightStatusBar, lightNavigationBar);
        }
        catch
        {
            // Keep the default Android system bars if the backend sends an invalid color.
        }
    }

    private static void ApplyLegacySystemBarIconContrast(
        global::Android.Views.Window window,
        bool lightStatusBar,
        bool lightNavigationBar)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            var flags = window.DecorView.SystemUiVisibility;

            flags = lightStatusBar
                ? flags | (StatusBarVisibility)SystemUiFlags.LightStatusBar
                : flags & ~(StatusBarVisibility)SystemUiFlags.LightStatusBar;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                flags = lightNavigationBar
                    ? flags | (StatusBarVisibility)SystemUiFlags.LightNavigationBar
                    : flags & ~(StatusBarVisibility)SystemUiFlags.LightNavigationBar;
            }

            window.DecorView.SystemUiVisibility = flags;
        }
    }

    private static string NormalizeHexColor(string value)
    {
        var candidate = value.Trim();
        return candidate.StartsWith('#') ? candidate : "#" + candidate;
    }

    private static bool IsLightColor(Android.Graphics.Color color)
    {
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255;
        return luminance > 0.72;
    }
}
