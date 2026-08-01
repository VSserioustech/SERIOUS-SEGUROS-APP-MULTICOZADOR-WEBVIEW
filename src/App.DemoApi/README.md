# App.DemoApi

API demo para simular el endpoint de whitelabel por tenant/empresa.

No guardar credenciales en este proyecto. Las credenciales de QA se usan solo para pruebas manuales.

## Ejecutar

```powershell
dotnet run --project src\App.DemoApi\App.DemoApi.csproj
```

URLs locales:

- `https://localhost:7144`
- `http://localhost:5144`

## Endpoints

Lista tenants/empresas disponibles:

```http
GET /api/mobile/whitelabel
```

Buscar por empresa:

```http
GET /api/mobile/whitelabel/{empresaId}
```

Resolver por tenant/empresa con fallback default:

```http
GET /api/mobile/whitelabel/resolve?tenantId=serioustech&empresaId=ali
```

## Empresas demo

| EmpresaId | NombreAplicacion | URL |
| --- | --- | --- |
| `serious` | Serious Seguros Portal | `https://portal-qa.seriouseguros.com.mx/` |
| `ali` | Ali Asociados | `https://ali-qa.seriouseguros.com.mx/` |
| `cbe` | CBE | `https://cbe-qa.seriouseguros.com.mx/` |

## Contrato principal

```json
{
  "TenantId": "serioustech",
  "EmpresaId": "ali",
  "Url": "https://ali-qa.seriouseguros.com.mx/",
  "Version": "1.0.0",
  "NombreAplicacion": "Ali Asociados",
  "LauncherIconKey": "ali",
  "LogoUrl": "https://ali-qa.seriouseguros.com.mx/brand/serioustech-mark.svg",
  "PrimaryColor": "#3B0764",
  "SecondaryColor": "#F59E0B",
  "IsDefault": false
}
```

Nota Android: `LauncherIconKey` solo puede activar iconos que esten precompilados en el APK mediante `activity-alias`.
