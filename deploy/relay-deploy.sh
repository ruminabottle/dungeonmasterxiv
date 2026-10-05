#!/usr/bin/env bash
# Deploys the relay at one release tag. Installed as /usr/local/bin/dmx-relay-deploy and run as the
# forced command of the CI deploy key, which passes the tag as its only argument (SSH_ORIGINAL_COMMAND).
set -euo pipefail

checkout="${DMX_RELAY_CHECKOUT:-/opt/dungeonmasterxiv}"
tag="${SSH_ORIGINAL_COMMAND:-${1:-}}"

if [[ ! "$tag" =~ ^v[0-9]+(\.[0-9]+){1,3}$ ]]; then
    echo "Refused: '$tag' is not a release tag like v0.1.8." >&2
    exit 2
fi

cd "$checkout"
git fetch --quiet --tags --force origin
git -c advice.detachedHead=false checkout --quiet --detach "refs/tags/$tag"
echo "Checked out $tag at $(git rev-parse --short HEAD)."

cd deploy
GIT_COMMIT="$(git rev-parse HEAD)" docker compose up -d --build --quiet-pull

for _ in $(seq 1 30); do
    if docker compose ps --status running --services | grep -qx relay \
        && timeout 2 bash -c '</dev/tcp/127.0.0.1/443' 2>/dev/null; then
        echo "Relay $tag is running and listening on 443."
        docker compose logs --no-color --tail=20 relay
        exit 0
    fi
    sleep 2
done

echo "Relay $tag did not start listening on 443 within 60 seconds." >&2
docker compose logs --no-color --tail=40 relay >&2 || true
exit 1
