# Game networking SDKs and player-hosted games: encryption and joining norms

Researched 2026-10-01. Unless a date is given, findings come from vendor docs as fetched on that date. Older incidents are dated inline.

## Is end-to-end encryption between players (the relay can't read it) common, rare, or absent in games?

### Takeaway
It exists only where the SDK's encryption session runs between the two game endpoints and the relay just forwards packets. Valve's Steam Networking Sockets/SDR does this, and so does a WebRTC data channel through TURN. Epic EOS P2P probably does too. Relay-centric products (Unity Relay, Photon Cloud) encrypt only between client and relay, and Mirror encrypts nothing by default. Individual games almost never make E2E a feature or say anything about it. The ones that get it (many Steam co-op titles) inherit it from Steamworks.

### Cited Findings
- **Valve GameNetworkingSockets (open source)** lists "Encryption. AES-GCM-256 per packet, Curve25519 for key exchange and cert signatures". Key derivation and the per-packet IV follow Google's QUIC design. — [GameNetworkingSockets README](https://github.com/ValveSoftware/GameNetworkingSockets)
- **Steamworks ISteamNetworkingSockets** promises "Strong encryption and authentication. When a player connects, you can be sure that if a certain SteamID is authenticated, that someone who has access to that person's account has authorized the connection". It also says "Eavesdropping / tampering requires hacking into the VAC-secured process. Impersonation requires access to the target's computer." ConnectP2P traffic "is relayed over the Steam Datagram Relay network". — [Steamworks ISteamNetworkingSockets](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets)
- **Steam Datagram Relay** says "All traffic you receive is authenticated, encrypted, and rate-limited". It uses "a proprietary public key infrastructure (PKI) to authenticate clients and servers", and "Certificates are an end-to-end concept, and can be used in all forms of SteamNetworkingSockets communication." — [Steamworks SDR doc](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay)
- The SteamNetworkingSockets config allows "unencrypted (and unauthenticated) communication", but encryption is required by default ("disallowed" is the default value). These relaxations are labelled dev-only: "you should not let users modify it in production". — [steamnetworkingtypes.h](https://raw.githubusercontent.com/ValveSoftware/GameNetworkingSockets/master/include/steam/steamnetworkingtypes.h)
- **Epic Online Services P2P**: connections are "secure-by-default using DTLS". RelayControl defaults to EOS_RC_AllowRelays, which tries a direct connection first and relays only if that fails. EOS_RC_ForceRelays "will hide IP Addresses from peers". — [EOS P2P interface / relay control (search summary of dev.epicgames.com)](https://dev.epicgames.com/docs/api-ref/enums/eos-e-relay-control); [EOS P2P reference](https://dev.epicgames.com/docs/epic-online-services/multiplayer/nat-p2p-interface/p2p-reference)
- **Unity Relay**: "Relay supports DTLS encryption of all UDP communication to and from the Relay servers". "After the handshake, the Relay message protocol is fully encapsulated by DTLS." The PSK is the allocation key that the Allocations service hands out. DTLS is opt-in: you set connectionType to "dtls" instead of "udp" or "wss". — [Unity Relay DTLS encryption](https://docs.unity.com/en-us/relay/dtls-encryption); [Enable DTLS encryption](https://docs.unity.com/en-us/relay/enable-dtls-encryption)
- **Photon (Fusion/PUN/Bolt)**: Datagram Encryption GCM encrypts "the connection between the local peer and the Photon Cloud". It needs a native plugin that you have to request from Photon Support because it is "not included in the default Fusion package due to its size and rare use". Fusion has a separate opt-in "Fusion Encryption System" for direct server↔client connections. — [Photon Fusion 2 Connection Encryption (via search summary)](https://doc.photonengine.com/fusion/current/manual/advanced/encryption); [PUN encryption page](https://doc.photonengine.com/pun/current/reference/encryption) (the fetch failed on a cookie wall)
- **Mirror (Unity)** uses KCP as its default transport. Encryption comes from a separate EncryptionTransport wrapper that you add (ECDH + HKDF-SHA256, then AES-GCM). — [Mirror Encryption Transport](https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport); [Mirror KCP Transport](https://mirror-networking.gitbook.io/docs/manual/transports/kcp-transport)
- **Include Security (2023)** showed that players in Unity/Mirror games over default unencrypted UDP can be impersonated by UDP spoofing. — [Include Security blog, Apr 2023](https://blog.includesecurity.com/2023/04/impersonating-local-unity-players-with-udp-spoofing-in-mirror/)
- **WebRTC**: every implementation does a DTLS handshake per ICE component, and "All implementations MUST support DTLS 1.2" with ECDHE-ECDSA-AES128-GCM. TURN relays forward the DTLS ciphertext. — [RFC 8827](https://datatracker.ietf.org/doc/html/rfc8827)
- **Among Us** started out as plaintext UDP on a Hazel fork (people sniffed packets to unmask impostors). It later moved to DTLS 1.2 to its own servers. — [amongus-impostor-detector](https://github.com/lesander/amongus-impostor-detector); [Hazel-Networking](https://github.com/willardf/Hazel-Networking)
- **Minecraft Java**: online-mode servers use AES/CFB8 between client and server, with an RSA key exchange. Offline/LAN mode was unencrypted; per the wiki, encryption can be used in offline mode again from 1.20.5. The server always decrypts. There is no E2E between players. — [Minecraft Wiki: Java protocol encryption](https://minecraft.wiki/w/Java_Edition_protocol/Encryption)
- **Valheim crossplay** registers the session with Microsoft PlayFab, which returns a 6-digit numeric join code; PC players can also connect by IP. — [connecthosting Valheim crossplay](https://connecthosting.net/help/games/valheim/valheim-crossplay-join-code); [low.ms Valheim join codes](https://low.ms/blog/valheim-crossplay-join-codes)
- **Outside games:** Discord's DAVE protocol (2024; Discord says it covers all voice and video calls by default as of 2026) is E2E with MLS. Text messages remain unencrypted. — [Discord blog: Meet DAVE](https://discord.com/blog/meet-dave-e2ee-for-audio-video); [EFF](https://www.eff.org/deeplinks/2024/09/discords-end-end-encryption-voice-and-video-step-forward-privacy-all)

### Inferences
- Steam Networking Sockets is E2E with respect to SDR relays. Valve's threat model puts eavesdropping in the endpoint process, not the relay. The trust anchor is Valve's own PKI, though, so Valve as CA could in principle issue a cert for anyone. A user can't check this, and no user-facing key exists.
- No game I found markets E2E between players or lets players see it. Where it exists, it is a property the transport inherited.
- The most common pattern for client-server and relay services (Unity Relay, Photon, Minecraft, Among Us) is that the operator's server terminates encryption. It is often opt-in and often absent (Mirror default, old Among Us, Minecraft LAN).
- DMXIV's design is E2E with the relay excluded, with keys the users anchor themselves rather than a platform CA. That is above the game norm. In model it is close to Steam Networking Sockets/WebRTC, and it improves on them by not relying on a platform CA.

### Gaps
- I found no Valve statement that says verbatim "SDR relays cannot decrypt". The conclusion rests on the "VAC-secured process" wording and the "end-to-end" cert statement.
- I could not fetch the EOS P2P page (empty render). The DTLS claim comes from a search summary of Epic docs. I could not confirm whether EOS relays terminate DTLS.
- I found no primary docs for FishNet's default encryption, and none for the transports used by Phasmophobia, Lethal Company or BG3. Lethal Company uses Steam lobbies with Unity Netcode, likely over a Steamworks transport; this is unverified.
- Jackbox's websocket transport encryption was not checked.

## Is TLS/DTLS to the relay the common default?

### Takeaway
It is mixed. The platform-scale SDKs (Steam, EOS) encrypt by default. Unity Relay, Photon datagram encryption and Mirror leave it opt-in or off. "Encrypted by default" holds for Steam/EOS and WebRTC, not for the Unity/Photon/Mirror ecosystem.

### Cited Findings
- Steam: unencrypted is "disallowed" by default. — [steamnetworkingtypes.h](https://raw.githubusercontent.com/ValveSoftware/GameNetworkingSockets/master/include/steam/steamnetworkingtypes.h)
- EOS P2P is "secure-by-default using DTLS". — [EOS P2P reference](https://dev.epicgames.com/docs/epic-online-services/multiplayer/nat-p2p-interface/p2p-reference)
- Unity Relay supports udp, dtls and wss. Developers have to pick dtls, and secure connections need Unity 2020.3.34+ / 2022.1+. — [Unity Relay DTLS](https://docs.unity.com/en-us/relay/dtls-encryption); [Unity enable DTLS](https://docs.unity.com/en-us/mps-sdk/networking/enable-dtls-encryption)
- Photon datagram encryption is described as "rare use" and needs a plugin from support. — [Photon Fusion encryption](https://doc.photonengine.com/fusion/current/manual/advanced/encryption)
- Mirror: no encryption unless EncryptionTransport is added. — [Mirror Encryption Transport](https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport)
- WebRTC: DTLS is mandatory. — [RFC 8827](https://datatracker.ietf.org/doc/html/rfc8827)

### Inferences
- Encrypting to a relay is the floor among serious platforms, and relays exist mainly to hide IPs (see the threats section). Plenty of indie Unity games ship without encryption at all.

### Gaps
- I found no survey data on what share of shipped games enable encryption.

## Do any games use human-verified fingerprints?

### Takeaway
I found no game that does. The closest analogues outside games are Discord's DAVE "Voice Privacy Code" / per-user "Verification Code" for out-of-band comparison, and Signal/WhatsApp safety numbers. Game SDKs offer key pinning through code or a CA, never through a human comparison.

### Cited Findings
- Mirror's EncryptionTransport validation modes are Off (the default), a List of keys "baked in" to the build, or a Callback. The docs note that "For complete Man-In-The-Middle security, at least one side needs to validate the public key." — [Mirror Encryption Transport](https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport)
- Steam identity comes from Valve's proprietary PKI/certificates tied to SteamIDs. — [Steamworks SDR](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay)
- WebRTC binds DTLS cert fingerprints into SDP. RFC 8827 also defines an optional identity-provider assertion over the fingerprint. That fingerprint check is automatic: it is only as trustworthy as the signaling channel, and users don't compare it by hand. — [RFC 8827](https://datatracker.ietf.org/doc/html/rfc8827)
- Discord DAVE's Voice Privacy Code "can be compared out-of-band to ensure that nobody on the call is being impersonated". Each pair of users can also compare a per-user Verification Code. — [Discord: Meet DAVE](https://discord.com/blog/meet-dave-e2ee-for-audio-video); [DAVE whitepaper](https://daveprotocol.com/)

### Inferences
- The expectation holds: human-compared fingerprints are essentially absent from games. In consumer software they appear only in E2EE messaging and voice (Signal, WhatsApp, Discord DAVE). DMXIV's read-aloud fingerprint puts it in messenger-grade territory, which is well above game norms. Users will find the step unfamiliar.

### Gaps
- I did not systematically search niche/open-source games (e.g. Veloren, Luanti mods) for SAS-style verification. Absence comes from not finding any, not from proof.

## What do short join codes typically protect, and do games treat codes as secrets?

### Takeaway
Join codes are capability tokens for routing and admission. They are short (4–6 chars), often rotate per session, and get treated as semi-secret ("don't show it on stream"). They usually ride on top of a platform account (Steam, PlayFab, Larian account), and many games layer a password or host kick on top. They are not cryptographic secrets, and they leak.

### Cited Findings
- Valheim: a 6-digit numeric code from PlayFab that exists only while the server runs and changes on restart. The server password is required as well as the code. — [connecthosting](https://connecthosting.net/help/games/valheim/valheim-crossplay-join-code)
- Unity Relay: each allocation gets a unique join code "suitable for sharing over instant messages". — [Unity Relay + NGO](https://docs.unity.com/en-us/relay/relay-and-ngo-standalone)
- Jackbox: room codes leak via streams. Streamers are told to add a "PASSWORDED GAME" password on top of the room code, or "REQUIRE TWITCH" login, and Jackbox offers moderation tools. A GitHub tool scans for open rooms; its FAQ asks "Won't joining a random room full of strangers ruin their experience?" and answers "Yes." — [Jackbox: So You Want To Stream Jackbox](https://www.jackboxgames.com/blog/so-you-want-to-stream-jackbox); [jackboxtv-room-finder](https://github.com/nelsonfigueroa/jackboxtv-room-finder)
- Baldur's Gate 3 privacy settings are Public, Friends only, Invitation only, and Closed (Direct Connection ID only). Cross-play needs a Larian Account linked to a platform account. — [Windows Central / PC Gamer BG3 multiplayer guides](https://www.pcgamer.com/baldurs-gate-3-multiplayer-co-op-guide/); [Larian support](https://larian.com/support/faqs/multiplayer-information_53)
- Lethal Company lobbies are public (listed), friends-only (unlisted, joinable via lobby ID) or invite-only. Public lobbies attract griefers. — [Lethal Company Steam discussions](https://steamcommunity.com/app/1966720/discussions/0/4032473436332175434/); [Thunderstore LobbyImprovements wiki](https://thunderstore.io/c/lethal-company/p/Dev1A3/LobbyImprovements/wiki/2517-steam-lobby-hosting/)
- Among Us: public lobbies and private 6-letter codes. In Oct 2020 the "Eris Loris" spam hack hit public games (reports of up to ~5M players affected), and Innersloth pushed an emergency server update. — [Yahoo/Engadget via Yahoo](https://news.yahoo.com/among-us-eris-loris-hack-223724773.html); [TalkEsport](https://www.talkesport.com/news/among-us-eris-loris-hack-explained/)

### Inferences
- The norm is code + platform identity + host kick/ban (+ optional password). Codes are treated as low-value, rotating and semi-secret. No game binds a code to a key. DMXIV's "code locates the session, fingerprint authenticates the DM, DM approves each joiner" is stricter than any of these. Per-joiner host approval is roughly comparable to "invite-only" plus a kick.

### Gaps
- I found no primary source for Among Us code length and entropy, or for Phasmophobia's code format; these are from memory and unverified.

## What's the real-world threat history: IP exposure/DDoS in P2P, RCE via netcode, griefers joining via leaked codes?

### Takeaway
The documented harms are, in order: (1) griefers and spammers joining public or leaked rooms, (2) peers' IPs exposed in P2P and used for DDoS and harassment, and (3) memory-corruption RCE in game netcode reachable by any peer or an invite. Eavesdropping or a relay operator reading payloads barely appears in the incident record.

### Cited Findings
- **Dark Souls (Jan–Feb 2022):** an RCE in the P2P netcode of DS3 and earlier titles was exploited live on a Twitch stream. Bandai Namco took PC PvP servers offline until after Elden Ring launched (Feb 25, 2022). — [Threatpost](https://threatpost.com/dark-souls-servers-down-rce-bug/177896/); [PC Gamer](https://www.pcgamer.com/psa-dont-play-dark-souls-3-until-a-new-remote-code-execution-vulnerability-is-patched/); [Game Informer](https://gameinformer.com/2022/02/09/update-dark-souls-pc-servers-to-remain-offline-until-after-the-launch-of-elden-ring)
- **Source engine CVE-2021-30481 (2021):** RCE via a malicious Steam game invite. It was reported to Valve about two years before disclosure and was still unpatched in CS:GO as of April 2021. — [secret.club writeup](https://secret.club/2021/04/20/source-engine-rce-invite.html); [BleepingComputer](https://www.bleepingcomputer.com/news/security/cs-go-valve-source-games-vulnerable-to-hacking-using-steam-invites/)
- **GTA Online:** its P2P model exposed player IPs, which modders used for DDoS and threats such as swatting. Rockstar later moved some traffic through relays depending on latency. — [Dexerto](https://www.dexerto.com/gta/gta-online-players-warned-about-exploit-that-lets-modders-swat-ddos-users-1484457/)
- Valve's and Epic's relay docs give IP hiding as the main reason for relays: "IP addresses are never revealed" (SDR) and ForceRelays "will hide IP Addresses from peers" (EOS). — [Steamworks SDR](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay); [EOS relay control](https://dev.epicgames.com/docs/api-ref/enums/eos-e-relay-control)
- **Among Us Eris Loris (Oct 2020):** mass spam and griefing in public lobbies. — [Yahoo](https://news.yahoo.com/among-us-eris-loris-hack-223724773.html)
- **Mirror UDP spoofing (2023):** impersonation in unencrypted Unity games. — [Include Security](https://blog.includesecurity.com/2023/04/impersonating-local-unity-players-with-udp-spoofing-in-mirror/)
- **Minecraft secure chat (1.19/1.19.1, 2022):** Mojang began signing each chat message with a per-account key, and enforce-secure-profile is on by default. The point was to make chat reports provably attributable for global bans. The community backlash took the form of the "No Chat Reports" mod, which strips the signatures. Here cryptographic identity was received as a surveillance and moderation tool, not as privacy. — [Adrian's blog deep-dive](https://blog.bithole.dev/blogposts/mc-report-system/); [No Chat Reports (Modrinth)](https://modrinth.com/mod/no-chat-reports); [Fabulously Optimized FAQ](https://github.com/Fabulously-Optimized/wiki/blob/main/chat-reporting-faq.md)

### Inferences
- For a small RP plugin, the realistic threats track this history: uninvited joiners, IP exposure (DMXIV's relay already hides it), and parser bugs in incoming payloads. Plaintext snooping by the relay is not a documented attack pattern against games. E2E mainly protects against the operator and against a relay compromise, which is a trust and privacy story rather than an answer to observed attacks.
- The Minecraft episode suggests users may read visible cryptographic identity as accountability or surveillance. DMXIV's fingerprints are user-controlled and not reported anywhere, so it is worth wording that clearly.

### Gaps
- I found no documented incident of a game relay operator reading or leaking session payloads.
- I found no incident record for Phasmophobia, Lethal Company or BG3 beyond public-lobby griefing anecdotes from Steam forums.
