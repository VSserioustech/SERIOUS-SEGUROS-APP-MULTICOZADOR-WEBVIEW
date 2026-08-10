namespace App.Mobile.Services;

internal sealed class WebViewCookieProvider : IWebViewCookieProvider
{
    public Task<string?> GetCookieHeaderAsync(Uri uri)
    {
#if ANDROID
        var cookies = Android.Webkit.CookieManager.Instance?.GetCookie(uri.AbsoluteUri);
        return Task.FromResult<string?>(string.IsNullOrWhiteSpace(cookies) ? null : cookies);
#elif IOS || MACCATALYST
        var completion = new TaskCompletionSource<string?>();
        WebKit.WKWebsiteDataStore.DefaultDataStore.HttpCookieStore.GetAllCookies(cookies =>
        {
            var cookieHeader = string.Join("; ", cookies
                .Where(cookie => IsCookieForHost(cookie.Domain, uri.Host))
                .Select(cookie => $"{cookie.Name}={cookie.Value}"));

            completion.SetResult(string.IsNullOrWhiteSpace(cookieHeader) ? null : cookieHeader);
        });

        return completion.Task;
#else
        return Task.FromResult<string?>(null);
#endif
    }

#if IOS || MACCATALYST
    private static bool IsCookieForHost(string cookieDomain, string host)
    {
        var normalizedDomain = cookieDomain.TrimStart('.');
        return host.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{normalizedDomain}", StringComparison.OrdinalIgnoreCase);
    }
#endif
}
