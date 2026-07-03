# Serious Seguros Portal WebView

Aplicación multiplataforma .NET 10 MAUI que integra el portal web de Serious
Seguros en un contenedor nativo:

- Android: `android.webkit.WebView`.
- iOS y MacCatalyst: `WKWebView` (WebKit).
- Windows: WebView2.

La solución conserva las capas definidas en la guía del proyecto:
`App.Mobile`, `App.Application`, `App.Domain`, `App.Infrastructure`,
`App.Contracts` y `App.Tests`.

## Configuración

La URL inicial y los hosts permitidos viven en
`src/App.Mobile/Configuration/appsettings.json`. El ambiente se selecciona con
`APP_ENVIRONMENT` (`Development`, `QA` o `Production`).

Las credenciales del usuario no se almacenan ni se compilan en la aplicación.
La autenticación se realiza directamente en el portal HTTPS y la sesión queda
administrada por el motor WebView de cada plataforma.

## Compilar y probar

```powershell
dotnet restore SeriousSeguros.WebView.sln
dotnet test src\App.Tests\App.Tests.csproj
dotnet build src\App.Mobile\App.Mobile.csproj -f net10.0-windows10.0.19041.0
dotnet build src\App.Mobile\App.Mobile.csproj -f net10.0-android
```
