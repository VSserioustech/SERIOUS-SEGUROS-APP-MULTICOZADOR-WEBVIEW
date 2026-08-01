namespace App.Mobile.Services;

internal interface IWebViewCookieProvider
{
    Task<string?> GetCookieHeaderAsync(Uri uri);
}
