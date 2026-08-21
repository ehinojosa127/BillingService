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

# Emisor/series se aseguran en el proceso HTTP (IssuerBootstrapHostedService).
exec dotnet Billing.WebApi.dll
