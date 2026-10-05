# Release pipeline: one tag ships the relay and the plugin

Date: 2026-10-04.

## Why

Both deploys are done by hand today, and they drifted:

- **The plugin** is released by hand: tag, Release build, release tool, GitHub pre-release, then a
  `repo.json` PR.
- **The default relay** is redeployed by hand with `docker compose` on the VM.
- **v0.1.7 shipped protocol version 2 against a relay still on version 1.** Every 0.1.7 client was
  refused with `426 Upgrade Required` until the relay was rebuilt. Nothing in either process could
  notice.

The goal: pushing one `v*` tag builds and checks everything, deploys the relay at that tag, proves
the live relay speaks the plugin's protocol, and only then publishes the plugin. A failure at any
step publishes nothing a player can download.

This closes distribution's open question on who cuts a release: CI, on a tag.

## Decisions

### 1. One trigger, one workflow, ordered jobs

- **Trigger.** An annotated tag `v*` pushed to the repository. The tag's message becomes the release
  notes; a tag without a message gets GitHub's generated notes.
- **`.github/workflows/release.yml`** runs four jobs in order. Each needs the one before:
  1. **Check.**
     - The tag must point at a commit on `main`.
     - Dalamud's libraries are fetched from `https://goatcorp.github.io/dalamud-distrib/latest.zip`
       into a directory passed to the build as `DALAMUD_HOME`.
     - `dotnet build DungeonMasterXIV.sln` must report 0 warnings and 0 errors.
     - `dotnet test` must pass every test with none skipped.
     - The plugin is built with `-c Release -p:ReleaseTag=<tag>`.
     - The release tool runs with `--dry-run`. Its refusals (distribution R-7.3a, R-7.4a) stop the
       workflow here.
     - The zip and the built manifest are kept as workflow artefacts for job 4, so job 4 publishes
       exactly what job 1 checked.
  2. **Deploy relay.** SSH to the relay host with the restricted deploy key (decision 2), passing the
     tag. The VM checks out the tag, rebuilds and restarts the container, and reports success only
     once the relay is listening.
  3. **Verify relay.** From the runner, `tools/relay-handshake.sh` checks the live default relay
     (decision 3). Failure stops the workflow: the new relay may be live, but no plugin is published.
  4. **Publish plugin.**
     - Creates the GitHub pre-release for the tag with `latest.zip` attached.
     - Regenerates `repo.json` with `tools/DungeonMasterXIV.Release` from job 1's artefacts.
     - Commits it straight to `main` as the GitHub Actions bot, with the message
       `chore(release): regenerate repo.json for <tag>`.
- **Manual run.** `release.yml` also has a `workflow_dispatch` trigger with a `tag` input and a
  `dry_run` input, which is on by default. A dry run performs Check and Verify only, against the
  relay as it is now, and deploys and publishes nothing.
- **Re-running is safe.** Every job is idempotent for the same tag:
  - the VM checking out an already-deployed tag rebuilds the same image;
  - publishing skips an existing release that already has its asset;
  - a `repo.json` that is already identical is not committed again.
- **Permissions.** The workflow has `contents: write` only, enough for the release and the commit.
  Nothing else.

### 2. The VM side: a deploy user, a forced command, an installed script

- **A `dmx-deploy` user**, not root, in the `docker` group. It owns the relay checkout at
  `/opt/dungeonmasterxiv`, cloned once at setup.
- **The TLS certificate** stays where `deploy/compose.yaml` expects it,
  `/opt/dungeonmasterxiv/deploy/relay-certificate.pfx`. It is never in git or in GitHub. A
  certificate password, if any, is in `/opt/dungeonmasterxiv/deploy/.env`, also outside git.
