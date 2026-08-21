#!/bin/sh
set -eu

dotnet Billing.WebApi.dll --migrate
dotnet Billing.WebApi.dll --ensure-issuer
exec dotnet Billing.WebApi.dll
