# Distribution: product spec

Describes the current product. Converted 2026-09-29 from `PRD-7-distribution.md` and
`policy/relay-service-policy.md`; the originals are archived at
`~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team/product/prd/PRD-7-distribution.md` and
`~/archive/dungeonmasterxiv-agent-team-2026-09-29/claude/team/product/policy/relay-service-policy.md`.

Principles are cited as `product-overview D-n` and are not restated here. The relay operator's
published service policy is **[RELAY-SERVICE-POLICY.md](../../../RELAY-SERVICE-POLICY.md)**; what
the relay can observe has a single source, `session-layer R-1.9`, and is not restated here either.

## Purpose

Dalamud's manifest format carries two parallel sets of fields, not one — a stable set and a testing
set (`AssemblyVersion` / `TestingAssemblyVersion`, `DownloadLinkInstall`+`DownloadLinkUpdate` /
`DownloadLinkTesting`, `DalamudApiLevel` / `TestingDalamudApiLevel`), plus `IsTestingExclusive` and
`IsHide`. A user only receives a testing build if they have opted into testing builds in Dalamud —
that is a gate Dalamud enforces for us. Treating "give the URL only to invited people" as the whole
mechanism is not correct: a URL leaks, gets pasted into a Discord, ends up in a search result.

The manifest format itself is Dalamud's and is not this product's to design. What is this product's:
which channel it publishes on, where the manifest lives, and when a build becomes visible to whom.

## What ships

### R-7.1 We publish on the testing channel only, for now

- The manifest sets **`IsTestingExclusive: true`** (Dalamud declares this field as a `bool`; the
  correct rendering is the unquoted literal `true`, not a quoted string) and populates only the
  testing fields.
- This is the concrete mechanism for `product-overview D-12`, which without it is a policy with no
  enforcement behind it: even someone who finds the URL gets nothing unless they have deliberately
  enabled testing builds.
- **Two gates, and they are independent.** The unadvertised URL is one. The testing flag is the
  other. Neither is sufficient; both are cheap.
- Moving to the stable channel is the act that makes this a release (`product-overview D-12`);
  official listing is not being pursued.

**Acceptance criteria**
- **A-7.1a** A Dalamud client with testing builds enabled, given the manifest URL, is **offered** the
  plugin — not merely able to fetch and parse the file. A manifest that parses cleanly and yields an
  empty repository fails this. This is the only criterion that closes the loop end to end, and it is
  checked in-game.
- **A-7.4** A user who has **not** enabled testing builds sees nothing from this repository. It needs
  a Dalamud install with testing builds disabled — the default, so it is cheap — and it must actually
  be checked, rather than assumed from the manifest flag. This is the criterion that enforces
  `product-overview D-12` most directly, precisely because it tests that nothing happens.
- **A-7.5** A user who has enabled testing builds can add the URL, see Dungeon Master XIV, and
  install it.
- **A-7.8** The manifest that is **actually served** carries `IsTestingExclusive: true`, and a
  change of that field to `false` is detected before it can reach a tester. Asserting it on the
  generated entry does not satisfy this — the committed file is what is served, and it is the
  committed file that must be checked. A green A-7.8 says the flag is set; only A-7.4 says Dalamud
  honoured it — A-7.8 does not weaken or replace A-7.4.

### R-7.2 Where the manifest lives

- Served from the **plugin repository over raw GitHub** — no hosting, no cost, no third-party
  service, and it moves with the repository.
- A vanity subdomain is deliberately not used yet. It is nicer to read aloud and it is more moving
  parts for an invited-testers phase. (See Open questions.)
- The manifest is **generated as part of the release process, not hand-edited.** A hand-maintained
  manifest drifts from the artifact it points at — the same class of defect `product-overview D-14`
  exists to prevent on the wire — and it fails the same way, silently, at the moment a user installs.

