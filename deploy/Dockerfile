# The relay, as an image built from this repository.
#
# Why this file exists at all: A-1.5e proves that the relay we BUILD stores nothing, and that says
# nothing about the instance users actually connect to unless what runs there came out of the repo
# rather than being assembled by hand on the box. E-9 withdrew self-hosting as a tested guarantee
# but explicitly did NOT withdraw this artifact — it is the only thing tying the deployed process
# back to a commit, and PRD-1 names the remaining gap rather than claiming this closes it.
#
# There is deliberately no self-hosting documentation here (E-9, and C2's do-not list). This is our
# deployment, not a guide to running your own.

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /build

# Restore against the project files alone first, so a source-only change does not re-resolve
# packages. Directory.Build.props is copied because every project under the repo root imports it.
#
# Directory.Build.targets carries the build-time guards that are not written in any one project —
# NoTlsValidationBypass among them. Left out, the image would build from source the fence had never
# been run over, so the one build that actually ships would be the one build that is unguarded.
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

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime

# Stamped at build time so a running relay can be traced to the commit it was built from. This is
# the mitigation PRD-1 asks for, and it is weaker than a proof — say so rather than implying more.
ARG GIT_COMMIT=unknown
LABEL org.opencontainers.image.title="DungeonMasterXIV relay" \
      org.opencontainers.image.revision="${GIT_COMMIT}" \
      org.opencontainers.image.description="Forwards encrypted session traffic and stores nothing (D-2, A-1.5e)."

# Unprivileged, and it cannot install anything or write outside a tmpfs. The relay needs no
# filesystem access at all: it opens no file, and its log goes to stdout (see RelayLog).
USER $APP_UID
WORKDIR /app
COPY --from=build /app .

# 443 directly. The client's TLS session must terminate against this process — D-2's clarification
# of 2026-08-26 makes a TLS-terminating proxy in front of the relay a network DESTINATION, which is
# approve-blocking. That rules out a CDN, an orange-cloud DNS record, and an ingress that decrypts.
EXPOSE 443
ENV DMX_RELAY_PORT=443 \
    DMX_RELAY_USE_TLS=true

ENTRYPOINT ["dotnet", "DungeonMasterXIV.Relay.dll"]
