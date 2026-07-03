using Microsoft.Maui.Handlers;

namespace App.Mobile;

internal static class PlatformWebViewConfiguration
{
    public static void Configure()
    {
#if ANDROID
        WebViewHandler.Mapper.AppendToMapping("PortalSecurity", (handler, _) =>
        {
            var settings = handler.PlatformView.Settings;
            settings.JavaScriptEnabled = true;
            settings.DomStorageEnabled = true;
            settings.AllowFileAccess = false;
            settings.AllowContentAccess = false;
            settings.MixedContentMode = Android.Webkit.MixedContentHandling.NeverAllow;
            settings.SetSupportMultipleWindows(false);
        });
#elif IOS || MACCATALYST
        WebViewHandler.Mapper.AppendToMapping("PortalNavigation", (handler, _) =>
        {
            handler.PlatformView.AllowsBackForwardNavigationGestures = true;
            handler.PlatformView.ScrollView.Bounces = false;
        });
#endif
    }
}
