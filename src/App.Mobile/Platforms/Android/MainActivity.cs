using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AColor = Android.Graphics.Color;

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
    private const string DefaultStatusBarColor = "#0F172A";
    private const string DefaultNavigationBarColor = "#F6F8FB";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplyDefaultSystemBars();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(
                [global::Android.Manifest.Permission.PostNotifications],
                NotificationsPermissionRequestCode);
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        ReapplyCurrentPageSystemBars();
    }

    public static void ApplyDefaultPrePortalSystemBars()
    {
        ApplySystemBarColors(DefaultStatusBarColor, DefaultNavigationBarColor);
    }

    public static void ApplySystemBarColors(string hexColor)
    {
        ApplySystemBarColors(hexColor, hexColor);
    }

    public static void ApplyImmersiveSystemBarColors(string statusBarHexColor, string navigationBarHexColor)
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
            ApplySystemBarColors(window, statusBarHexColor, navigationBarHexColor, fitsSystemWindows: false);

            // ColorOS/Android 15+ can redraw edge-to-edge flags after WebView attaches.
            // This second pass keeps the status icons over the toolbar color instead of the page canvas.
            window.DecorView.Post(() =>
                ApplySystemBarColors(window, statusBarHexColor, navigationBarHexColor, fitsSystemWindows: false));
        }
        catch
        {
            // Keep the current Android system bars if the backend sends an invalid color.
        }
    }

    public static double GetStatusBarHeight()
    {
        var activity = Platform.CurrentActivity as MainActivity;
        if (activity is null)
        {
            return 0;
        }

        var resourceId = activity.Resources.GetIdentifier("status_bar_height", "dimen", "android");
        if (resourceId <= 0)
        {
            return 0;
        }

        var pixels = activity.Resources.GetDimensionPixelSize(resourceId);
        return pixels / activity.Resources.DisplayMetrics.Density;
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
            ApplySystemBarColors(window, statusBarHexColor, navigationBarHexColor, fitsSystemWindows: true);
        }
        catch
        {
            // Keep the default Android system bars if the backend sends an invalid color.
        }
    }

    private void ApplyDefaultSystemBars()
    {
        var window = Window;
        if (window is null)
        {
            return;
        }

        ApplySystemBarColors(window, DefaultStatusBarColor, DefaultNavigationBarColor, fitsSystemWindows: true);

        // MAUI/Android can re-apply theme flags just after the first native page is created.
        // Posting one extra pass keeps first-launch light mode from leaving dark icons on a dark bar.
        window.DecorView.Post(() =>
            ApplySystemBarColors(window, DefaultStatusBarColor, DefaultNavigationBarColor, fitsSystemWindows: true));
    }

    private static void ReapplyCurrentPageSystemBars()
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            var currentPage = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;

            if (currentPage is ISystemBarsPage systemBarsPage)
            {
                systemBarsPage.ApplySystemBars();
                return;
            }

            ApplyDefaultPrePortalSystemBars();
        });
    }

    private static void ApplySystemBarColors(
        global::Android.Views.Window window,
        string statusBarHexColor,
        string navigationBarHexColor,
        bool fitsSystemWindows)
    {
        var statusBarColor = AColor.ParseColor(NormalizeHexColor(statusBarHexColor));
        var navigationBarColor = AColor.ParseColor(NormalizeHexColor(navigationBarHexColor));

        window.ClearFlags(WindowManagerFlags.TranslucentStatus);
        window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            window.SetDecorFitsSystemWindows(fitsSystemWindows);
        }

        window.DecorView.SetBackgroundColor(statusBarColor);
        window.SetStatusBarColor(fitsSystemWindows ? statusBarColor : AColor.Transparent);
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
