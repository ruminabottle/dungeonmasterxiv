# Release Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One `v*` tag builds and checks everything, deploys the default relay at that tag, proves the live relay speaks the plugin's protocol, and only then publishes the plugin release and `repo.json`.

**Architecture:**
- **Scripts.** Two shell scripts do the work, and each runs the same way by hand:
  - `tools/relay-handshake.sh` checks a relay's protocol;
  - `deploy/relay-deploy.sh` runs on the VM as the forced command of a restricted deploy key.
- **Workflows.** Two GitHub Actions workflows call them:
  - `ci.yml` builds and tests every PR and every push to `main`;
  - `release.yml` runs Check → Deploy relay → Verify relay → Publish on a tag.
- **Shared steps.** A composite action holds the steps both workflows share. One-time VM setup is a document.

**Tech Stack:** Bash, GitHub Actions, Docker Compose, the existing .NET 10 build, and the release tool.

**Spec:** `docs/superpowers/specs/2026-10-04-release-pipeline-design.md` (and distribution R-7.5).

**How this plan was checked:** every file below was written and checked before the plan:
- **Shell:** both scripts pass shellcheck 0.11.0.
- **Workflows:** both pass actionlint 1.7.12, which also runs shellcheck over every `run:` block.
- **`relay-handshake.sh`, run against the live relay:**
  - with `ProtocolVersion.Current = 2` (the real source), it fails: "did not accept protocol 2 (status 426, relay says 1)". That's correct, because the relay is still on protocol 1;
  - against a copy of the source claiming protocol 1, it passes: "ok: protocol 1 accepted (101)";
  - the same two results hold when the `ProtocolVersion.cs` path is passed as the second argument.
- **`relay-deploy.sh`,** run against a throwaway git repo with stub `docker` and `timeout` commands:
  - it refuses `v0.1.8; rm -rf /` and an empty tag with exit 2;
  - it checks out `v0.1.8` and reports success.
- **The Check job, simulated against `v0.1.7`:** worktree outside the workspace, tag-on-main check, 0 warnings, 3 test assemblies passed with no skips, the tagged Release build, and the release tool dry run all pass. So a dry run of `v0.1.7` reaches Verify, which then fails against today's relay as intended.
- **Building against goatcorp's Dalamud zip:** `DALAMUD_HOME=<unzipped dalamud-distrib/latest.zip> dotnet build DungeonMasterXIV.sln -warnaserror` gives 0 warnings and 0 errors, the tests pass 1 / 6 / 7 with 0 skipped, and the tagged Release build produces `latest.zip`.
- **Not checked yet:** the workflows themselves have not run, because GitHub runs them. Task 3's PR runs `ci.yml`, and Task 6 runs `release.yml`.

## Global Constraints

- **Smoke tests only, pre-release.** No new C# tests, and no tests of tooling.
- **The plugin's network rule (product-overview D-2) is untouched.** Nothing here changes plugin code.
- **Secrets never appear in the repository or in workflow logs.** The deploy key, known-hosts line and target come only from repository secrets `RELAY_DEPLOY_KEY`, `RELAY_KNOWN_HOSTS` and `RELAY_DEPLOY_TARGET`.
- **Untrusted values are never interpolated into a shell.** Workflow inputs and refs reach `run:` blocks only as environment variables (`$TAG`), never as `${{ … }}` inside a script.
- **The installed deploy script is what runs on the VM.** Nothing in a tag changes the deploy logic.
- **The workflow's token permission is `contents: write` only.**
- **Commit messages** use `git commit -F -` with a quoted heredoc and end with a `Co-Authored-By:` line naming the model that wrote the commit.

## Review Focus

These are the inputs most likely to hurt someone. The smoke-tests-only rule forbids adding tests for them, so each is checked by reading the code, and by Task 6's runs where it can be:

1. **A malicious or malformed tag reaching the VM** (for example `v1; rm -rf /`, or an empty string): `relay-deploy.sh` must refuse it before running any git or docker command. *Owner: Task 2. It's covered by that task's stub test.*
2. **A relay that's up but still on the old protocol:** the workflow must stop before publishing. *Owner: Tasks 1 and 4. Task 6's dry run demonstrates it, which is A-7.11.*
3. **A truncated or skipped test run that prints "Passed!"** must fail the build. *Owner: Task 3, which requires 3 passing assemblies and fails any skip.*
4. **Re-running a release that partly succeeded** must neither duplicate the release nor make an empty `repo.json` commit. *Owner: Task 4's publish steps.*
5. **A tag that isn't on `main`** must not deploy. *Owner: Task 4's Check job.*