**Acceptance criteria**
- **A-7.9** Every field of the served manifest agrees with what the release process would produce for
  the current tag. A hand-edit to any field — not an enumerated subset — is detected. This detects
  the served file drifting from the generator and cannot detect the generator itself being wrong; see
  A-7.10. How the agreement is established is not specified here; this criterion says only that a
  hand-edit to any field must be something that can fail.
- **A-7.10** Every field Dalamud declares as required is present in the served manifest, established
  against Dalamud's own declaration rather than against this product's generator output. A manifest
  the release process produces happily, and Dalamud cannot use, fails this. A criterion that checks
  the product is consistent with itself cannot detect that the product is wrong about the world —
  A-7.9 and A-7.8 are both internal-agreement criteria and are good ones, but they share that blind
  spot, which is why A-7.10 is anchored to Dalamud's own declaration and A-7.1a to the plugin actually
  being offered, neither of which is a fact about the product's own output. A manifest with
  `TestingAssemblyVersion` present and the required `AssemblyVersion` field absent — the two fields
  the wrong way round — is the shape of failure this criterion exists to catch.

### R-7.3 What the manifest says

Fixed by Dalamud, but three fields are product copy:
- `Name`: **Dungeon Master XIV** · `InternalName`: **DungeonMasterXIV** (permanent; `core-skeleton
  R-0.1`).
- `Punchline`: **"Dice, initiative and encounter tracking for in-game tabletop sessions."**
- `Description`: must state that it requires a relay connection and other participants running the
  plugin, because a tracker that does nothing on its own is a bad surprise to discover after
  installing. It must not claim rolls are verified (`rolls A-2.11`) or imply anonymity
  (`product-overview D-8`).
- `RepoUrl` points at the plugin repository. (`IconUrl` needs an icon; see Open questions.)

**Acceptance criteria**
- **A-7.7** The description states the plugin needs a relay and other participants, and makes no
  claim about verified rolls or anonymity.

### R-7.3a The API level is derived, never supplied

`DalamudApiLevel` is the field that, if wrong, makes Dalamud silently never offer the plugin — no
error, nothing written anywhere observable. The release tool exits rather than guessing it.

