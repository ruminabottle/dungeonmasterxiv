# VTT and gaming voice/chat session security norms (as of 2026-10-01)

Scope: Foundry VTT, Roll20, Owlbear Rodeo (v1 legacy and v2), Fantasy Grounds, TaleSpire, Discord (text + DAVE voice/video). Alchemy RPG was not researched (budget); treat as a gap.

Fetch caveat: help.roll20.net, docs.owlbear.rodeo and support.discord.com returned HTTP 403 to the fetch tool. Claims from those pages below come from search-result excerpts of the official page, not a full read, and are marked "(excerpt)".

Comparison at a glance (each cell is backed by the cited findings below; "?" = not found):

| Product | (1) Encryption of session traffic | (2) Join / host verification | (3) Where security/privacy info lives | (4) Deletion controls |
| --- | --- | --- | --- | --- |
| Foundry VTT (self-hosted) | None by default; TLS optional and set up by the host | Invitation link (host IP) + per-user password chosen from a dropdown; admin access key for setup | KB articles on foundryvtt.com | ? (self-hosted, host owns the data) |
| Roll20 | TLS to Roll20 assumed (not verified from a primary source) | Account required; reusable Player Join Link; no approval step found | help.roll20.net, incident FAQ | Account deletion in account settings ("Danger Zone") |
| Owlbear Rodeo v1 (legacy) | P2P WebRTC for assets/audio, server for signalling (later a central sync server from 1.7.0) | Room link (+ password in v1, per the 2.0 docs) | GitHub README | Local IndexedDB only; browser can wipe it |
| Owlbear Rodeo v2 | Server-based; E2EE not claimed | Room link + "request access" waiting room + GM approves | docs.owlbear.rodeo | ? |
| Fantasy Grounds Unity | ? (Cloud relay or LAN port 1802) | GM name lookup or IP:port + optional campaign password | Customer portal wiki | ? |
| TaleSpire | ? | Invite code typed in "Join Campaign"; auto-join if a seat is free | Website / Steam Q&A | ? |
| Discord voice/video | E2EE (DAVE/MLS), mandatory since early March 2026, except Stage channels | Server/DM membership; optional out-of-band Voice Privacy Code and per-user Verification Codes, in a Privacy tab of the call details | Blog, support article, whitepaper; plus an in-call lock icon and Privacy tab | n/a for media (ephemeral) |
| Discord text | Not E2EE (no plans) | same | same | ? |

## Which products encrypt end-to-end, and which rely on TLS to a server they operate?

### Takeaway
Of the products surveyed, only Discord encrypts end-to-end, and only for voice and video: DAVE has been mandatory for every non-Stage call since early March 2026. Text on Discord is not end-to-end encrypted. None of the VTTs claims end-to-end encryption. Foundry does not even turn on TLS by default: the self-hosting GM has to set it up, and Foundry's own reason for doing so is browser camera and microphone permissions, not confidentiality.