---

### Task 1: The relay handshake check

**Files:** create `tools/relay-handshake.sh` (executable).

**Interfaces:**
- Produces: `tools/relay-handshake.sh <wss-url> [ProtocolVersion.cs]`. It reads the version from the given file, or from its own checkout's `src/DungeonMasterXIV.Core/Net/ProtocolVersion.cs` when none is given. It exits 0 when protocol `Current` gets `101` and `Current-1` gets `426` naming `Current`. It exits 1 on a failed check, and 2 if the version can't be read.

- [ ] **Step 1: Write the script**

`tools/relay-handshake.sh` (new file, complete):

```bash
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

read -r status _ <<<"$(probe "$version")"
if [[ "$status" != "101" ]]; then
    read -r _ stated <<<"$(probe "$version")"
    echo "FAIL: the relay at $url did not accept protocol $version (status $status, relay says ${stated})." >&2
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
```

Then run `chmod +x tools/relay-handshake.sh`.

- [ ] **Step 2: Check it**

Run: `shellcheck tools/relay-handshake.sh` if shellcheck is installed (no output means it passed), then `tools/relay-handshake.sh wss://relay.ruminabottle.com/session; echo "exit=$?"`.

Expected while the live relay is still on protocol 1: `FAIL: the relay at wss://relay.ruminabottle.com/session did not accept protocol 2 (status 426, relay says 1).` and `exit=1`. Once the relay has been redeployed (Task 6), the expected result is two `ok:` lines and `exit=0`.

- [ ] **Step 3: Commit**

```bash
git add tools/relay-handshake.sh
git commit -F - <<'EOF'
feat(tools): a relay handshake check that reads the protocol version from the source

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

---

### Task 2: The VM deploy script

**Files:** create `deploy/relay-deploy.sh` (executable).

**Interfaces:**
- Consumes: `deploy/compose.yaml` (unchanged), a checkout at `$DMX_RELAY_CHECKOUT` (default `/opt/dungeonmasterxiv`).
- Produces: `relay-deploy.sh`, which takes the tag from `SSH_ORIGINAL_COMMAND` or `$1`. It exits 2 for a bad tag, 0 once the relay is listening, and 1 otherwise. It is installed on the VM as `/usr/local/bin/dmx-relay-deploy`.

- [ ] **Step 1: Write the script**

`deploy/relay-deploy.sh` (new file, complete):

```bash
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
```

Then run `chmod +x deploy/relay-deploy.sh`.

- [ ] **Step 2: Test it locally with stubs** (Review Focus 1)

```bash
T=$(mktemp -d); mkdir -p "$T/stub" "$T/origin"
( cd "$T/origin" && git init -q && mkdir deploy && echo x > deploy/compose.yaml && git add . \
  && git -c user.email=t@t -c user.name=t commit -qm init && git tag v0.1.8 )
git clone -q "$T/origin" "$T/checkout"
printf '#!/usr/bin/env bash\n[[ "$*" == *"ps --status running --services"* ]] && echo relay\nexit 0\n' > "$T/stub/docker"
printf '#!/usr/bin/env bash\nexit 0\n' > "$T/stub/timeout"; chmod +x "$T/stub/"*
for tag in 'v0.1.8; rm -rf /' '' 'v0.1.8'; do
  PATH="$T/stub:$PATH" DMX_RELAY_CHECKOUT="$T/checkout" SSH_ORIGINAL_COMMAND="$tag" bash deploy/relay-deploy.sh; echo "exit=$?"
done
rm -rf "$T"
```

Expected, in order:
- `Refused: 'v0.1.8; rm -rf /' is not a release tag like v0.1.8.` and `exit=2`;
- `Refused: '' is not a release tag like v0.1.8.` and `exit=2`;
- `Checked out v0.1.8 at <sha>.`, then `Relay v0.1.8 is running and listening on 443.`, and `exit=0`.

Also run `shellcheck deploy/relay-deploy.sh` if it's installed (no output means it passed).

- [ ] **Step 3: Commit**

```bash
git add deploy/relay-deploy.sh
git commit -F - <<'EOF'
feat(deploy): a relay deploy script that accepts only a release tag

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

---

### Task 3: Shared build-and-test steps, and CI on every change

**Files:** create `.github/actions/build-and-test/action.yml` and `.github/workflows/ci.yml`.

