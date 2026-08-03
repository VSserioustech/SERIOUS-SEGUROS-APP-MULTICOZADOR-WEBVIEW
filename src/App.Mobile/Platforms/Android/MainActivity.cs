using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AColor = Android.Graphics.Color;
using AView = Android.Views.View;

namespace App.Mobile;

[Activity(
    Name = "com.serioustech.seriousseguros.portal.MainActivity",
    Theme = "@style/Serious.SplashTheme",
    MainLauncher = false,
    Exported = true,
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
    private static readonly int StatusBarToolbarOverlayId = AView.GenerateViewId();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        SetTheme(Resource.Style.Serious_MainTheme);
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

    protected override void OnStop()
    {
        base.OnStop();
        Platforms.Android.LauncherBrandService.ApplyQueuedIfAny();
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
            ApplySystemBarColors(window, statusBarHexColor, navigationBarHexColor, fitsSystemWindows: false);

            // Some OEM skins (ColorOS/Android 16) repaint the system status bar after MAUI/WebView
            // attaches. Re-apply the native top toolbar one frame later.
            window.DecorView.Post(() =>
                ApplySystemBarColors(window, statusBarHexColor, navigationBarHexColor, fitsSystemWindows: false));
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

        ApplySystemBarColors(window, DefaultStatusBarColor, DefaultNavigationBarColor, fitsSystemWindows: false);

        // MAUI/Android can re-apply theme flags just after the first native page is created.
        // Posting one extra pass keeps first-launch light mode from leaving dark icons on a dark bar.
        window.DecorView.Post(() =>
            ApplySystemBarColors(window, DefaultStatusBarColor, DefaultNavigationBarColor, fitsSystemWindows: false));
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
        window.SetStatusBarColor(AColor.Transparent);
        window.SetNavigationBarColor(navigationBarColor);
        ApplyNativeStatusBarToolbar(window, statusBarColor);

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

    private static void ApplyNativeStatusBarToolbar(
        global::Android.Views.Window window,
        AColor statusBarColor)
    {
        if (window.DecorView is not ViewGroup decorView)
        {
            return;
        }

        var statusBarHeight = GetStatusBarHeightPixels(decorView);
        if (statusBarHeight <= 0)
        {
            return;
        }

        var toolbar = decorView.FindViewById<AView>(StatusBarToolbarOverlayId);
        if (toolbar is null)
        {
            toolbar = new AView(decorView.Context)
            {
                Id = StatusBarToolbarOverlayId,
                Clickable = false,
                Focusable = false
            };

            var layoutParams = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                statusBarHeight,
                GravityFlags.Top);

            decorView.AddView(toolbar, layoutParams);
        }
        else if (toolbar.LayoutParameters is ViewGroup.LayoutParams layoutParameters &&
                 layoutParameters.Height != statusBarHeight)
        {
            layoutParameters.Height = statusBarHeight;
            toolbar.LayoutParameters = layoutParameters;
        }

        toolbar.SetBackgroundColor(statusBarColor);
        toolbar.BringToFront();
        toolbar.Visibility = ViewStates.Visible;
    }

    private static int GetStatusBarHeightPixels(AView decorView)
    {
        var resources = decorView.Context.Resources;
        var resourceId = resources.GetIdentifier("status_bar_height", "dimen", "android");
        return resourceId > 0 ? resources.GetDimensionPixelSize(resourceId) : 0;
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