### Cited Findings
- **Discord DAVE was announced 2024-09-17.** It covers voice and video in DMs, Group DMs, voice channels and Go Live streams. "messages on Discord will continue to follow our content moderation approach and are not end-to-end encrypted." The whitepaper is in the discord/dave-protocol GitHub repo. — [Discord blog: Meet DAVE](https://discord.com/blog/meet-dave-e2ee-for-audio-video)
- **Since March 1, 2026, clients without DAVE support cannot join calls.** Out-of-date clients are rejected by the voice gateway with close code 4017. — [Discord support: A/V E2EE Enforcement for Non-Stage Voice Calls](https://support.discord.com/hc/en-us/articles/38749827197591-A-V-E2EE-Enforcement-for-Non-Stage-Voice-Calls) (excerpt); [Discord blog: Bringing DAVE to All Discord Platforms](https://discord.com/blog/bringing-dave-to-all-discord-platforms)
- **Discord finished the migration "at the beginning of March 2026"** and says: "End-to-end Encryption is now standard for every voice and video call on Discord, outside of stage channels. No opt-in required." Stage channels are excluded because they "are designed for broadcasting to larger audiences". On text: "We have no current plans to extend E2EE to text messages." Discord is removing the client code that falls back to unencrypted calls. — [Discord blog: Every Voice and Video Call on Discord Is Now End-to-End Encrypted](https://discord.com/blog/every-voice-and-video-call-on-discord-is-now-end-to-end-encrypted); press coverage: [Help Net Security, 2026-05-19](https://www.helpnetsecurity.com/2026/05/19/discord-voice-and-video-call-encryption/)
- **The EFF criticised Discord's choice to leave text unencrypted:** "partial encryption is always confusing for users". — [EFF, Sept 2024](https://www.eff.org/deeplinks/2024/09/discords-end-end-encryption-voice-and-video-step-forward-privacy-all)
- **Foundry VTT (self-hosted):** the SSL article does not say SSL is on by default. It gives the main reason to configure it as A/V: "browsers ... will not let a website capture your camera and microphone unless that connection is secure". Setup is manual (Caddy, Certbot, self-signed or existing certificates). The article does not discuss passwords sent in cleartext. — [Foundry KB: SSL](https://foundryvtt.com/article/ssl/)
- **The Foundry Users KB article does not mention encryption or TLS at all.** — [Foundry KB: Users](https://foundryvtt.com/article/users/)
- **Owlbear Rodeo v1 (legacy):** WebRTC carries images peer to peer "because this project doesn't have user accounts or cloud storage". The backend is a socket.io signalling relay, and STUN/TURN is configured in `ice.json`. The README does not mention encryption beyond what WebRTC provides. — [owlbear-rodeo-legacy README](https://github.com/owlbear-rodeo/owlbear-rodeo-legacy). **From v1.7.0, game state syncs through a central server**, because the peer-to-peer sync was unreliable. — [Owlbear Rodeo 2.0 Dev Log 6](https://blog.owlbear.rodeo/owlbear-rodeo-2-0-dev-log-6/) (via search excerpt; older, pre-2023)
- **Fantasy Grounds Unity:** "Cloud" mode routes through FG's cloud server so the host needs no port forwarding. LAN mode needs TCP/UDP 1802 forwarded and is "no longer directly supported". No encryption statement was found. — [FG Customer Portal: How to Join a Game](https://fantasygroundsunity.atlassian.net/wiki/spaces/FGCP/pages/996639939/How+to+Join+a+Game)

### Inferences
- The norm for VTTs is "the server can read everything": TLS to the vendor (Roll20, Owlbear v2, FG Cloud) or nothing at all (Foundry self-hosted over plain http, which is common).
- Discord is the only point of comparison for end-to-end encryption, and it took Discord about 18 months (Sept 2024 to March 2026) to make it mandatory. A hobby plugin whose payloads are end-to-end encrypted is clearly above the VTT norm, and on par with Discord voice in kind, though not in rigour: DAVE uses MLS and has been audited.
- WebRTC media and data channels are always DTLS-encrypted. So Owlbear v1's peer-to-peer asset transfer was encrypted between peers in practice, but Owlbear never advertised it as a privacy feature, and the signalling server was trusted. This is my inference from WebRTC standards, not from an Owlbear source.

### Gaps
- No primary statement found on Roll20 transport encryption. HTTPS is near-certain but was not verified.
- Fantasy Grounds Cloud relay encryption: nothing found.
- TaleSpire networking and encryption: nothing found.
- Alchemy RPG was not researched.
- Whether DAVE's MLS implementation has had an independent audit: I recall Trail of Bits audited it, but this was not verified here.

## Joining and host verification: does anyone use out-of-band fingerprint comparison, and is Discord's in the join path?

### Takeaway
No VTT uses fingerprint comparison. The VTT norm is a shared secret: a link, a code, or a link plus password. Owlbear Rodeo v2 adds a waiting room where the GM approves each joiner, but on the basis of the name they show, not a cryptographic check. Discord DAVE is the only product with out-of-band code comparison: a group Voice Privacy Code plus pairwise Verification Codes. These are optional, sit in a Privacy tab of the call details panel rather than in the join path, and by default must be redone every call.

### Cited Findings
- **Discord's group code.** The Voice Privacy Code is the MLS "epoch authenticator", shown "in call details under a new Privacy tab". It changes when anyone joins or leaves. — [Discord blog: Meet DAVE](https://discord.com/blog/meet-dave-e2ee-for-audio-video)
- **Discord's per-user codes.** Users can "view a pairwise Verification Code for each of the other users in your audio and video call". "By comparing these codes out-of-band, participants of the MLS group can verify that they all have the same MLS group state and that no one is being impersonated." Codes are ephemeral per call by default. Users may opt into "a persistent identity key pair for each device". — [Discord blog: Meet DAVE](https://discord.com/blog/meet-dave-e2ee-for-audio-video)
- **Where it shows on desktop.** A green lock labelled "End-to-end encrypted" appears in the Voice/Video Details panel, next to a Privacy tab that holds the Voice Privacy Code. — [Discord support: End-to-End Encryption for Audio and Video](https://support.discord.com/hc/en-us/articles/25968222946071-End-to-End-Encryption-for-Audio-and-Video) (excerpt)
- **EFF on the verification burden:** "By default, you have to do this *every time* you initiate a call if you wish to verify". Persistent keys reduce this to once per device, but "the keys on separate devices you own will be different". — [EFF](https://www.eff.org/deeplinks/2024/09/discords-end-end-encryption-voice-and-video-step-forward-privacy-all)
- **Discord frames verification as optional and for higher-risk conversations.** — [TechRepublic](https://www.techrepublic.com/article/news-discord-dave-end-to-end-encryption/) (secondary)
- **Foundry joining.** The GM shares an invitation link: a local and a public IP address, with the remote one obscured by default. Users pick their name from a dropdown and enter a password. "Newly created users do not start with a password". Foundry "strongly recommend[s] setting at least a simple password for each user, especially for GM-level users". A separate Administrator Access Key protects the setup screen. — [Foundry KB: Users](https://foundryvtt.com/article/users/); [Foundry KB: Tutorial – Gamemaster Part Two](https://foundryvtt.com/article/tutorial-two/) (excerpt); [Foundry KB: Application Configuration](https://foundryvtt.com/article/configuration/) (excerpt)
- **Roll20 joining.** Roll20 requires at least a free account. A reusable Player Join Link "can be used an unlimited number of times" and "changes whenever a Player is kicked". No GM approval step was found. — [Roll20 Help: Game Invite Troubleshooting](https://help.roll20.net/hc/en-us/articles/360046018993-Game-Invite-Troubleshooting); [Roll20 Help: Invite, Promote and Manage Players](https://help.roll20.net/hc/en-us/articles/29620515876375-Invite-Promote-and-Manage-Players) (both excerpt)
- **Owlbear Rodeo v2 joining.** v2 dropped room passwords for "request access": the player opens the room link, lands in a waiting room and requests to join, and the GM approves. Anonymous players are allowed, with no account needed. — [Owlbear docs: Rooms](https://docs.owlbear.rodeo/docs/rooms/); [Owlbear docs: Migrating from 1.0 to 2.0](https://docs.owlbear.rodeo/docs/migration/migrate-from-1-to-2/) (excerpt)
- **Fantasy Grounds joining.** Players join by GM name (their forum username) in Cloud mode, or by IP and port in LAN mode, plus a campaign password if the GM set one. — [FG Customer Portal: How to Join a Game](https://fantasygroundsunity.atlassian.net/wiki/spaces/FGCP/pages/996639939/How+to+Join+a+Game)
- **TaleSpire joining.** The GM generates an invite code, and the player types it into "Join Campaign". If a seat is available, "you will get in immediately". — [TaleSpire: Getting started with seats as a guest](https://talespire.com/getting-started-with-seats-as-a-guest) (excerpt); [Steam Q&A](https://steamcommunity.com/app/720620/discussions/0/7529517132617041985/)

### Inferences
- DMXIV's flow (short code, then host approval, then fingerprint read aloud) combines Owlbear v2's approval gate with a Discord-style out-of-band code. No VTT does the latter.
- Making fingerprint comparison part of every approval puts it in the join path. That is stronger in placement than Discord, where comparison is buried and optional. It is also more friction than any peer product imposes.
- Discord's experience (EFF: "every time" is burdensome; persistent keys added as the fix) suggests DMXIV's per-entry stored identity is the right mitigation for repeat players.

### Gaps
- Mobile and console placement of Discord's Privacy tab was not confirmed.
- No usage data found on how often Discord users actually verify codes.

## Where does privacy/security information live: in-app at point of use, or in policies and help centres?

### Takeaway
Almost entirely off-app: KB articles, help centres, blogs and privacy policies. The only in-app, point-of-use signal found is Discord's: a green "End-to-end encrypted" lock and the Privacy tab in the call details panel. Neither is a disclosure shown at join time. No VTT was found showing privacy text when a player joins.

### Cited Findings
- **Discord's in-call status.** Discord shows encryption status in-call (green lock, Privacy tab). Detail lives in a blog post, a support article and a GitHub whitepaper. — [Discord support](https://support.discord.com/hc/en-us/articles/25968222946071-End-to-End-Encryption-for-Audio-and-Video) (excerpt); [Discord blog](https://discord.com/blog/meet-dave-e2ee-for-audio-video)
- **Foundry's security guidance** (passwords, access key, SSL) lives in KB articles. The only in-app protective touch found is that the remote invite link is obscured by default. — [Foundry KB: SSL](https://foundryvtt.com/article/ssl/); [Foundry KB: Users](https://foundryvtt.com/article/users/); [Foundry Tutorial Part Two](https://foundryvtt.com/article/tutorial-two/) (excerpt)
- **Roll20's breach communication** went out by email notice and a help-centre FAQ. — [Roll20 Help: Data Security Incident (July 3rd, 2024) FAQ](https://help.roll20.net/hc/en-us/articles/24620372778775-Data-Security-Incident-July-3rd-2024-FAQ); [Roll20 forum: notification thread (2019)](https://app.roll20.net/forum/post/7622620/i-just-received-a-notification-of-a-data-breach-on-roll20)
- **Owlbear v1's data-loss caveats** ("Safari will delete all your data", Chrome "will silently decide") are in the developer README, not in the app. — [owlbear-rodeo-legacy README](https://github.com/owlbear-rodeo/owlbear-rodeo-legacy)

### Inferences
- An in-app disclosure of what the relay can see, at the point of use, has no counterpart among the VTTs surveyed, and is more specific than Discord's lock icon. This is above the norm.

### Gaps
- I did not open each product's privacy policy or watch each join screen. An absence claim ("no join-time disclosure") rests on not finding one in docs and press, not on watching the UI.

## User-facing data deletion controls (local and server)

### Takeaway
Deletion is account-level and server-side where accounts exist: Roll20 has a self-serve "Delete My Account". Self-hosted Foundry leaves the data with the host. Owlbear v1's only "storage" was browser IndexedDB, which browsers can wipe on their own. None of the products had fine-grained local controls like "delete my stored participant IDs" in what I found.

### Cited Findings
- **Roll20 account deletion.** Account page, "Danger Zone", "Delete My Account", then type DELETE to confirm. Contact team@roll20.net for help. — third-party guide [helpdelete.com](https://helpdelete.com/how-to-delete-your-roll20-account/) (secondary; Roll20's own page was not fetched)
- **Roll20's post-breach data access.** After the 2024 incident, users could request a copy of the account data the intruder may have accessed, via a support ticket titled "Incident Data Request". — [Roll20 incident FAQ](https://help.roll20.net/hc/en-us/articles/24620372778775-Data-Security-Incident-July-3rd-2024-FAQ) (excerpt)
- **Owlbear v1 storage.** Images and maps were stored only client-side in IndexedDB, which the browser may evict. — [owlbear-rodeo-legacy README](https://github.com/owlbear-rodeo/owlbear-rodeo-legacy)

### Inferences
- Local, per-item deletion of stored identifiers is uncommon and is above the norm for this category.

### Gaps
- Deletion controls for Discord, Owlbear v2, Fantasy Grounds and TaleSpire were not researched.
- Foundry's world and user deletion was not researched (presumably the host deletes the user in Configure Players).

## Notable incidents and community complaints that shaped expectations

### Takeaway
Roll20 has had two disclosed incidents (Dec 2018 and June 2024). Both exposed account PII held server-side, and neither involved session content. Discord's partial E2EE (voice yes, text no) drew criticism from privacy advocates. The VTT community's security expectations are set by account breaches, not by eavesdropping.

### Cited Findings
- **Roll20, December 2018 breach (older; disclosed 2019).** Breached 2018-12-26, announced 2019-02-14. About 4M accounts were affected: emails, IPs, names, bcrypt password hashes and the last 4 card digits. The cause is attributed to a security misconfiguration. Added to HIBP 2019-07-19. — [Have I Been Pwned: Roll20](https://haveibeenpwned.com/Breach/Roll20); [Mozilla Monitor](https://monitor.mozilla.org/en/breach-details/Roll20)
- **Roll20, 2024 incident.** An administrative account was compromised, discovered 2024-06-29, and access was blocked within an hour. Exposed fields may include name, email, last IP and last 4 card digits. No passwords or full card data were exposed. One user account was modified. — [Roll20 Help: Data Security Incident FAQ](https://help.roll20.net/hc/en-us/articles/24620372778775-Data-Security-Incident-July-3rd-2024-FAQ) (excerpt); [EN World](https://www.enworld.org/threads/roll20-hacked-customer-information-possibly-exposed.705130/)
- **EFF on Discord** called DAVE "a step forward" but criticised the absence of E2EE for private text. — [EFF](https://www.eff.org/deeplinks/2024/09/discords-end-end-encryption-voice-and-video-step-forward-privacy-all)
- **Owlbear's developers on WebRTC:** "every time I've decided to use WebRTC in a project I've regretted it". They moved to a central server, trading peer-to-peer privacy for reliability. — [owlbear-rodeo-legacy README](https://github.com/owlbear-rodeo/owlbear-rodeo-legacy)

### Inferences
- A relay that stores nothing and cannot read payloads is structurally immune to the Roll20 class of incident, which was a server-side PII dump. That is a stronger story than encryption alone.
- Owlbear's retreat from peer-to-peer is a caution that reliability pressure pushes hobby products toward trusting the server. Keeping E2EE through a relay avoids that trade.

### Gaps
- No significant security incidents found for Foundry, Owlbear, Fantasy Grounds or TaleSpire. I did not search exhaustively.
