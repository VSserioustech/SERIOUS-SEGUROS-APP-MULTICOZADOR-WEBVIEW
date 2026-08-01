using Android.App;
using Android.Content;
using Android.Provider;
using Android.Webkit;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Java.Interop;
using Microsoft.Maui.Handlers;
using System.Runtime.Versioning;

namespace App.Mobile.Platforms.Android;

public sealed class CustomWebViewHandler : WebViewHandler
{
    protected override global::Android.Webkit.WebView CreatePlatformView()
    {
        var webView = base.CreatePlatformView();
        webView.AddJavascriptInterface(new BlobDownloadBridge(), "MauiBlobDownloader");
        webView.SetDownloadListener(new PortalDownloadListener(webView));

        return webView;
    }
}

public sealed class PortalDownloadListener(global::Android.Webkit.WebView webView) : Java.Lang.Object, IDownloadListener
{
    public void OnDownloadStart(
        string? url,
        string? userAgent,
        string? contentDisposition,
        string? mimetype,
        long contentLength)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            DownloadBlobWithJavascript(url, contentDisposition, mimetype);
            return;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            BlobDownloadBridge.ShowToast("Tipo de descarga no soportado.");
            return;
        }

        DownloadHttpFile(url, userAgent, contentDisposition, mimetype);
    }

    private void DownloadHttpFile(
        string url,
        string? userAgent,
        string? contentDisposition,
        string? mimetype)
    {
        var request = new DownloadManager.Request(global::Android.Net.Uri.Parse(url));
        if (!string.IsNullOrWhiteSpace(mimetype))
        {
            request.SetMimeType(mimetype);
        }

        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            request.AddRequestHeader("User-Agent", userAgent);
        }

        var cookies = CookieManager.Instance?.GetCookie(url);
        if (!string.IsNullOrWhiteSpace(cookies))
        {
            request.AddRequestHeader("Cookie", cookies);
        }

        var fileName = ResolveFileName(url, contentDisposition, mimetype);
        request.SetTitle(fileName);
        request.SetDescription("Descargando cotización...");
        request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);
        request.SetAllowedOverMetered(true);
        request.SetAllowedOverRoaming(true);
        request.SetDestinationInExternalPublicDir(
            global::Android.OS.Environment.DirectoryDownloads,
            fileName);

        var downloadManager = (DownloadManager?)global::Android.App.Application.Context
            .GetSystemService(Context.DownloadService);
        downloadManager?.Enqueue(request);
    }

    private void DownloadBlobWithJavascript(
        string blobUrl,
        string? contentDisposition,
        string? mimetype)
    {
        var fileName = ResolveFileName(blobUrl, contentDisposition, mimetype);
        var escapedBlobUrl = EscapeJavaScript(blobUrl);
        var fallbackMimeType = EscapeJavaScript(mimetype ?? "application/octet-stream");
        var escapedFileName = EscapeJavaScript(fileName);
        var script = $$"""
            (function () {
                var blobUrl = '{{escapedBlobUrl}}';
                var fileName = '{{escapedFileName}}';
                var fallbackMimeType = '{{fallbackMimeType}}';

                function pad(value) {
                    return String(value).padStart(2, '0');
                }

                function extensionFromMimeType(mimeType, currentFileName) {
                    var lowerFileName = String(currentFileName || '').toLowerCase();
                    if (/\.(pdf|zip|xlsx|xls)$/.test(lowerFileName)) {
                        return lowerFileName.substring(lowerFileName.lastIndexOf('.') + 1);
                    }

                    var lowerMimeType = String(mimeType || '').toLowerCase();
                    if (lowerMimeType.indexOf('pdf') >= 0) {
                        return 'pdf';
                    }

                    if (lowerMimeType.indexOf('zip') >= 0) {
                        return 'zip';
                    }

                    if (lowerMimeType.indexOf('spreadsheet') >= 0 || lowerMimeType.indexOf('excel') >= 0) {
                        return 'xlsx';
                    }

                    return 'pdf';
                }

                function sanitizeNamePart(value) {
                    return String(value || '')
                        .normalize('NFD')
                        .replace(/[\u0300-\u036f]/g, '')
                        .replace(/[^a-zA-Z0-9_-]+/g, '-')
                        .replace(/^-+|-+$/g, '')
                        .slice(0, 40);
                }

                function buildContextFileName(mimeType, currentFileName, context) {
                    context = context || window.__mauiLastDownloadContext || {};

                    var portalFileName = String(currentFileName || '');
                    if (portalFileName.indexOf('-serioustech-') > 0 && /\.(pdf|zip|xlsx|xls)$/i.test(portalFileName)) {
                        return portalFileName;
                    }

                    var bank = sanitizeNamePart(context.bank || '');
                    var folioFromFileName = (portalFileName.match(/\b\d{5,}\b/) || [''])[0];
                    var folio = sanitizeNamePart(context.folio || folioFromFileName);
                    var now = new Date();
                    var dateStamp = now.getFullYear().toString() +
                        pad(now.getMonth() + 1) +
                        pad(now.getDate());
                    var extension = extensionFromMimeType(mimeType, currentFileName);
                    var company = normalizeCompanyName(bank, currentFileName);

                    if (company && company !== 'sin-banco') {
                        return [
                            company,
                            'serioustech',
                            dateStamp,
                            'cotizacion',
                            folio || (dateStamp + '-' + pad(now.getHours()) + pad(now.getMinutes()) + pad(now.getSeconds()))
                        ].join('-') + '.' + extension;
                    }

                    var stamp = dateStamp +
                        '-' +
                        pad(now.getHours()) +
                        pad(now.getMinutes()) +
                        pad(now.getSeconds());
                    var parts = ['cotizacion'];

                    if (bank) {
                        parts.push(bank);
                    }

                    if (folio) {
                        parts.push(folio);
                    }

                    parts.push(stamp);
                    return parts.join('-') + '.' + extension;
                }

                function normalizeCompanyName(value, currentFileName) {
                    var portalFileName = String(currentFileName || '').toLowerCase();
                    var seriousTechIndex = portalFileName.indexOf('-serioustech-');
                    if (seriousTechIndex > 0) {
                        return sanitizeNamePart(portalFileName.substring(0, seriousTechIndex)).toLowerCase();
                    }

                    var normalized = sanitizeNamePart(value || '').toUpperCase();
                    var ignoredCompanies = {
                        POLIZA: true,
                        POLIZA: true,
                        COTIZACION: true,
                        COTIZACIÓN: true,
                        COTIZADA: true,
                        PDF: true,
                        ZIP: true,
                        DESCARGA: true,
                        DESCARGAR: true
                    };

                    if (ignoredCompanies[normalized]) {
                        return 'sin-banco';
                    }

                    var companyMap = {
                        BAN: 'banorte',
                        ZRH: 'zurich',
                        ZURICH: 'zurich',
                        AFI: 'afirme',
                        GNP: 'gnp',
                        GS: 'general-seguros',
                        GENERAL: 'general-seguros',
                        AXA: 'axa',
                        HDI: 'hdi',
                        ANA: 'ana',
                        CHUBB: 'chubb',
                        MAPFRE: 'mapfre',
                        QUALITAS: 'qualitas',
                        SURA: 'sura',
                        ATLAS: 'atlas',
                        POTOSI: 'el-potosi',
                        ELPOTOSI: 'el-potosi',
                        INBURSA: 'inbursa'
                    };

                    return companyMap[normalized] || sanitizeNamePart(normalized || 'sin-banco').toLowerCase();
                }


                function sendBlob(blob) {
                    var reader = new FileReader();
                    reader.onloadend = function () {
                        var result = String(reader.result || '');
                        var base64 = result.indexOf(',') >= 0 ? result.split(',')[1] : result;
                        var context = window.__mauiCurrentBlobContext || window.__mauiLastDownloadContext || {};
                        var contextualFileName = buildContextFileName(blob.type || fallbackMimeType, fileName, context);
                        var companyFolder = normalizeCompanyName(context.bank || '', contextualFileName || fileName);
                        window.MauiBlobDownloader.receiveBlob(
                            base64,
                            blob.type || fallbackMimeType,
                            contextualFileName,
                            companyFolder);
                    };
                    reader.onerror = function () {
                        window.MauiBlobDownloader.reportError('FileReader error');
                    };
                    reader.readAsDataURL(blob);
                }

                function readWithXhr() {
                    var xhr = new XMLHttpRequest();
                    xhr.open('GET', blobUrl, true);
                    xhr.responseType = 'blob';
                    xhr.onload = function () {
                        if (xhr.status === 200 || xhr.status === 0) {
                            sendBlob(xhr.response);
                            return;
                        }

                        window.MauiBlobDownloader.reportError('XHR status ' + xhr.status);
                    };
                    xhr.onerror = function () {
                        window.MauiBlobDownloader.reportError('XHR error');
                    };
                    xhr.send();
                }

                try {
                    var cachedEntry = window.__mauiBlobDownloads && window.__mauiBlobDownloads[blobUrl];
                    if (cachedEntry) {
                        if (cachedEntry.blob) {
                            window.__mauiCurrentBlobContext = cachedEntry.context || {};
                            sendBlob(cachedEntry.blob);
                            return;
                        }

                        sendBlob(cachedEntry);
                        return;
                    }

                    if (window.fetch) {
                        fetch(blobUrl)
                            .then(function (response) { return response.blob(); })
                            .then(sendBlob)
                            .catch(function () { readWithXhr(); });
                        return;
                    }

                    readWithXhr();
                } catch (error) {
                    window.MauiBlobDownloader.reportError(String(error));
                }
            })();
            """;

        webView.Post(() => webView.EvaluateJavascript(script, null));
    }

    private static string ResolveFileName(string url, string? contentDisposition, string? mimetype)
    {
        var fileName = URLUtil.GuessFileName(url, contentDisposition, mimetype) ?? "cotizacion.pdf";
        if (!fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            return BlobDownloadBridge.SanitizeFileName(fileName);
        }

        return (mimetype ?? string.Empty).ToLowerInvariant() switch
        {
            "application/pdf" => "cotizacion.pdf",
            "application/zip" => "cotizacion.zip",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "cotizacion.xlsx",
            "application/vnd.ms-excel" => "cotizacion.xls",
            _ => "cotizacion.pdf"
        };
    }

    private static string EscapeJavaScript(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .ReplaceLineEndings("\\n");
}

