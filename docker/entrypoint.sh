#!/bin/sh
set -eu

echo "Waiting for PostgreSQL schema (database 'billing')..."
i=0
until dotnet Billing.WebApi.dll --migrate; do
  i=$((i + 1))
  if [ "$i" -ge 30 ]; then
    echo "Migrations failed after retries." >&2
    exit 1
  fi
  echo "Migrate attempt $i failed (DB may still be initializing). Retrying in 2s..."
  sleep 2
done

if ! dotnet Billing.WebApi.dll --ensure-issuer; then
  echo "WARNING: issuer/series bootstrap failed; API will start anyway." >&2
fi

exec dotnet Billing.WebApi.dll