**Interfaces:**
- Produces: the composite action `./.github/actions/build-and-test`, with one input `source-dir` (default `.`) naming the checkout to build. It sets up .NET from that checkout's `global.json`, fetches Dalamud into `$RUNNER_TEMP/dalamud` and exports `DALAMUD_HOME`, builds `DungeonMasterXIV.sln -warnaserror`, and runs the tests, failing unless exactly 3 assemblies pass with no skips. Task 4 uses it.

- [ ] **Step 1: Write the composite action**

`.github/actions/build-and-test/action.yml` (new file, complete):

```yaml
name: Build and test
description: Fetches Dalamud, builds the solution with warnings as errors, and runs every test with none skipped.

inputs:
  source-dir:
    description: The checkout to build and test.
    default: .

runs:
  using: composite
  steps:
    - uses: actions/setup-dotnet@v4
      with:
        global-json-file: ${{ inputs.source-dir }}/global.json

    - name: Fetch Dalamud's libraries
      shell: bash
      run: |
        mkdir -p "$RUNNER_TEMP/dalamud"
        curl --silent --show-error --fail --location -o "$RUNNER_TEMP/dalamud.zip" \
          https://goatcorp.github.io/dalamud-distrib/latest.zip
        unzip -q "$RUNNER_TEMP/dalamud.zip" -d "$RUNNER_TEMP/dalamud"
        echo "DALAMUD_HOME=$RUNNER_TEMP/dalamud" >> "$GITHUB_ENV"

    - name: Build
      shell: bash
      working-directory: ${{ inputs.source-dir }}
      run: dotnet build DungeonMasterXIV.sln -warnaserror

    # dotnet test prints "Passed!" for a truncated run, so every assembly must report and none may skip.
    - name: Test
      shell: bash
      working-directory: ${{ inputs.source-dir }}
      run: |
        set -o pipefail
        dotnet test DungeonMasterXIV.sln --no-build 2>&1 | tee "$RUNNER_TEMP/test.log"
        passed="$(grep -c '^Passed!' "$RUNNER_TEMP/test.log" || true)"
        if [[ "$passed" != "3" ]]; then
          echo "::error::Expected 3 test assemblies to pass, saw $passed."
          exit 1
        fi
        if grep -qE 'Skipped: +[1-9]' "$RUNNER_TEMP/test.log"; then
          echo "::error::A test was skipped."
          exit 1
        fi
```

- [ ] **Step 2: Write `ci.yml`**

`.github/workflows/ci.yml` (new file, complete):

```yaml
# Builds and tests every pull request and every push to main. Never deploys or publishes.
name: CI

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/build-and-test
```

- [ ] **Step 3: Lint**

Run: `actionlint .github/workflows/ci.yml` if it's installed (no output means it passed). Otherwise, the PR's own CI run is the check.

- [ ] **Step 4: Commit**

```bash
git add .github/actions/build-and-test/action.yml .github/workflows/ci.yml
git commit -F - <<'EOF'
ci: build and test every pull request and every push to main

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

The workflow first runs on the PR that carries this plan's commits. Expected: the `CI / build-and-test` check is green, and its Test step log shows 3 `Passed!` lines with `Skipped: 0`.

---

### Task 4: The release workflow

**Files:** create `.github/workflows/release.yml`.

**Interfaces:**
- Consumes:
  - Task 1's `tools/relay-handshake.sh`;
  - Task 3's composite action;
  - `tools/DungeonMasterXIV.Release`, with the flags it already has (`--assembly --plugin-manifest --asset --tag`, plus `--dry-run` or `--out`);
  - the secrets `RELAY_DEPLOY_KEY`, `RELAY_KNOWN_HOSTS` and `RELAY_DEPLOY_TARGET`.
- Produces: the jobs `check`, `deploy-relay`, `verify-relay` and `publish`, and a `workflow_dispatch` trigger with `tag` and `dry_run` (default true) inputs.

- [ ] **Step 1: Write the workflow**

`.github/workflows/release.yml` (new file, complete):

```yaml
# One v* tag: check everything, deploy the default relay at that tag, prove it speaks the plugin's
# protocol, then publish the plugin. A failure at any step publishes nothing (distribution R-7.5).
name: Release

on:
  push:
    tags: ['v*']
  workflow_dispatch:
    inputs:
      tag:
        description: Release tag to run, for example v0.1.8
        required: true
        type: string
      dry_run:
        description: Check and verify only; deploy and publish nothing
        type: boolean
        default: true

permissions:
  contents: write

concurrency:
  group: release
  cancel-in-progress: false

