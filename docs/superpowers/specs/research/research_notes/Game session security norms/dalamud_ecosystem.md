# Dalamud ecosystem: security, privacy and networking norms

Research date 2026-10-01. Extends `../../lightless-sync-client-server.md` (which covers Lightless's *architecture*: shared contract, SignalR hubs, DTOs). This file covers *norms*: rules, what peers disclose, community history. Not duplicated here: anything about Lightless's code shape.

**Correction to the brief:** the Mare Synchronos shutdown was **August 2025**, not 2024 (sources below).

## 1. What do Dalamud's official rules say about networking, privacy, data collection and disclosure?

### Takeaway
Dalamud's official repo has explicit, fairly strict backend-server rules: maintainer-run servers are allowed but must use TLS with a trusted-CA certificate, a DNS hostname rather than an IP, send minimum data, hash local-player identifiers (Content ID or name) client-side where feasible, make telemetry opt-in with a client-resettable pseudo-random ID, and never expose a list of plugin users. There is **no rule requiring a privacy policy, a disclosure text, or end-to-end encryption**. DungeonMasterXIV's design meets every stated rule and goes past them on E2E, out-of-band key verification and in-app disclosure.

### Cited Findings
- "Plugins are permitted to communicate with maintainer-run backend services, though there are certain considerations and requirements that must be met." — [Dalamud: Plugin Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- "Plugins should take care to send the minimum amount of data necessary to do their job. Whenever feasible, plugins should hash information about the local player (such as the player's Content ID or name) on the client side" (so a server-side breach does not reveal it). — [Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- Telemetry: allowed only if "the user is given a chance to review what data is being collected and for what purpose"; "Users must explicitly opt in"; must be "for the public interest". — [Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- "Plugins must use a pseudo-random identifier (or no identifier) for any analytics data. If an identifier is used, it must not contain or be derived from any personal information and must be resettable at any time by the user purely on the client side." — [Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- "Plugins must take care to not expose a list of other plugin users or allow an easy way to test whether a specific user is using any plugin." — [Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- "Any maintainer-run server for a plugin must use encrypted communication via HTTPS, TLS, or equivalent, and must have certificates issued from a trusted certificate authority such as Let's Encrypt." "Plugins connecting to backend servers must do so via a DNS hostname rather than an IP address." — [Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- Restrictions page (nine rules): no automatic or out-of-spec interaction with game servers; no combat augmentation; no interference with SE monetary interests (Mog Station); no parsing/DPS meters; "does not collect account IDs of player characters beyond your own in any form, regardless of the intended use"; no hard dependency on violating plugins; not only useful out-of-spec; no PvP/competitive advantage. No rule on character names, privacy policies, or disclosure. — [Dalamud: Plugin Restrictions](https://dalamud.dev/plugin-publishing/restrictions/)
- Submission (D17) requires a public Git repository and commit hash (`repository`, `commit`, `owners`, `project_path`, optional `changelog` in `manifest.toml`) and an AI-usage disclosure in the PR. The submission page itself does not define a network-use or privacy disclosure requirement. — [Dalamud: Submission](https://dalamud.dev/plugin-publishing/submission)
- Dalamud "cannot control plugins from custom repositories as a matter of design"; its rules bind only the official repo. — [Statement on Account IDs and Plugins, 2025-01-10](https://dalamud.dev/news/2025/01/10/account-ids-and-plugins/)

### Inferences
- The relay is fully compliant if it is on a DNS name with a Let's Encrypt (or other trusted-CA) certificate. A self-signed certificate or a raw IP endpoint would fail review, **E2E encryption notwithstanding**. Worth checking against the deployment config.
- "Hash the local player's name client-side where feasible" is the closest rule to our join flow. We send the display name **in the clear** at join. It is defensible because the DM must read it to approve the join (so hashing is not "feasible" for its purpose) and our disclosure text says so. Still, a reviewer could ask why. Consider recording that rationale in the spec, and whether the display name has to be the character name at all (a free-text name the player chooses would sidestep the rule's spirit entirely).
- "Don't expose a list of plugin users / no easy way to test whether a user runs the plugin": a relay that forwards and stores nothing, with no lookup by character, satisfies this by construction. Any future "is my friend online?" or session-discovery feature would need checking against it.
- Our "player can delete stored participant IDs" mirrors the analytics-ID rule ("resettable … purely on the client side"), although that rule is written for telemetry. We meet it in spirit for a non-telemetry identifier.

### Gaps
- I couldn't confirm the current `AcceptsFeedback` / Punchline / Description manifest requirements from the pages fetched. The submission page did not mention them. From memory, `AcceptsFeedback` defaults to true and sends in-installer feedback to Dalamud's feedback service, but I have no source for this, so treat it as unverified. Check the DalamudPackager / plugin-manifest docs on dalamud.dev.
- No source found for an explicit official-repo rule requiring a README privacy section or privacy policy. Its absence is itself the finding, but it comes from three pages, not an exhaustive crawl.

## 2. How do networked plugins actually handle transport, auth, operator visibility, disclosure and deletion?

### Takeaway
The norm is TLS to an operator-run, often closed-source server. Identity comes from a Discord account, often plus Lodestone character verification, and that gives the operator a persistent identity. Privacy info, where it exists at all, lives in Discord servers or terms-of-service pages, not in the plugin. I found **no** networked Dalamud plugin that does end-to-end encryption, out-of-band key fingerprint verification, or in-plugin disclosure of what the operator sees. DungeonMasterXIV is clearly above the observed norm.

### Cited Findings
- **Mare Synchronos (shut down Aug 2025):** registration was via a Discord bot: `/register` with your Lodestone URL, put a code on your Lodestone profile, then `/verify`. That issued a **secret key** (unrecoverable; lose it and re-register) and a UID that you share with people you pair with. — [HackMD Mare setup guide](https://hackmd.io/@JeffreyLeeTW/ffxiv_mare_synchronos_setup); [eqbot/Mare-Client](https://github.com/eqbot/Mare-Client)
  - So the Mare operator held a verified link of Discord account ↔ Lodestone character ↔ UID, plus a pairing graph and mod files (see the existing Lightless note: server is authoritative and persistent).
- **Mare forks after the shutdown:** Snowcloak, Lightless, PlayerSync, XIVSync (plus Absolute Roleplay's sync, reportedly ended); Snowcloak and Lightless are the most used. Distribution is through each project's Discord, and community members have urged not naming them publicly on social media. — [X post, Geneva Abrichter](https://x.com/DoctorAbrichter/status/1993119592823373874); [X post, SmuggestSage](https://x.com/SmuggestSage/status/1964009203879575845). (Social-media sources; low authority, but consistent with each other.)
- **PlayerSync:** register in its Discord via a bot, then authenticate in the plugin with **Discord OAuth2** (preferred) or a legacy secret key. The server stores character names and world, UIDs, appearance data and mod file info. The setup docs contain **no privacy policy, deletion procedure or encryption statement**. — [PlayerSync docs: Setup](https://docs.playersync.io/docs/setup?open=1st-time-setup)
- **Sonar** (crowdsourced hunt/FATE relay): the server "perform[s] the heavywork of receiving and broadcasting relays for every Sonar client" and "is not included in this repo" (closed source). The README says nothing about what data clients send, transport, auth or privacy, and sends users to a Discord for support. — [FFXIV-Sonar/SonarDistrib](https://github.com/FFXIV-Sonar/SonarDistrib)
- **Glance (RPHub.co RP profiles):** requires a free RPHub.co account; "real-time sync between web and plugin"; "nearby player discovery with distance"; the backend is "a separate, closed-source program"; the plugin is AGPL-3.0. The README has no data-sent, encryption or deletion statement, only ToS acceptance. — [rphubco/glance](https://github.com/rphubco/glance)
- **EchoRoleplay (RP profiles):** markets "no account". Private contact notes "never leave your machine and are never sent anywhere"; uploaded images are "decoded and re-encoded on the server, so no bytes a client sent are ever stored or served back"; blocking is local. No formal privacy policy found on the page. — [EchoRoleplay](https://echoxiv.com/echoroleplay/)
- **RpUtils ("Roleplay Sonar"):** "Information sent to the server is anonymous so you'll have to head out to those spaces to see who's there." — [Tumblr, erahsae-ffxiv](https://www.tumblr.com/erahsae-ffxiv/749215097995558912/rputils-dalamud-plugin-or-roleplay-sonar) (secondary/author post)
- **Roleplay Profiles (RPP):** the README carries only a one-line description; I found no data or privacy documentation. — [Codeberg Linneris/dalamud-roleplay-profiles](https://codeberg.org/Linneris/dalamud-roleplay-profiles)
- An RP plugin directory exists listing these tools. — [RP Supporting Plugins (carrd)](https://rp-plugins.carrd.co/)

### Inferences
- Best observed practice in the RP niche is "no account" (EchoRoleplay) and "anonymous to the server" (RpUtils). DungeonMasterXIV matches these with no accounts and no cross-campaign identifier. It exceeds them with E2E, fingerprint approval, a client-deletable participant ID and in-app disclosure of exactly what the relay sees.
- The sync lineage (Mare and forks) is the *opposite* model: Discord + Lodestone-verified persistent identity with a server-held social graph. Our "copy the shape, never the substance" rule in the Lightless note is reinforced by this.
- Peers keep privacy info in Discord or ToS pages, and in-plugin disclosure is rare. Our in-app disclosure is unusual and is a differentiator worth keeping. It also helps if the plugin ever faces a reviewer question about the clear-text display name.

### Gaps
- Chat2/ChatTwo, Splatoon/Pictomancy, PartyFinder plugins, Honorific/Moodles/Customize+: not researched in depth. Honorific/Moodles/Customize+ are generally understood to share state via IPC to local plugins and, for other players, via Mare-style sync. I have no source for this in this session.
- Lightless/Snowcloak/XIVSync privacy pages and transport details: not found. These projects are Discord-gated, and the web had no privacy statements to index.
- Whether any sync fork has moved off Lodestone verification after the shutdown: unknown.

## 3. Community concerns and Square Enix's stance

### Takeaway
Two 2025 incidents set the current climate. In January 2025, PlayerScope correlated alts via account IDs; it prompted a Dalamud statement, a new Dalamud rule banning account-ID collection, and a Yoshida statement threatening legal action. In August 2025 Mare Synchronos was shut down after a "legal inquiry", with Yoshida citing shared visibility of mods to *other* players, paid-item theft and NSFW content. The lessons: SE acts when a tool affects other players or correlates identity, and the community is acutely sensitive to alt-tracking and stalking.

### Cited Findings
- **PlayerScope (Jan 2025):** a custom-repo plugin that crowdsourced Content IDs and Account IDs (visible since 7.0's blacklist changes) to link characters on the same account. It caused panic about stalking and harassment and reached PC Gamer. — [notnite: PlayerScope, plugins, and Dalamud's fate](https://notnite.com/blog/playerscope)
- Dalamud: "fundamentally does not approve of the plugin in question". Its purpose is to "improve the game and add quality of life changes, not to support people who wish to harm others' experiences"; it notes that anything reading game memory, or Wireshark, can see these IDs. — [Dalamud statement, 2025-01-10](https://dalamud.dev/news/2025/01/10/account-ids-and-plugins/)
- Yoshida, 2025-01-24: confirmed tools exposing "a segment of an FFXIV character's internal account ID" to correlate characters; SE was considering "requesting that the tool in question be removed and deleted" and "pursuing legal action"; asked players to "refrain from using third-party tools"; third-party tools are prohibited by the User Agreement. — [SE forum: Regarding the Use of Third-Party Programs and Player Safety](https://forum.square-enix.com/ffxiv/threads/515102-Regarding-the-Use-of-Third-Party-Programs-and-Player-Safety)
- **Mare shutdown (Aug 2025):** the developer DarkArchon "received a legal inquiry concerning the project". Servers went down with about a day's notice; the Discord had around 200k users; Steam recent reviews dropped to Mixed. — [TheGamer](https://www.thegamer.com/final-fantasy-14-mare-synchronos-mod-shut-down-legal-inquiry/); [Kotaku](https://kotaku.com/one-of-ffxivs-most-important-mods-is-shutting-down-and-people-are-losing-it-2000619228); [PC Gamer](https://www.pcgamer.com/games/final-fantasy/legal-inquiry-shuts-down-one-of-final-fantasy-14s-most-popular-mods-adding-yet-another-blurry-line-in-the-sand-from-square-enix/); [Game Rant](https://gamerant.com/final-fantasy-14-review-bombed-why/)
- Yoshida, about 2025-08-27: "I personally see no reason to track down or investigate gamers for the general use of mods". Mods must be "for personal use only" and must not "infringe upon others". The named problems were Mog Station items obtained free via mods and NSFW screenshots ("FFXIV itself may be subject to legal measures"). — [Kotaku](https://kotaku.com/final-fantasy-ff14-mare-synchronos-mod-gposer-lewd-yoshida-2000620959)
- Commentary (opinion, not confirmed): platform parity, cosmetic revenue, P2P file-sharing security and IP issues, and age rating. — [The Nosy Gamer](https://nosygamer.blogspot.com/2025/08/why-square-enix-wanted-to-shut-down.html)
- Older but long-running: forum threads on doxxers using Lodestone and FFLogs to locate and harass players (server, character, instance). — [SE forum: Doxxers using lodestone and fflogs](https://forum.square-enix.com/ffxiv/threads/437950-Doxxers-using-lodestone-and-fflogs-to-harass-people/page5)

### Inferences
- SE's enforcement trigger so far has been (a) effects visible to or imposed on *other* players (Mare), (b) identity correlation (PlayerScope), and (c) monetisation or NSFW. A private, opt-in tabletop session tool that shows nothing in-world to non-participants and stores no cross-campaign identity is far from all three. All third-party tools remain formally against the User Agreement; that is a background risk shared by every Dalamud plugin, not something specific to us.
- A relay we operate is a single legal target, the same exposure that ended Mare in a day. Our existing commitment to a shutdown notice (see the operator→user message channel in the Lightless note) is directly relevant. Having no stored data on the relay also means there is nothing to hand over or leak if an inquiry arrives.

### Gaps
- No SE statement specifically about plugins *collecting character names* found. The account-ID statement is the closest.
- Reddit r/ffxiv and r/ffxivdiscussion threads not fetched; community sentiment is cited via press and forums instead.

## 4. Does sending character names over a relay raise community or SE-policy concerns?

### Takeaway
No rule forbids it: Dalamud bans collecting *account IDs*, not names, and only recommends hashing the local player's name where feasible. Peers (PlayerSync, the Mare lineage) store character names server-side routinely. The community's fear is **correlation and tracking** (alts, location, who-plays-with-whom). Our risk is low because the relay stores nothing, but a clear-text name plus IP plus timing at join is exactly the kind of data that fear is about. That is the one place to tighten or to explain in more detail.

### Cited Findings
- Restriction is on account IDs "of player characters beyond your own in any form". Names are not mentioned. — [Plugin Restrictions](https://dalamud.dev/plugin-publishing/restrictions/)
- Guidance to hash the local player's "Content ID or name" client-side "whenever feasible". — [Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- PlayerSync stores character names and server info. — [PlayerSync docs](https://docs.playersync.io/docs/setup?open=1st-time-setup)
- Mare-style registration bound a Lodestone-verified character to a Discord account. — [HackMD guide](https://hackmd.io/@JeffreyLeeTW/ffxiv_mare_synchronos_setup)
- The community reacts strongly to cross-character correlation (PlayerScope). — [notnite](https://notnite.com/blog/playerscope); [SE forum statement](https://forum.square-enix.com/ffxiv/threads/515102-Regarding-the-Use-of-Third-Party-Programs-and-Player-Safety)

### Inferences
- Options in order of strength: (1) let the joiner choose the display name rather than defaulting to the character name; (2) if the character name is used, keep the current disclosure and add to the spec why it can't be hashed (the DM must read it to approve); (3) make sure relay logs (reverse proxy, container stdout) don't persist names or session codes, because "the relay stores nothing" has to cover incidental logging too.
- Never put Content ID or Account ID on the wire, even hashed, as a join credential: the account-ID ban covers collection "in any form".

### Gaps
- No evidence found of an SE policy addressing operator-run relays that see character names.
