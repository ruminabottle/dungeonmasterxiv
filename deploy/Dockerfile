# Builds the relay into a deployable image from this repository.

# Pins SDK and runtime tags separately (their repos number tags differently); keep the SDK tag equal to global.json's.
ARG DOTNET_SDK_VERSION=10.0.400
ARG DOTNET_RUNTIME_VERSION=10.0.11

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /build

# Copies the project files and shared build props/targets first, so a source-only change does not re-resolve packages.
COPY Directory.Build.props Directory.Build.targets ./
COPY src/DungeonMasterXIV.Core/DungeonMasterXIV.Core.csproj src/DungeonMasterXIV.Core/
COPY src/DungeonMasterXIV.Relay/DungeonMasterXIV.Relay.csproj src/DungeonMasterXIV.Relay/
RUN dotnet restore src/DungeonMasterXIV.Relay/DungeonMasterXIV.Relay.csproj

COPY src/DungeonMasterXIV.Core/ src/DungeonMasterXIV.Core/
COPY src/DungeonMasterXIV.Relay/ src/DungeonMasterXIV.Relay/
RUN dotnet publish src/DungeonMasterXIV.Relay/DungeonMasterXIV.Relay.csproj \
        --configuration Release \
        --no-restore \
        --output /app

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS runtime

# Stamps the image with the commit it was built from, for traceability.
ARG GIT_COMMIT=unknown
LABEL org.opencontainers.image.title="DungeonMasterXIV relay" \
      org.opencontainers.image.revision="${GIT_COMMIT}" \
      org.opencontainers.image.description="Forwards encrypted session traffic and stores nothing (D-2, A-1.5e)."

# Runs as an unprivileged user; the relay logs to stdout and reads only its TLS certificate file.
USER $APP_UID
WORKDIR /app
COPY --from=build /app .

# Exposes 443 directly, since the client's TLS session must terminate at this process rather than a proxy.
EXPOSE 443
ENV DMX_RELAY_PORT=443 \
    DMX_RELAY_USE_TLS=true

ENTRYPOINT ["dotnet", "DungeonMasterXIV.Relay.dll"]
