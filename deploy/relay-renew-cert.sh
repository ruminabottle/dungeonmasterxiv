#!/usr/bin/env bash
# Renews the relay's Let's Encrypt certificate and, when it changed, rebuilds relay-certificate.pfx, which the
# relay picks up without a restart. Installed as /usr/local/bin/dmx-relay-renew-cert and run as root by
# dmx-relay-renew-cert.timer. Extra arguments go to `certbot renew` (for example --force-renewal or --dry-run).
set -euo pipefail

domain="${DMX_RELAY_DOMAIN:-relay.ruminabottle.com}"
deploy_dir="${DMX_RELAY_CHECKOUT:-/opt/dungeonmasterxiv}/deploy"
live="/etc/letsencrypt/live/$domain"
pfx="$deploy_dir/relay-certificate.pfx"

# Certbot answers the HTTP-01 challenge itself on port 80. Docker's published port bypasses ufw, so port 80
# is reachable only while this container runs. It does nothing until fewer than 30 days remain.
docker pull --quiet certbot/certbot >/dev/null || echo "Could not pull certbot/certbot; using the local image." >&2
docker run --rm -p 80:80 -v /etc/letsencrypt:/etc/letsencrypt certbot/certbot renew --non-interactive "$@"

# Reads the password the relay is given, from the same .env compose reads. No password means an empty one.
password=""
if [[ -f "$deploy_dir/.env" ]]; then
    password="$(sed -n 's/^DMX_RELAY_CERT_PASSWORD=//p' "$deploy_dir/.env" | tail -n 1)"
    password="${password%\"}"; password="${password#\"}"; password="${password%\'}"; password="${password#\'}"
fi
export DMX_PFX_PASSWORD="$password"

fingerprint_of_pem() { openssl x509 -noout -fingerprint -sha256 -in "$1" | cut -d= -f2; }

# Prints the fingerprint of the certificate inside a .pfx, or nothing if it cannot be read
# (an older .pfx may need OpenSSL's legacy algorithms).
fingerprint_of_pfx() {
    { openssl pkcs12 -in "$1" -clcerts -nokeys -passin env:DMX_PFX_PASSWORD 2>/dev/null \
        || openssl pkcs12 -legacy -in "$1" -clcerts -nokeys -passin env:DMX_PFX_PASSWORD 2>/dev/null; } \
        | openssl x509 -noout -fingerprint -sha256 2>/dev/null | cut -d= -f2 || true
}

fingerprint_served() {
    openssl s_client -connect 127.0.0.1:443 -servername "$domain" </dev/null 2>/dev/null \
        | openssl x509 -noout -fingerprint -sha256 2>/dev/null | cut -d= -f2 || true
}

if [[ ! -f "$pfx" ]]; then
    echo "There is no $pfx to renew. Put the first certificate there by hand (deploy/README.md, step 3)." >&2
    exit 1
fi

wanted="$(fingerprint_of_pem "$live/cert.pem")"
current="$(fingerprint_of_pfx "$pfx")"
if [[ "$wanted" == "$current" ]]; then
    echo "The relay already has the current certificate (expires $(openssl x509 -noout -enddate -in "$live/cert.pem" | cut -d= -f2))."
    exit 0
fi

# Waits up to about a minute for the relay to serve the given fingerprint on 443.
serves() {
    for _ in $(seq 1 30); do
        [[ "$(fingerprint_served)" == "$1" ]] && return 0
        sleep 2
    done
    return 1
}

echo "Installing the renewed certificate (expires $(openssl x509 -noout -enddate -in "$live/cert.pem" | cut -d= -f2))."
staged="$(mktemp)"
previous="$(mktemp)"
trap 'rm -f "$staged" "$previous"' EXIT
openssl pkcs12 -export -in "$live/fullchain.pem" -inkey "$live/privkey.pem" -out "$staged" \
    -passout env:DMX_PFX_PASSWORD

# Writes over the existing file rather than replacing it: compose mounts this one file into the container,
# and only an in-place write is visible there. The file keeps its owner (the container's uid) and mode 600.
# The relay loads it on the next handshake; open sessions keep the certificate they started with.
cp "$pfx" "$previous"
cat "$staged" > "$pfx"

if serves "$wanted"; then
    echo "The relay serves the renewed certificate ($wanted)."
    exit 0
fi

echo "The relay did not serve the renewed certificate within about a minute. See 'docker compose logs relay'." >&2
echo "Writing the previous certificate back; the next run tries again." >&2
cat "$previous" > "$pfx"
serves "$current" && echo "The previous certificate is back in service." >&2
exit 1
