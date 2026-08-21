FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Billing.Shared/Billing.Shared.csproj src/Billing.Shared/
COPY src/Billing.Domain/Billing.Domain.csproj src/Billing.Domain/
COPY src/Billing.Application/Billing.Application.csproj src/Billing.Application/
COPY src/Billing.Infrastructure/Billing.Infrastructure.csproj src/Billing.Infrastructure/
COPY src/Billing.WebApi/Billing.WebApi.csproj src/Billing.WebApi/

RUN dotnet restore src/Billing.WebApi/Billing.WebApi.csproj

COPY src/ src/
# Sin --no-restore: evita NETSDK1064 cuando assets.json no coincide tras copiar fuentes.
RUN dotnet publish src/Billing.WebApi/Billing.WebApi.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Chromium nativo (Debian bookworm): Chrome for Testing no publica builds linux-arm64,
# y PuppeteerSharp descarga binarios x86_64 incompatibles en Apple Silicon / Colima.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        gnupg \
        debian-archive-keyring \
        fonts-liberation \
        fonts-dejavu-core \
        fonts-noto-core \
    && cp /usr/share/keyrings/debian-archive-keyring.gpg /etc/apt/keyrings/debian-archive-keyring.gpg \
    && printf '%s\n' \
        'deb [signed-by=/etc/apt/keyrings/debian-archive-keyring.gpg] http://deb.debian.org/debian bookworm main' \
        'deb [signed-by=/etc/apt/keyrings/debian-archive-keyring.gpg] http://deb.debian.org/debian-security bookworm-security main' \
        > /etc/apt/sources.list.d/debian-chromium.list \
    && apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends chromium \
    && rm -f /etc/apt/sources.list.d/debian-chromium.list \
    && rm -rf /var/lib/apt/lists/* \
    && chromium --version

COPY --from=build /app/publish .
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh \
    && mkdir -p /app/storage

ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