public sealed class BlobDownloadBridge : Java.Lang.Object
{
    [JavascriptInterface]
    [Export("receiveBlob")]
    public void ReceiveBlob(string base64Data, string mimetype, string fileName, string companyFolder)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64Data);
            var safeFileName = SanitizeFileName(fileName);
            var safeCompanyFolder = SanitizeFolderName(companyFolder);
            var fileUri = SaveToDownloads(bytes, mimetype, safeFileName, safeCompanyFolder);
            PortalDownloadNotification.ShowDownloadCompleted(safeFileName, safeCompanyFolder, mimetype, fileUri);
            ShowToast("Cotización guardada en Descargas.");
        }
        catch (Exception)
        {
            ShowToast("No fue posible guardar la cotización.");
        }
    }

    [JavascriptInterface]
    [Export("reportError")]
    public void ReportError(string message)
    {
        global::Android.Util.Log.Warn("MauiBlobDownloader", message);
        ShowToast("No fue posible leer el archivo del portal.");
    }

    public static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidCharacters, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "cotizacion.pdf" : sanitized;
    }

    public static string SanitizeFolderName(string folderName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", folderName.Split(invalidCharacters, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .Trim('.');

        return string.IsNullOrWhiteSpace(sanitized) ? "sin-banco" : sanitized;
    }

    public static void ShowToast(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
            global::Android.Widget.Toast
                .MakeText(global::Android.App.Application.Context, message, global::Android.Widget.ToastLength.Long)
                ?.Show());
    }

    private static global::Android.Net.Uri SaveToDownloads(
        byte[] bytes,
        string mimetype,
        string fileName,
        string companyFolder)
    {
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Q)
        {
#pragma warning disable CA1416
            return SaveToDownloadsWithMediaStore(bytes, mimetype, fileName, companyFolder);
#pragma warning restore CA1416
        }

        var downloadsDirectory = global::Android.OS.Environment
            .GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)
            ?.AbsolutePath;

        if (string.IsNullOrWhiteSpace(downloadsDirectory))
        {
            throw new InvalidOperationException("Downloads directory is not available.");
        }

        var companyDirectory = Path.Combine(downloadsDirectory, "Serious Seguros", companyFolder);
        Directory.CreateDirectory(companyDirectory);
        var destinationPath = Path.Combine(companyDirectory, fileName);
        File.WriteAllBytes(destinationPath, bytes);

        return AndroidX.Core.Content.FileProvider.GetUriForFile(
            global::Android.App.Application.Context,
            string.Concat(global::Android.App.Application.Context.PackageName, ".fileprovider"),
            new Java.IO.File(destinationPath));
    }

    [SupportedOSPlatform("android29.0")]
    private static global::Android.Net.Uri SaveToDownloadsWithMediaStore(
        byte[] bytes,
        string mimetype,
        string fileName,
        string companyFolder)
    {
        var context = global::Android.App.Application.Context;
        var resolver = context.ContentResolver ??
            throw new InvalidOperationException("Content resolver is not available.");
        using var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
        values.Put(MediaStore.IMediaColumns.MimeType, string.IsNullOrWhiteSpace(mimetype)
            ? "application/octet-stream"
            : mimetype);
        values.Put(
            MediaStore.IMediaColumns.RelativePath,
            string.Concat(global::Android.OS.Environment.DirectoryDownloads, "/Serious Seguros/", companyFolder));
        values.Put(MediaStore.IMediaColumns.IsPending, 1);

        var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values) ??
            throw new InvalidOperationException("Unable to create download entry.");

        using (var output = resolver.OpenOutputStream(uri) ??
            throw new InvalidOperationException("Unable to open download stream."))
        {
            output.Write(bytes, 0, bytes.Length);
        }

        values.Clear();
        values.Put(MediaStore.IMediaColumns.IsPending, 0);
        resolver.Update(uri, values, null, null);

        return uri;
    }
}

