#!/usr/bin/env bash
# Checks that a relay accepts the plugin's protocol version and refuses the one before it.
# Usage: tools/relay-handshake.sh <wss-url> [ProtocolVersion.cs]
#   e.g. tools/relay-handshake.sh wss://relay.ruminabottle.com/session
# The version is read from the given ProtocolVersion.cs, or from this checkout's when none is given.
set -euo pipefail

url="${1:?usage: relay-handshake.sh <wss-url> [ProtocolVersion.cs]}"
source_file="${2:-$(dirname "$0")/../src/DungeonMasterXIV.Core/Net/ProtocolVersion.cs}"

version="$(grep -E 'public const int Current = [0-9]+;' "$source_file" | grep -oE '[0-9]+' | head -n 1)"
if [[ -z "$version" ]]; then
    echo "Could not read ProtocolVersion.Current from $source_file." >&2
    exit 2
fi

https_url="https://${url#wss://}"

# Sends one WebSocket upgrade at the given version and prints the status code and the relay's stated version.
# An accepted upgrade holds the connection open, so the request always ends at --max-time; curl still reports.
probe() {
    local headers status stated
    headers="$(mktemp)"
    status="$(curl --silent --max-time 3 --http1.1 --dump-header "$headers" --output /dev/null \
        --write-out '%{http_code}' \
        -H 'Connection: Upgrade' -H 'Upgrade: websocket' -H 'Sec-WebSocket-Version: 13' \
        -H 'Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==' -H "X-DMX-Protocol-Version: $1" \
        "${https_url}?v=$1" 2>/dev/null || true)"
    stated="$(tr -d '\r' <"$headers" | grep -i '^X-DMX-Protocol-Version:' | grep -oE '[0-9]+' | head -n 1 || true)"
    rm -f "$headers"
    echo "${status:-000} ${stated:-none}"
}

# A relay that has just restarted may not answer yet, so the accepting probe is retried before failing.
tries="${RELAY_HANDSHAKE_TRIES:-10}"
for attempt in $(seq 1 "$tries"); do
    read -r status stated <<<"$(probe "$version")"
    [[ "$status" == "101" ]] && break
    (( attempt < tries )) && sleep 3
done
if [[ "$status" != "101" ]]; then
    echo "FAIL: the relay at $url did not accept protocol $version after $tries tries (status $status, relay says ${stated})." >&2
    exit 1
fi
echo "ok: protocol $version accepted (101)."

if (( version > 1 )); then
    previous=$((version - 1))
    read -r status stated <<<"$(probe "$previous")"
    if [[ "$status" != "426" || "$stated" != "$version" ]]; then
        echo "FAIL: protocol $previous should get 426 naming $version; got status $status, relay says $stated." >&2
        exit 1
    fi
    echo "ok: protocol $previous refused (426, relay speaks $version)."
fi
