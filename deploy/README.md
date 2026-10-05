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

3. **Move the certificate** (and its password file, if there is one) next to the new `compose.yaml`,
   keeping their modes:

   ```bash
   cp -p <old checkout>/deploy/relay-certificate.pfx /opt/dungeonmasterxiv/deploy/
   [ -f <old checkout>/deploy/.env ] && cp -p <old checkout>/deploy/.env /opt/dungeonmasterxiv/deploy/
   chown dmx-deploy: /opt/dungeonmasterxiv/deploy/relay-certificate.pfx /opt/dungeonmasterxiv/deploy/.env 2>/dev/null || true
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
   ssh-keygen -F 91.99.153.235 | grep -v '^#' | gh secret set RELAY_KNOWN_HOSTS --repo ruminabottle/dungeonmasterxiv
   gh secret set RELAY_DEPLOY_TARGET --repo ruminabottle/dungeonmasterxiv --body 'dmx-deploy@91.99.153.235'
   ```

   Keep `~/dmx-relay-deploy` until step 9 passes, then `rm ~/dmx-relay-deploy ~/dmx-relay-deploy.pub`.

9. **Check the key can deploy and do nothing else:**

   ```bash
   ssh -i ~/dmx-relay-deploy -o IdentitiesOnly=yes dmx-deploy@91.99.153.235 v0.1.7   # redeploys v0.1.7
   ssh -i ~/dmx-relay-deploy -o IdentitiesOnly=yes dmx-deploy@91.99.153.235 bash     # Refused: 'bash' is not a release tag
   tools/relay-handshake.sh wss://relay.ruminabottle.com/session                      # ok: protocol 2 accepted
   ```

## Changing the deploy script

`relay-deploy.sh` in the repository is the source; the VM runs the copy in `/usr/local/bin`. After
changing it, reinstall on the VM as root: `git -C /opt/dungeonmasterxiv pull` (or check out the tag),
then repeat step 4.
