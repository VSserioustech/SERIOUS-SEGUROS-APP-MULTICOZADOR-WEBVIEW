using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace App.Mobile.Services;

internal sealed partial class PortalFileDownloader(IWebViewCookieProvider cookieProvider) : IPortalFileDownloader
{
    private static readonly Regex UnsafeFileNameCharacters = new(@"[^\w\-. ]+", RegexOptions.Compiled);

    public async Task DownloadAndShareAsync(Uri downloadUri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        var cookieHeader = await cookieProvider.GetCookieHeaderAsync(downloadUri);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        using var client = new HttpClient();
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var fileName = ResolveFileName(downloadUri, response.Content.Headers.ContentDisposition);
        var destinationPath = Path.Combine(FileSystem.CacheDirectory, fileName);

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = File.Create(destinationPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Guardar cotización",
            File = new ShareFile(destinationPath)
        });
    }

    private static string ResolveFileName(Uri downloadUri, ContentDispositionHeaderValue? contentDisposition)
    {
        var rawFileName = contentDisposition?.FileNameStar ??
            contentDisposition?.FileName?.Trim('"') ??
            Path.GetFileName(downloadUri.LocalPath);

        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            rawFileName = "cotizacion.pdf";
        }

        var fileName = UnsafeFileNameCharacters.Replace(rawFileName, "_");
        return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.pdf";
    }
}
