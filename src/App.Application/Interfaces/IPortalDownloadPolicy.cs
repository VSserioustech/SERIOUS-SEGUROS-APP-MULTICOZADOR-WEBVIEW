namespace App.Application.Interfaces;

public interface IPortalDownloadPolicy
{
    bool IsDownload(Uri destination);
}