internal static class PortalDownloadNotification
{
    private const string ChannelId = "portal_downloads";
    private const string ChannelName = "Descargas de cotizaciones";

    public static void ShowDownloadCompleted(
        string fileName,
        string companyFolder,
        string mimetype,
        global::Android.Net.Uri fileUri)
    {
        var context = global::Android.App.Application.Context;
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Tiramisu &&
            context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) !=
            global::Android.Content.PM.Permission.Granted)
        {
            return;
        }

        EnsureChannel(context);

        using var intent = new Intent(Intent.ActionView);
        intent.SetDataAndType(fileUri, ResolveMimeType(mimetype, fileName));
        intent.ClipData = ClipData.NewUri(context.ContentResolver, fileName, fileUri);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);

        var pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
        {
            pendingIntentFlags |= PendingIntentFlags.Immutable;
        }

        using var pendingIntent = PendingIntent.GetActivity(
            context,
            fileName.GetHashCode(StringComparison.Ordinal),
            intent,
            pendingIntentFlags);

        var notification = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(Resource.Drawable.ic_stat_seriouseguros)
            .SetContentTitle("Cotización descargada")
            .SetContentText(fileName)
            .SetStyle(new NotificationCompat.BigTextStyle()
                .BigText($"{fileName}\nGuardado en Descargas/Serious Seguros/{companyFolder}"))
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault)
            .Build();

        NotificationManagerCompat.From(context)
            .Notify(Math.Abs(fileName.GetHashCode(StringComparison.Ordinal)), notification);
    }

    private static void EnsureChannel(Context context)
    {
        if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.O)
        {
            return;
        }

        var notificationManager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (notificationManager?.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        using var channel = new NotificationChannel(
            ChannelId,
            ChannelName,
            NotificationImportance.Default)
        {
            Description = "Notificaciones cuando una cotización se guarda en Descargas."
        };

        notificationManager?.CreateNotificationChannel(channel);
    }

    private static string ResolveMimeType(string mimetype, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(mimetype) &&
            !string.Equals(mimetype, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return mimetype;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/octet-stream"
        };
    }
}