env:
  TAG: ${{ github.event_name == 'push' && github.ref_name || inputs.tag }}
  RELAY_URL: wss://relay.ruminabottle.com/session

jobs:
  # The pipeline's own files come from this workflow's commit; the code it ships comes from the tag,
  # checked out outside the workspace so the plugin's source glob never sees a second copy.
  check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Check out the tag to ship
        run: |
          git fetch --quiet --tags --force origin
          git worktree add --detach "$RUNNER_TEMP/release" "refs/tags/$TAG"
          echo "SRC=$RUNNER_TEMP/release" >> "$GITHUB_ENV"

      - name: The tag is on main
        run: |
          if ! git merge-base --is-ancestor "refs/tags/$TAG" origin/main; then
            echo "::error::$TAG is not on main."
            exit 1
          fi

      - uses: ./.github/actions/build-and-test
        with:
          source-dir: ${{ env.SRC }}

      - name: Build the plugin for the tag
        working-directory: ${{ env.SRC }}
        run: dotnet build DungeonMasterXIV.csproj -c Release -p:ReleaseTag="$TAG" -warnaserror

      - name: Dry-run the release tool
        working-directory: ${{ env.SRC }}
        run: >-
          dotnet run --project tools/DungeonMasterXIV.Release --
          --assembly bin/Release/DungeonMasterXIV.dll
          --plugin-manifest bin/Release/DungeonMasterXIV/DungeonMasterXIV.json
          --asset bin/Release/DungeonMasterXIV/latest.zip
          --tag "$TAG" --dry-run

      - uses: actions/upload-artifact@v4
        with:
          name: plugin
          path: |
            ${{ env.SRC }}/bin/Release/DungeonMasterXIV.dll
            ${{ env.SRC }}/bin/Release/DungeonMasterXIV/DungeonMasterXIV.json
            ${{ env.SRC }}/bin/Release/DungeonMasterXIV/latest.zip
          if-no-files-found: error

  deploy-relay:
    needs: check
    if: github.event_name == 'push' || !inputs.dry_run
    runs-on: ubuntu-latest
    steps:
      - name: Deploy the relay at the tag
        env:
          DEPLOY_KEY: ${{ secrets.RELAY_DEPLOY_KEY }}
          KNOWN_HOSTS: ${{ secrets.RELAY_KNOWN_HOSTS }}
          TARGET: ${{ secrets.RELAY_DEPLOY_TARGET }}
        run: |
          install -m 700 -d ~/.ssh
          printf '%s\n' "$DEPLOY_KEY" > ~/.ssh/relay_deploy
          chmod 600 ~/.ssh/relay_deploy
          printf '%s\n' "$KNOWN_HOSTS" > ~/.ssh/relay_known_hosts
          ssh -i ~/.ssh/relay_deploy -o IdentitiesOnly=yes -o BatchMode=yes \
            -o StrictHostKeyChecking=yes -o UserKnownHostsFile="$HOME/.ssh/relay_known_hosts" \
            "$TARGET" "$TAG"

  verify-relay:
    needs: [check, deploy-relay]
    if: >-
      always() && needs.check.result == 'success'
      && (needs.deploy-relay.result == 'success' || needs.deploy-relay.result == 'skipped')
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: The live relay speaks the tag's protocol
        run: |
          git fetch --quiet --tags --force origin
          git show "refs/tags/$TAG:src/DungeonMasterXIV.Core/Net/ProtocolVersion.cs" > "$RUNNER_TEMP/ProtocolVersion.cs"
          tools/relay-handshake.sh "$RELAY_URL" "$RUNNER_TEMP/ProtocolVersion.cs"

  publish:
    needs: [check, deploy-relay, verify-relay]
    if: github.event_name == 'push' || !inputs.dry_run
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          ref: main
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - uses: actions/download-artifact@v4
        with:
          name: plugin
          path: bin/Release

      - name: Publish the GitHub pre-release
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          git fetch --quiet --tags --force origin
          if gh release view "$TAG" --json assets --jq '.assets[].name' 2>/dev/null | grep -qx latest.zip; then
            echo "$TAG is already released with latest.zip."
            exit 0
          fi
          if gh release view "$TAG" >/dev/null 2>&1; then
            gh release upload "$TAG" bin/Release/DungeonMasterXIV/latest.zip --clobber
            exit 0
          fi
          subject="$(git for-each-ref "refs/tags/$TAG" --format='%(contents:subject)')"
          body="$(git for-each-ref "refs/tags/$TAG" --format='%(contents:body)')"
          if [[ -n "$subject" ]]; then
            printf '%s\n' "$body" > "$RUNNER_TEMP/notes.md"
            gh release create "$TAG" bin/Release/DungeonMasterXIV/latest.zip --prerelease --verify-tag \
              --title "$subject" --notes-file "$RUNNER_TEMP/notes.md"
          else
            gh release create "$TAG" bin/Release/DungeonMasterXIV/latest.zip --prerelease --verify-tag \
              --title "$TAG" --generate-notes
          fi

      - name: Regenerate repo.json
        run: >-
          dotnet run --project tools/DungeonMasterXIV.Release --
          --assembly bin/Release/DungeonMasterXIV.dll
          --plugin-manifest bin/Release/DungeonMasterXIV/DungeonMasterXIV.json
          --asset bin/Release/DungeonMasterXIV/latest.zip
          --tag "$TAG" --out repo.json

      - name: Commit repo.json to main
        run: |
          git add repo.json
          if git diff --cached --quiet; then
            echo "repo.json already describes $TAG."
            exit 0
          fi
          git -c user.name='github-actions[bot]' \
              -c user.email='41898282+github-actions[bot]@users.noreply.github.com' \
              commit --quiet -m "chore(release): regenerate repo.json for $TAG"
          git push origin HEAD:main