- **The deploy script.** Its source is `deploy/relay-deploy.sh` in the repository. It is installed by
  copy at `/usr/local/bin/dmx-relay-deploy`, and **the installed copy is what runs**, so a tag cannot
  change the logic that deploys it. Changing the script means reinstalling it. It:
  1. reads the tag from `SSH_ORIGINAL_COMMAND` and refuses anything not matching
     `^v[0-9]+(\.[0-9]+){1,3}$`;
  2. runs `git fetch --tags --force`, then `git checkout --detach <tag>`;
  3. runs `GIT_COMMIT=$(git rev-parse HEAD) docker compose up -d --build` in `deploy/`;
  4. waits up to 60 seconds for the relay container to be running and accepting TCP on 443;
  5. prints the container's last 20 log lines, and exits non-zero on any failure. Compose replaces
     the running container only after a successful build, so a failed build leaves the old relay
     serving.
- **The deploy key** is a new ed25519 key made only for this, never a person's key. Its public half
  is in `/home/dmx-deploy/.ssh/authorized_keys` as
  `restrict,command="/usr/local/bin/dmx-relay-deploy" ssh-ed25519 …`. That rules out a shell, port
  forwarding, agent forwarding and a TTY. A leaked key can redeploy a release tag and nothing else.
- **Setup is a document, `deploy/README.md`:** the one-time steps, run once through the hosting
  provider's console. Root access is the operator's own concern and is not used by the pipeline.

### 3. The relay handshake check

- **`tools/relay-handshake.sh <wss-url>`** reads the protocol version from
  `src/DungeonMasterXIV.Core/Net/ProtocolVersion.cs` (`Current = <n>`), so it is never typed.
- **It passes only when both hold:**
  - a WebSocket upgrade at version `n` answers `101 Switching Protocols`;
  - one at version `n - 1` answers `426 Upgrade Required` with `X-DMX-Protocol-Version: <n>`.

  When `n` is 1, only the first check applies.
- It sends no session traffic: an upgrade request and nothing after it. A person runs the same script
  to check a relay by hand.

### 4. CI on every change

- **`.github/workflows/ci.yml`** runs on pull requests and on pushes to `main`. It does the Check
  job's build and test steps, and never deploys or publishes.
- The release workflow's own `repo.json` commits do not trigger it: GitHub runs no workflows for a
  push made with the workflow's token. They change no code.

### 5. Secrets

Repository secrets:

- **`RELAY_DEPLOY_KEY`:** the deploy key's private half.
- **`RELAY_KNOWN_HOSTS`:** the relay host's pinned key line,
  `91.99.153.235 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHzJACBboiKNUpJai/6e95CSXcSkz6GU9F7YlpmRvEq9`.
  The workflow sets `StrictHostKeyChecking yes`, so a different host is refused.
- **`RELAY_DEPLOY_TARGET`:** `dmx-deploy@91.99.153.235`.

## Spec changes

- **distribution:**
  - the open question on who cuts a release is closed: CI, on a `v*` tag;
  - new **R-7.5 A release ships the relay it needs**, with A-7.11 and A-7.12;
  - R-7.2's "generated as part of the release process" is now a property of that process.
- **product-overview D-2 is unchanged.** The plugin still talks only to a relay. GitHub Actions, the
  deploy SSH connection and the Dalamud download are operator and build infrastructure, never a
  destination of the plugin.

## Out of scope

- Moving to the stable channel (distribution R-7.1).
- Signing or attestation of release artefacts.
- Rolling the relay back automatically. Rolling back means redeploying an earlier tag through
  `workflow_dispatch`.
- Restoring the operator's own root SSH access.
- Relays run by anyone other than the default operator.

## Verification

The smoke-tests-only rule applies: no new C# tests.

- `ci.yml` runs green on the pipeline's own PR.
- **Before the one-time VM setup**, while the relay is still on protocol 1, a manual dry run of
  `release.yml` is recorded. It must fail at Verify, and that proves the guard.
  `tools/relay-handshake.sh` run by hand against the live relay agrees with it.
- **The VM setup** ends by redeploying v0.1.7 from the new checkout, after which the handshake
  passes.
- **The first real release, `v0.1.8`:**
  - its run is recorded with all four jobs green;
  - the handshake passes at version 2;
  - the GitHub pre-release has `latest.zip`;
  - `repo.json` on `main` names 0.1.8.0;
  - a 0.1.8 client connects in game.
