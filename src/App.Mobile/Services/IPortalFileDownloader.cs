namespace App.Mobile.Services;

public interface IPortalFileDownloader
{
    Task DownloadAndShareAsync(Uri downloadUri, CancellationToken cancellationToken = default);
}
