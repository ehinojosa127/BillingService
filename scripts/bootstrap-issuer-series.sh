#!/usr/bin/env sh
# Configura emisor + series mínimas en BillingService (idempotente).
# Uso:
#   BILLING_URL=http://localhost:5147 ./scripts/bootstrap-issuer-series.sh
#
# Variables opcionales:
#   ISSUER_RUC | SUNAT_RUC, ISSUER_LEGAL_NAME, ISSUER_TRADE_NAME,
#   ISSUER_ADDRESS_LINE, ISSUER_UBIGEO, ISSUER_DEPARTMENT, ISSUER_PROVINCE,
#   ISSUER_DISTRICT, SERVICE_API_KEY

set -eu

BASE_URL="${BILLING_URL:-http://127.0.0.1:5147}"
API_KEY="${SERVICE_API_KEY:-}"

RUC="${ISSUER_RUC:-${SUNAT_RUC:-}}"
LEGAL_NAME="${ISSUER_LEGAL_NAME:-Confecciones Erika}"
TRADE_NAME="${ISSUER_TRADE_NAME:-$LEGAL_NAME}"
ADDRESS_LINE="${ISSUER_ADDRESS_LINE:-Av. Principal 123}"
UBIGEO="${ISSUER_UBIGEO:-150101}"
DEPARTMENT="${ISSUER_DEPARTMENT:-LIMA}"
PROVINCE="${ISSUER_PROVINCE:-LIMA}"
DISTRICT="${ISSUER_DISTRICT:-LIMA}"

if [ -z "$RUC" ]; then
  echo "Defina ISSUER_RUC o SUNAT_RUC." >&2
  exit 1
fi

HDRS="-H Content-Type: application/json -H Accept: application/json"
if [ -n "$API_KEY" ]; then
  HDRS="$HDRS -H X-Api-Key:$API_KEY"
fi

TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT

python3 - "$RUC" "$LEGAL_NAME" "$TRADE_NAME" "$ADDRESS_LINE" "$UBIGEO" "$DEPARTMENT" "$PROVINCE" "$DISTRICT" <<'PY' >"$TMP"
import json, sys
ruc, legal, trade, line, ubigeo, dep, prov, dist = sys.argv[1:]
print(json.dumps({
    "ruc": ruc,
    "legalName": legal,
    "tradeName": trade,
    "addressLine": line,
    "ubigeo": ubigeo,
    "department": dep,
    "province": prov,
    "district": dist,
    "countryCode": "PE",
    "establishmentCode": "0000",
}))
PY

echo "Upsert issuer ($RUC) → $BASE_URL"
# shellcheck disable=SC2086
curl -fsS -X PUT "$BASE_URL/api/v1/issuer" $HDRS -d @"$TMP"
echo

ensure_series() {
  type="$1"
  series="$2"
  body=$(python3 -c "import json; print(json.dumps({'documentType':'$type','series':'$series'}))")
  code=$(curl -sS -o "$TMP" -w '%{http_code}' -X POST "$BASE_URL/api/v1/series" \
    -H 'Content-Type: application/json' \
    -H 'Accept: application/json' \
    ${API_KEY:+-H "X-Api-Key: $API_KEY"} \
    -d "$body")
  if [ "$code" = "201" ] || [ "$code" = "200" ]; then
    echo "Serie $type/$series creada."
  elif [ "$code" = "409" ]; then
    echo "Serie $type/$series ya existe."
  else
    echo "No se pudo crear serie $type/$series (HTTP $code):" >&2
    cat "$TMP" >&2 || true
    echo >&2
    exit 1
  fi
}

# RUS: boletas (03). F001 queda listo si cambian de régimen.
ensure_series 03 B001
ensure_series 01 F001

echo "OK. Series actuales:"
# shellcheck disable=SC2086
curl -fsS "$BASE_URL/api/v1/series" $HDRS
echo
