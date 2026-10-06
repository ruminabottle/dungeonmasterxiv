# Relay deployment

The default relay runs from `compose.yaml` on one VM. Releases deploy it automatically: a `v*` tag runs
`.github/workflows/release.yml`, which SSHes to the VM with a deploy key that can only run
`relay-deploy.sh` for a release tag (spec: `docs/superpowers/specs/2026-10-04-release-pipeline-design.md`).

## One-time setup

Run steps 1–6 on the VM as root (the hosting provider's console works), and steps 7–9 on your own
machine. Replace `<old checkout>` with the folder the relay currently runs from. If you don't know it:
`docker inspect -f '{{index .Config.Labels "com.docker.compose.project.working_dir"}}' $(docker ps -q)`
prints its `deploy` folder.

1. **Create the deploy user**, in the `docker` group:

   ```bash
   useradd --create-home --shell /bin/bash dmx-deploy
   usermod -aG docker dmx-deploy
   ```

2. **Clone the repository** where the deploy script expects it:

   ```bash
   git clone https://github.com/ruminabottle/dungeonmasterxiv.git /opt/dungeonmasterxiv
   chown -R dmx-deploy: /opt/dungeonmasterxiv
   ```

3. **Copy the certificate** (and its password file, if there is one) next to the new `compose.yaml`.
   `cp -p` keeps the owner and mode. The certificate must stay owned by the container's user
   (uid 1654, the image's `$APP_UID`) with mode `600`, or the relay cannot read it. Do not `chown`
   it to `dmx-deploy`.

   ```bash
   cp -p <old checkout>/deploy/relay-certificate.pfx /opt/dungeonmasterxiv/deploy/
   [ -f <old checkout>/deploy/.env ] && cp -p <old checkout>/deploy/.env /opt/dungeonmasterxiv/deploy/
   ls -l /opt/dungeonmasterxiv/deploy/relay-certificate.pfx   # -rw------- 1 1654 1654 …
   ```

4. **Install the deploy script.** The installed copy is what runs, so a tag cannot change it:

   ```bash
   install -m 755 /opt/dungeonmasterxiv/deploy/relay-deploy.sh /usr/local/bin/dmx-relay-deploy
   ```

5. **Stop the old stack**, so the new checkout owns port 443:

   ```bash
   (cd <old checkout>/deploy && docker compose down)
   ```

6. **Deploy the current release once by hand.** This brings the relay up from the new checkout:

   ```bash
   sudo -u dmx-deploy SSH_ORIGINAL_COMMAND=v0.1.7 /usr/local/bin/dmx-relay-deploy
   ```

7. **On your machine, make the deploy key.** It has no passphrase and is used by CI only:

   ```bash
   ssh-keygen -t ed25519 -N '' -C dmx-relay-deploy -f ~/dmx-relay-deploy
   cat ~/dmx-relay-deploy.pub
   ```

   **On the VM**, as root, add that public key restricted to the deploy script (paste the key where
   `<public key>` is):

   ```bash
   install -d -m 700 -o dmx-deploy -g dmx-deploy /home/dmx-deploy/.ssh
   echo 'restrict,command="/usr/local/bin/dmx-relay-deploy" <public key>' >> /home/dmx-deploy/.ssh/authorized_keys
   chown dmx-deploy: /home/dmx-deploy/.ssh/authorized_keys
   chmod 600 /home/dmx-deploy/.ssh/authorized_keys
   ```

8. **Store the secrets in GitHub**, then delete the private key from your machine:

   ```bash
   gh secret set RELAY_DEPLOY_KEY --repo ruminabottle/dungeonmasterxiv < ~/dmx-relay-deploy
   gh secret set RELAY_KNOWN_HOSTS --repo ruminabottle/dungeonmasterxiv \
     --body '91.99.153.235 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHzJACBboiKNUpJai/6e95CSXcSkz6GU9F7YlpmRvEq9'
   gh secret set RELAY_DEPLOY_TARGET --repo ruminabottle/dungeonmasterxiv --body 'dmx-deploy@91.99.153.235'
   ```

   That host key line is the one the spec pins. Before trusting it, check it on the VM's console:
   `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` must print the same fingerprint as
   `echo '91.99.153.235 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHzJACBboiKNUpJai/6e95CSXcSkz6GU9F7YlpmRvEq9' | ssh-keygen -lf -`
   on your machine. If they differ, set the secret to the line `ssh-keyscan -t ed25519 91.99.153.235`
   prints, after confirming its fingerprint the same way. `gh secret list` must show all three secrets.

   Keep `~/dmx-relay-deploy` until step 9 passes, then `rm ~/dmx-relay-deploy ~/dmx-relay-deploy.pub`.

9. **Check the key can deploy and do nothing else:**

   ```bash
   ssh -i ~/dmx-relay-deploy -o IdentitiesOnly=yes dmx-deploy@91.99.153.235 v0.1.7   # redeploys v0.1.7
   ssh -i ~/dmx-relay-deploy -o IdentitiesOnly=yes dmx-deploy@91.99.153.235 bash     # Refused: 'bash' is not a release tag
   tools/relay-handshake.sh wss://relay.ruminabottle.com/session                      # ok: protocol 2 accepted
   ```

## Changing the deploy script

`relay-deploy.sh` in the repository is the source; the VM runs the copy in `/usr/local/bin`. The
checkout is left on a detached tag after every deploy, so install the new copy from `origin/main`
instead of pulling. On the VM, as root:

```bash
sudo -u dmx-deploy git -C /opt/dungeonmasterxiv fetch --quiet origin
sudo -u dmx-deploy git -C /opt/dungeonmasterxiv show origin/main:deploy/relay-deploy.sh > /tmp/dmx-relay-deploy
install -m 755 /tmp/dmx-relay-deploy /usr/local/bin/dmx-relay-deploy && rm /tmp/dmx-relay-deploy
```

## Certificate renewal

The certificate is from Let's Encrypt and lasts 90 days. `relay-renew-cert.sh` runs daily at 10:00 UTC from
a systemd timer. It runs `certbot renew` from the `certbot/certbot` image, which answers the HTTP-01
challenge on port 80 itself: Docker's published port bypasses ufw, so port 80 is open only while certbot
runs. Certbot renews in the last 30 days and does nothing before then.

When the certificate changed, the script rebuilds `relay-certificate.pfx` from `/etc/letsencrypt/live` and
writes it over the existing file, so it keeps its owner and mode. The relay loads the new file on the next
handshake, without a restart: sessions already connected keep the certificate they started with, and new
connections and reconnects get the renewed one. The script then checks the relay serves the new
certificate, and writes the old file back if it does not.

The weekly `Relay certificate` workflow checks the served certificate from outside and fails when fewer
than 21 days remain, which means renewal is broken. GitHub emails that failure. (GitHub turns scheduled
workflows off after 60 days without a commit; re-enable it in the Actions tab if that happens.)

**Setup**, once, on the VM as root, after the relay runs v0.1.10 or later (earlier relays only load the
certificate at startup):

1. Install the script and its units from `origin/main`:

   ```bash
   sudo -u dmx-deploy git -C /opt/dungeonmasterxiv fetch --quiet origin
   for f in relay-renew-cert.sh dmx-relay-renew-cert.service dmx-relay-renew-cert.timer; do
       sudo -u dmx-deploy git -C /opt/dungeonmasterxiv show "origin/main:deploy/$f" > "/tmp/$f"
   done
   install -m 755 /tmp/relay-renew-cert.sh /usr/local/bin/dmx-relay-renew-cert
   install -m 644 /tmp/dmx-relay-renew-cert.service /tmp/dmx-relay-renew-cert.timer /etc/systemd/system/
   rm /tmp/relay-renew-cert.sh /tmp/dmx-relay-renew-cert.service /tmp/dmx-relay-renew-cert.timer
   systemctl daemon-reload
   ```

2. **Prove the challenge works** against Let's Encrypt's staging server. This changes nothing:

   ```bash
   dmx-relay-renew-cert --dry-run   # ends with: The relay already has the current certificate (…)
   ```

   If it ends with "The relay serves the renewed certificate" instead, the old `.pfx` was in a format the
   script could not read, and it has rewritten it with the same certificate. That is fine.

3. **Prove the handover** with a real renewal, while a client is connected to the relay:

   ```bash
   dmx-relay-renew-cert --force-renewal   # ends with: The relay serves the renewed certificate (…)
   ```

   The connected client must stay connected, and `tools/relay-handshake.sh wss://relay.ruminabottle.com/session`
   must still pass. Use a real client: the relay closes a connection that does not answer its pings after
   about two minutes, so a bare `curl` upgrade drops then whether or not a renewal happened.

4. **Turn on the timer:**

   ```bash
   systemctl enable --now dmx-relay-renew-cert.timer
   systemctl list-timers dmx-relay-renew-cert.timer   # NEXT is the coming 10:00 UTC
   ```

A run's output is in `journalctl -u dmx-relay-renew-cert`. To change the script or units later, repeat
step 1.
