using App.Application.Models;

namespace App.Application.Interfaces;

public interface IWebPortalNavigationPolicy
{
    PortalNavigationTarget Evaluate(Uri destination);
}