**It is not a human input at all.** Dalamud.NET.Sdk stamps the correct value into the plugin's own
built manifest at build time, from the Dalamud version the plugin was compiled against. So the
repository manifest **copies it from the built plugin manifest** — never typed, never defaulted,
never carried in a config file someone must remember to bump when Dalamud moves. That makes it
structurally impossible for the two to disagree, which is the same reasoning as R-7.2's "generated,
not hand-edited", the same reasoning as enforcing a rule with a type rather than a review, and one
instance of `product-overview D-16` (a build's API level is one of the values D-16 names).

If the value cannot be read from the built manifest, the release **stops**. The fallback is "the
build did not produce what was expected", a real failure, rather than "a human has not told us a
number".

### R-7.4 A release is a versioned artifact, not a branch

Each published build is a **tagged release with an attached zip**, and the manifest's download links
point at that release asset — never at a branch, never at a moving target.

**Acceptance criteria**
- **A-7.3** Download links point at a tagged release asset, not a branch.

### R-7.4a The advertised version has one author

Follows from `product-overview D-16`: every value that tells a user or Dalamud what a build is has
exactly one place a human authors it, and the tag and the assembly version are not both typed.

- **Exactly one human-authored source** for the version a release advertises. Every other appearance
  of that version — the repository manifest's `TestingAssemblyVersion`, the download link's tag path,
  the built assembly — is **computed** from that one place.
- Which value is authored and which are derived is not specified here; the tag, the `.csproj`
  property, or something else entirely are all permissible — two of them being typed is not.
- **If the advertised version cannot be verified against the artefact, the release stops** rather
  than proceeding — this is release-blocking regardless of exit code, the same exit R-7.3a already
  specifies for the API level, for the same reason.
- The failure mode this guards against is silent: Dalamud does not reject a build advertising a stale
  version, it simply never offers it, with nothing logged. The symptom is a tester who goes quiet.

**Acceptance criteria**
- **A-7.2a** Two releases cut from two different tags advertise two different versions. Cutting a
  release, then a second from a later tag without hand-editing anything, must show the advertised
  `TestingAssemblyVersion` differ between them. **This demonstration asserts distinctness, and
  distinctness alone is not the property wanted:** a build advertising the commit SHA, a build
  timestamp, or a fresh GUID would give two different values from two different tags and pass this
  while advertising something that is not the version at all. The property is that the advertised
  version **is the tag's version**, so the check must compare the advertised value against the tag,
  not the two releases against each other.
- **A-7.2b** A tag that disagrees with the version the artefact reports causes the release to stop,
  rather than producing a manifest. Passing an arbitrary tag string does not yield exit 0 and a
  generated manifest, in a forced-failure case.
- **A-7.6** The installed plugin's version matches what the manifest advertised, checked in-game.

### R-7.5 A release ships the relay it needs

A plugin build is useless against a default relay that does not speak its protocol: every client is
refused, and nothing tells the person who released it. v0.1.7 shipped exactly that way.

- **A release is cut by CI on a `v*` tag**, never by hand. The pipeline is specified in
  `2026-10-04-release-pipeline-design.md`.
- **The default relay is deployed at the same tag before any plugin artefact is published,** and the
  live relay is checked to accept the plugin's protocol version and refuse the one before it.
- **A failed relay deploy or check publishes nothing:** no GitHub release, no `repo.json` change.
- The check reads the protocol version from the source; nobody types it.

**Acceptance criteria**
- **A-7.11** A release whose relay check fails publishes no release asset and leaves `repo.json`
  unchanged. Demonstrated by a dry run against a relay on the previous protocol version, which must
  stop at the check.
- **A-7.12** After a release, the default relay accepts the released plugin's protocol version. A
  release that ends green while the relay refuses the plugin fails this.

## Out of scope

- Submission to the **official** Dalamud plugin repository, and any work whose only justification is
  that the official repository would want it (`product-overview D-12`).
- Any update or version check performed **by the plugin**. Dalamud performs the manifest fetch; the
  plugin does not (`product-overview D-2`, `product-overview D-12`).
- Auto-update behaviour of any kind beyond what Dalamud does natively.
- Telemetry, install counting, or any measurement of who has installed it (`product-overview D-2`).

## The published relay service policy

The default relay's operator publishes a service policy before the relay is public
(`product-overview D-12`), linked above rather than duplicated here. It states what the relay stores
(nothing), what it can still observe (the public statement of `session-layer R-1.9`, kept in step
with it under `A-1.13b`), what happens if it is discontinued, and how it is funded. Funding is optional community support, kept entirely outside the plugin, and there is no
other anti-abandonment guarantee (`product-overview` Non-goals section, "No monetisation surface").

## Open questions

- Open question: whether a vanity subdomain (`repo.ruminabottle.com`) is used for the manifest URL
  (R-7.2). Costs a CNAME and some setup; buys a URL people can read aloud. Deferred to the stable
  channel. Not now.
- Open question: `IconUrl` (R-7.3) needs an icon that does not yet exist. Flagged, not urgent, and
  not a reason to delay a testing build.

## Retired IDs

- A-7.1: superseded by A-7.1a and A-7.10. As written ("the manifest URL returns valid JSON that
  Dalamud accepts as a custom repository") it could not fail against the defect it existed to catch —
  a manifest can be valid JSON that Dalamud parses and still never offer the plugin.
- A-7.2: superseded by A-7.2a and A-7.2b. As written ("the manifest's version matches the version in
  the built assembly it links to") it could not fail against the defect it existed to catch — a
  manifest and an assembly can both independently be typed as `0.0.0.1`, match each other exactly,
  and still describe a broken release.
- A-7.2a-note: folded into A-7.2a.