```

Notes for the implementer:
- **Two checkouts, on purpose.** The pipeline's own files (the composite action, `relay-handshake.sh`) come from the workflow's commit at the workspace root. The code being shipped comes from the tag, through `git worktree add "$RUNNER_TEMP/release"`. That worktree is outside the workspace, so `DungeonMasterXIV.csproj`'s default source glob never compiles a second copy. This lets a dry run of an older tag (one without the pipeline files) still reach Verify.
- **Verify reads the tag's protocol version** with `git show refs/tags/$TAG:…/ProtocolVersion.cs` and passes it to the script.
- **`publish` has no status function in its `if`,** so it runs only when `check`, `deploy-relay` and `verify-relay` all succeeded. On a dry run, `deploy-relay` is skipped, so `publish` is skipped too.
- **`verify-relay` uses `always()`** so a dry run still checks the live relay.
- **The `repo.json` commit is pushed with the workflow token,** which by GitHub's rules does not trigger `ci.yml` again.

- [ ] **Step 2: Lint**

Run: `actionlint .github/workflows/release.yml` if it's installed (no output means it passed).

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/release.yml
git commit -F - <<'EOF'
ci: one v* tag deploys the relay, checks it, then publishes the plugin

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

---

### Task 5: The one-time VM setup document

**Files:** create `deploy/README.md`.

- [ ] **Step 1: Write the document**

`deploy/README.md` (new file, complete):

````markdown
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
````

- [ ] **Step 2: Commit**

```bash
git add deploy/README.md
git commit -F - <<'EOF'
docs(deploy): one-time relay host setup for the release pipeline

Co-Authored-By: <model> <noreply@anthropic.com>
EOF
```

---

### Task 6: Merge, set up, prove the guard, first release (the person)

This needs the VM console and repository admin, so the person does it and the controller checks each result.

- [ ] **Step 1:** Open the PR with Tasks 1–5, check that `CI / build-and-test` is green, and merge it.
- [ ] **Step 2: Prove the guard (A-7.11)**, while the live relay is still on protocol 1. Run Actions → Release → Run workflow with `tag: v0.1.7` and `dry_run` ticked. Expected: `check` green, `deploy-relay` and `publish` skipped, and `verify-relay` red with `did not accept protocol 2 (status 426, relay says 1)`. Nothing is published.
- [ ] **Step 3:** Follow `deploy/README.md` steps 1–9 on the VM and your own machine.
  - Step 6 of that document redeploys `v0.1.7` from the new checkout, so the live relay moves to protocol 2. That ends the current outage, and 0.1.7 clients connect.
  - The document's step 9 must show the restricted key redeploying `v0.1.7` and refusing `bash`, and the handshake printing two `ok:` lines.
- [ ] **Step 4: First real release.** Run `git tag -a v0.1.8 -m "<subject line>" -m "<notes>" && git push origin v0.1.8`. Expected:
  - all four jobs green;
  - a v0.1.8 pre-release with `latest.zip`;
  - a `chore(release): regenerate repo.json for v0.1.8` commit on `main`;
  - `tools/relay-handshake.sh wss://relay.ruminabottle.com/session` prints two `ok:` lines (A-7.12);
  - Dalamud offers 0.1.8 and it connects in game.
