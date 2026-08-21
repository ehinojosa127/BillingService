# Facturación electrónica SUNAT — BillingService

Microservicio .NET 10. En Docker forma parte del stack con `erp-api` y `erp-front`.

## Desarrollo local

```bash
cp .env.example .env
# completar DB_* y SUNAT_*
dotnet run --project src/Billing.WebApi/Billing.WebApi.csproj
```

HTTP: `http://localhost:5147` · Swagger · `GET /health`

## Docker (stack completo)

Ver **[stack/README.md](stack/README.md)**: clonar los 3 repos como hermanos, copiar compose + envs, `docker-compose up -d --build`.

Al arrancar el contenedor:

1. Migraciones EF (`--migrate`)
2. Emisor + series por defecto (`--ensure-issuer`) — demo si no hay `SUNAT_RUC`
3. API Kestrel en `:8080`

Certificado: montar `certs/sunat-beta-signing.pfx` (no se versiona).

## Variables importantes

| Variable | Uso |
|---|---|
| `SUNAT_ENVIRONMENT` | `beta` o `production` |
| `SUNAT_RUC` / `SUNAT_SOL_*` | Credenciales SOL |
| `SUNAT_CERTIFICATE_*` | Firma XML |
| `ISSUER_*` | Datos del emisor (defaults demo si vacíos) |
| `SERVICE_API_KEY` | API key s2s (vacío = abierta en dev) |

## Emisor y series

`PUT /api/v1/issuer` · `POST /api/v1/series`  
Bootstrap automático: series `B001` (03) y `F001` (01).
