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

# Ready means the relay itself answers HTTPS on two polls in a row. An open port alone is not enough:
# Docker's proxy accepts connections before the relay binds. Logs stay on the VM; they carry session codes.
answered=0
for _ in $(seq 1 30); do
    code="$(curl --silent --insecure --output /dev/null --max-time 3 --write-out '%{http_code}' \
        https://127.0.0.1/session || true)"
    if docker compose ps --status running --services | grep -qx relay && [[ "$code" != "000" ]]; then
        answered=$((answered + 1))
        if (( answered >= 2 )); then
            echo "Relay $tag is answering on 443 (HTTP $code)."
            docker inspect -f '{{.State.Status}} revision={{index .Config.Labels "org.opencontainers.image.revision"}}' \
                "$(docker compose ps -q relay)"
            exit 0
        fi
    else
        answered=0
    fi
    sleep 2
done

echo "Relay $tag did not answer on 443 within about two minutes. See 'docker compose logs relay' on the VM." >&2
exit 1
