# Stack Docker (carpeta padre)

Copia estos archivos al directorio que contiene `erp-api`, `erp-front` y `BillingService`:

```bash
# desde la carpeta padre:
cp BillingService/stack/docker-compose.yaml .
cp BillingService/stack/.env.example .env
cp -R BillingService/stack/docker .

cp erp-api/.env.example erp-api/.env
cp BillingService/.env.example BillingService/.env
# erp-front no requiere .env en Docker (VITE_API_BASE_URL=/api en build)

# Secretos mínimos:
#   .env              → POSTGRES_PASSWORD
#   erp-api/.env      → APP_KEY, JWT_SECRET  (php artisan key:generate)
#   BillingService/.env → SUNAT_* (y opcionalmente ISSUER_*)
#   BillingService/certs/sunat-beta-signing.pfx

docker-compose up -d --build
```

Al arrancar, Billing crea automáticamente emisor demo + series `B001`/`F001` si no existen.
Reemplaza `SUNAT_RUC` / `ISSUER_*` antes de emitir a SUNAT real.
