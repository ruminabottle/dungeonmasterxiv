# Legal baseline, disclosure UX norms, out-of-band verification, and threat model for a hobby RP relay

Scope note: this is research, not legal advice. It reports what sources say as of October 2026.

## 1. Legal baseline (GDPR / UK GDPR / CCPA): what does the relay operator owe, and where must the notice live?

### Takeaway
An IP address seen by the relay is very likely personal data (Breyer). A character name tied to an account is an "online identifier" and is probably personal data too. So the operator needs Art. 13 information, a lawful basis (legitimate interest, with Recital 49 network security for short-lived logs), and limited retention. EU guidance accepts a linked or layered privacy notice. For apps it should be reachable from the store listing and never more than "two taps away" inside the app. That points to a link to the notice in the plugin's menu, not long in-app text. Data that only ever sits on the DM's or player's own machine is not processed by the operator, so the operator has no erasure duty for it. The DM's own storage plausibly falls under the household exemption. CCPA almost certainly does not apply to a free, non-commercial project.

### Cited Findings
- **IP addresses.** In Breyer v Bundesrepublik Deutschland (C-582/14, 19 Oct 2016) the CJEU held that a dynamic IP address is personal data for a website operator when the operator "has the legal means" to identify the visitor using additional data held by the ISP. That access must be a "means likely reasonably to be used to identify" the person. — [Inside Privacy (Covington)](https://www.insideprivacy.com/international/cjeu-confirms-dynamic-ip-addresses-to-be-personal-data/); [GDPRhub summary](https://gdprhub.eu/index.php?title=CJEU_-_C-582/14_-_Patrick_Breyer); [Bird & Bird](https://www.twobirds.com/en/insights/2016/global/cjeu-decision-on-dynamic-ip-addresses-touches-fundamental-dp-law-questions)
- **Breyer's context.** It was decided under the 1995 Directive. The claim was about German federal websites storing IPs beyond the access session, which is the same storage-and-retention question a relay faces. — [IAPP](https://iapp.org/news/a/in-breyer-decision-today-europes-highest-court-rules-on-definition-of-personal-data); [A&L Goodbody](https://www.algoodbody.com/insights-publications/cjeu-rules-ip-addresses-may-constitute-personal-data)
- **Relative test (2025).** In EDPS v SRB (C-413/23 P, 4 Sep 2025) the CJEU confirmed that whether data is personal is judged from each holder's perspective. Strongly pseudonymised data can be personal for the original controller but not for a recipient who cannot re-identify. Information duties apply at the point of collection. — [CJEU press release](https://curia.europa.eu/site/upload/docs/application/pdf/2025-09/cp250107en.pdf); [Judgment (EUR-Lex)](https://eur-lex.europa.eu/legal-content/EN/TXT/PDF/?uri=CELEX%3A62023CJ0413); [Jones Day](https://www.jonesday.com/en/insights/2025/09/cjeu-clarifies-scope-of-personal-data-in-edps-v-srb-decision)
- **Usernames and online identifiers.** The UK GDPR names "online identifiers" in its definition of personal data. ICO guidance says an ID number, location data "or even an online username" can be enough to identify someone. Pseudonymised data stays personal data. — [ICO: identifiers and related factors](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/personal-information-what-is-it/what-is-personal-data/what-are-identifiers-and-related-factors/); [ICO: what is personal data](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/personal-information-what-is-it/what-is-personal-data/what-is-personal-data/)
- **Gaming example.** The Swedish DPA (IMY) took a GDPR decision against MAG Interactive (QuizDuel) over handling of players' usernames. That shows a regulator treating in-game usernames as personal data. — [EDPB-published decision SE 2023-05](https://www.edpb.europa.eu/system/files/2023-07/se_2023-05decisionpublic.pdf) (summary via search result; I did not read the full decision)
- **Lawful basis for security logs.** GDPR Recital 49 says processing "to the extent strictly necessary and proportionate for the purposes of ensuring network and information security" is a legitimate interest. That covers resisting attacks that harm availability and integrity, which is the usual basis for transient IP and abuse logs. — [Recital 49 text (gdpr-info.eu)](https://gdpr-info.eu/recitals/no-49/)
- **How information may be delivered.** The ICO's right-to-be-informed guidance accepts a layered approach, dashboards, just-in-time notices, icons, and mobile-device features. A layered notice is a short notice with key facts that links to a fuller second layer. A just-in-time notice appears at the point of collection and is recommended for unexpected or more intrusive processing. Information must be "concise, transparent, intelligible and easily accessible." — [ICO: what methods can we use](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/the-right-to-be-informed/what-methods-can-we-use-to-provide-privacy-information/); [ICO: right to be informed](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/individual-rights/right-to-be-informed/)
- **Apps specifically.** The EDPB-endorsed Article 29 WP Transparency Guidelines (WP260 rev.01) say:
  - Art. 13 information should be available from the app store before download.
  - Once installed, the information should be "never more than two taps away", for example via a "Privacy"/"Data Protection" menu option.
  - A linked notice is acceptable if it stays this reachable.
  — [WP260 rev.01 (EDPB)](https://www.edpb.europa.eu/system/files/documents/2023-09/wp260rev01_en.pdf); [IAPP summary](https://iapp.org/news/a/transparency-and-the-gdpr-practical-guidance-and-interpretive-assistance-from-the-article-29-working-party)
- **Household exemption.** GDPR Art. 2(2)(c) excludes processing "by a natural person in the course of a purely personal or household activity". Recital 18 gives examples ("correspondence and the holding of addresses, or social networking and online activity" in that context). It also says the GDPR still applies to controllers or processors who "provide the means" for such processing. Ryneš (C-212/13) narrowed the exemption where processing reaches into public space (CCTV covering a street). — [GDPRhub Art. 2](https://gdprhub.eu/Article_2_GDPR); [Handley Gill on Recital 18](https://www.handleygill.co.uk/handley-gill-blog/data-protection-personal-data-recital-18-article-2-material-scope-uk-gdpr-purely-personal-household-activity-domestic-purposes-exemption); [Inforrm on Ryneš](https://inforrm.org/2020/06/05/when-privacy-and-security-collide-the-legality-of-using-facial-recognition-security-systems-in-quasi-public-spaces-raghav-mendiratta/)
- **CCPA/CPRA.** These apply only to for-profit businesses doing business in California that meet one threshold:
  - more than $26,625,000 annual gross revenue (2026 CPI-adjusted), or
  - buying, selling or sharing data of 100,000 or more California consumers or households, or
  - 50% or more of revenue from selling or sharing data.
  — [Clym CCPA applicability 2026](https://www.clym.io/blog/ccpa-applicability-guide); [california-ccpa.org](https://california-ccpa.org/blog/ccpa-applicability-thresholds-does-my-business-comply/) (secondary sources; the statute's "business" definition requires a for-profit entity)
- **Ecosystem rules (Dalamud).** These are not law, but they bind distribution for this project. Dalamud's technical considerations say:
  - plugins "should take care to send the minimum amount of data necessary"
  - "Whenever feasible, plugins should hash information about the local player (such as the player's Content ID or name) on the client side so that a server-side data breach does not reveal information"
  - "Users must explicitly opt in to additional telemetry collection"
  - plugins "should offer the ability to connect to a user-defined backend server"
  - backend servers "should be available under an Open Source license"
  - plugins "must take care to not expose a list of other plugin users or allow an easy way to test whether a specific user is using any plugin"

  Maintainer-run servers must use TLS with CA-issued certificates and be reached by DNS hostname. Data appropriateness is judged case by case by the Plugin Approval Committee, including "how things are communicated to users". — [Dalamud: Technical Considerations](https://dalamud.dev/plugin-development/technical-considerations/)

  The restrictions page also bars collecting "account IDs of player characters beyond your own in any form". — [Dalamud: Plugin Restrictions](https://dalamud.dev/plugin-publishing/restrictions/)

### Inferences
- The relay is the operator's only processing. It handles IP, timing, session code and the plaintext display name. An EU-facing hobby relay should therefore have a short Art. 13 notice covering:
  - who runs it
  - what the relay sees
  - why it sees it (delivering sessions, security/abuse)
  - the lawful basis (legitimate interest / Recital 49)
  - how long anything is kept (ideally "not logged" or "N hours")
  - the person's rights and a contact
- That notice can be an external page if it is linked from the plugin's settings/about menu (two-tap reach) and from the repo or plugin listing. Nothing in the guidance found requires the full text in-app.
- A just-in-time line is the guidance-backed exception. The character name sent in the clear to the relay may be "unexpected" to users, so a one-line hint at the join step fits ICO/WP260 thinking. An example: "Your display name is visible to the relay; change it here".
- Under the SRB relative approach, E2EE payloads the relay cannot decrypt are arguably not personal data *for the operator*. The IP and the cleartext name still are. Hashing or omitting the cleartext name, as Dalamud encourages, would shrink the operator's duties.
- Right to erasure (Art. 17) runs against a controller for data it holds. Campaign data on the DM's PC and the player's local UUID never reach the operator, so the operator has no erasure obligation for them.
- The DM keeping friends' character names and UUIDs for a hobby game fits Recital 18's "purely personal or household activity". The project still "provides the means" (Recital 18), which is a reason, though not a legal mandate, to make local deletion easy.
- Local deletion controls for local data are good practice and match Dalamud's spirit. No source found makes them a legal obligation for the operator.

### Gaps
- I found no authority on whether an FFXIV character name specifically is personal data. The conclusion rests on general "online identifier/username" guidance and the fact that FFXIV names are unique per world and publicly searchable on Lodestone. The Lodestone part is my knowledge, not cited.
- I did not verify whether the CJEU has ruled on the household exemption for game or hobby group tools. None was found.
- I found no worked small open-source privacy notice examples in the time available. Syncthing's relay documentation (section 2) is the closest analogue.
- UK GDPR follows the same Art. 2/13 structure. The Data (Use and Access) Act 2025 changes were not researched.

## 2. Disclosure UX norms: just-in-time vs. policy, notice fatigue, and where security explanations live

### Takeaway
Regulators (FTC 2013, ICO, WP260) all recommend a full policy *plus* short contextual notices at the moment of unexpected or sensitive collection. Users largely do not read policies, and they click through generic warnings at high rates when the warning design is weak. Reference products put their security explanation in docs and FAQs. In-app they show it as an optional settings screen or a status indicator, not as a mandatory explanatory step.

### Cited Findings
- **FTC.** The 2013 staff report "Mobile Privacy Disclosures: Building Trust through Transparency" recommended just-in-time disclosures and affirmative express consent before collecting sensitive data such as geolocation. It also suggested a privacy "dashboard". — [FTC press release](https://www.ftc.gov/news-events/news/press-releases/2013/02/ftc-staff-report-recommends-ways-improve-mobile-privacy-disclosures)
- **ICO.** Recommends layered notices, dashboards and just-in-time notices, with extra just-in-time notifications where apps process data in unexpected or more intrusive ways. — [ICO methods](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/the-right-to-be-informed/what-methods-can-we-use-to-provide-privacy-information/)
- **Policy reading.** Obar & Oeldorf-Hirsch (Information, Communication & Society, 2020) studied 543 participants:
  - 74% skipped the privacy policy via "quick join"
  - those who opened it spent about 73 seconds on average, against an estimated 29–32 minutes needed
  — [Taylor & Francis](https://www.tandfonline.com/doi/abs/10.1080/1369118X.2018.1486870); [SSRN](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2757465)
- **Warnings.** Akhawe & Felt, "Alice in Warningland" (USENIX Security 2013), observed more than 25M warning impressions:
  - users clicked through 70.2% of Chrome SSL warnings and 33.0% of Firefox's
  - malware/phishing warning clickthrough was 9–23%
  - so warning design strongly affects behaviour, and warnings are not uniformly ignored
  — [USENIX paper](https://www.usenix.org/system/files/conference/usenixsecurity13/sec13-paper_akhawe.pdf)
- **Syncthing.** The docs ("Security Principles") explain that each device's certificate fingerprint is its Device ID, checked against a preset list of accepted devices. Device approval is an in-app accept prompt for a new device ID. The detailed explanation lives in the docs. The relay page says the relay "will learn the connecting device's device ID", that public relays "can be run by anyone", and that data is encrypted and "not subject to inspection by the relay". — [Syncthing security](https://docs.syncthing.net/users/security.html); [Syncthing relaying](https://docs.syncthing.net/v1.21.0/users/relaying.html)
- **Tailscale.** The trust explanation (coordination server distributes keys; Tailnet Lock removes the need to trust it) lives in docs, a blog post and a whitepaper. Tailnet Lock is opt-in. — [Tailnet Lock docs](https://tailscale.com/docs/features/tailnet-lock); [Tailscale blog](https://tailscale.com/blog/tailnet-lock)
- **WhatsApp/Telegram.** Both showed an "end-to-end encrypted" notice on chats but did not show whether the conversation was authenticated. That is a status notice, not an explanation or a verification step. — [Herzberg & Leibowitz, STAST 2016](https://dl.acm.org/doi/10.1145/3046055.3046059)

### Inferences
- The common pattern is:
  - an external privacy/security page (docs, FAQ)
  - a two-tap in-app link
  - at most a one-line contextual cue at the risky moment (here, joining with a cleartext name)
  - no mandatory in-app essays
- The evidence says long text will not be read, and generic warnings get clicked through unless they are rare and specific.
- For DungeonMasterXIV, an in-app "About privacy" link plus a single join-time hint matches norms. A blocking in-app security disclosure would be atypical.

### Gaps
- Apple App Store privacy labels and Google Play Data Safety were not researched in depth. Neither applies to a Dalamud plugin, which is distributed outside those stores, but they show the "summary label + linked policy" pattern.
- NIST guidance on notices was not located in this pass.

## 3. Out-of-band verification: do users actually compare fingerprints?

### Takeaway
Lab studies consistently find users cannot find or understand authentication ceremonies unless prompted. Real-world use is low. Mainstream products keep verification of *other people* optional. The notable 2026 move by Matrix/Element makes only *own-device* cross-signing mandatory.

### Cited Findings
- **Vaziripour et al., "Is that you, Alice?" (SOUPS 2017).** 36 participant pairs used WhatsApp, Viber and Facebook Messenger. Success at finding and completing the ceremony rose from 14% to 79% between the first phase (threat instruction) and the second (stressing the importance of the ceremony), reaching up to 96% for Viber. Times were "undesirably long". It was "inconclusive" whether users linked the ceremony to its security guarantees. — [USENIX SOUPS 2017](https://www.usenix.org/conference/soups2017/technical-sessions/presentation/vaziripour)
- **Vaziripour et al., "Action Needed!" (SOUPS 2018).** The team modified Signal with a persistent red "unverified" bar and split the ceremony into a phone-call path and an in-person QR path. This significantly reduced time to find and complete it. I could not extract exact numbers because the PDF would not parse. — [SOUPS 2018 paper](https://www.usenix.org/system/files/conference/soups2018/soups2018-vaziripour.pdf); [figure summary](https://www.researchgate.net/figure/aziripour-et-al-10-modified-Signal-by-adding-an-explicit-prompt-for-the-authentication_fig3_346777625)
- **Herzberg & Leibowitz (STAST 2016).** Evaluated WhatsApp, Viber, Telegram and Signal. They concluded the E2EE authentication mechanisms were impractical, "leaving users with only the illusion of security". — [ACM DL](https://dl.acm.org/doi/10.1145/3046055.3046059)
- **Synthesis paper.** "Secure Messaging Authentication Ceremonies Are Broken" (Herzberg, Leibowitz, Seamons, Vaziripour, Wu, Zappala) summarises the literature as follows (secondary; via search snippet of the paper):
  - only 14% of participants in one study verified key material without further explanation
  - only 29.6% of surveyed Iranian Telegram users had ever used the authentication ceremony in text chats
  — [ResearchGate](https://www.researchgate.net/publication/346777625_Secure_Messaging_Authentication_Ceremonies_Are_Broken)
- **CHI 2023 autoethnography.** "Why I Can't Authenticate" examines why the ceremony sees low adoption. — [ACM DL](https://dl.acm.org/doi/10.1145/3544548.3581508)
- **Matrix/Element.** Per MSC4153, Element clients stop sending encrypted messages to devices not cross-signed by their owner's identity, with the change rolling out in April 2026. Users are forced to verify their *own* new devices. Verifying *other users* stays optional. — [Matrix.org blog, Nov 2025](https://matrix.org/blog/2025/11/exclude-insecure-devices/); [Privacy Guides](https://www.privacyguides.org/news/2025/11/20/element-will-soon-make-verifying-your-devices-mandatory/)
- **Syncthing** puts device-ID approval in the mandatory path: a new device must be accepted. IDs are usually exchanged by copying or QR, not compared character by character. — [Syncthing security](https://docs.syncthing.net/users/security.html)
- **Tailscale** keeps signing-based verification (Tailnet Lock) opt-in. By default, users trust the coordination server to distribute keys. — [Tailnet Lock docs](https://tailscale.com/docs/features/tailnet-lock)

### Inferences
- Optional fingerprint comparison in DungeonMasterXIV would be used by very few players.
- When verification sits in the mandatory path in consumer products, it is framed as *approval of a new member*, not as *comparison of a code*. Examples are Syncthing's device accept and Element's own-device cross-signing.
- The DM approving a joiner is the analogous, already-familiar pattern.

### Gaps
- No published real-world telemetry rate for Signal safety-number verification was found. Signal does not appear to publish one, and the support page returned 403.
- The "14%" and "29.6%" figures are second-hand via the synthesis paper.

## 4. Realistic threat model for a hobby RP relay

### Takeaway
The plausible threats are:
- griefers using leaked or guessed session codes
- harassment or stalking tied to character names
- exposure of IPs to the relay operator (and to a malicious relay)

A state-level or targeted MITM by the relay operator is low likelihood. E2EE plus DM join approval and short-lived codes covers most realistic risk.

### Cited Findings
- **Relay visibility.** Analogous relay designs openly accept that the relay sees metadata (device ID, IP) but not content. Syncthing: the relay "will learn the connecting device's device ID", anyone can run one, and the data is encrypted and "not subject to inspection by the relay". — [Syncthing relaying](https://docs.syncthing.net/v1.21.0/users/relaying.html)
- **Operator as key distributor.** Without Tailnet Lock, Tailscale's coordination server distributes public keys, so a malicious or compromised server could insert keys. Tailnet Lock exists so that "even if Tailscale were malicious or Tailscale infrastructure hacked, attackers can't send or receive traffic". This is the same MITM-by-relay class as a session relay that brokers key exchange. — [Tailnet Lock docs](https://tailscale.com/docs/features/tailnet-lock); [blog](https://tailscale.com/blog/tailnet-lock)
- **Fake-key attacks** on E2EE messengers are a recognised class. Research proposes automatic detection because users rarely verify manually. — [Automatic Detection of Fake Key Attacks in Secure Messaging, CCS 2022](https://dl.acm.org/doi/10.1145/3548606.3560588?cid=81100506951)
- **Ecosystem privacy concerns.** The Dalamud ecosystem specifically worries about:
  - revealing who uses a plugin
  - collecting other players' account IDs
  - server-side breaches revealing player names (hence the hashing advice)

  That makes character-name exposure a recognised community-level privacy concern. — [Dalamud technical considerations](https://dalamud.dev/plugin-development/technical-considerations/); [Dalamud restrictions](https://dalamud.dev/plugin-publishing/restrictions/)

### Inferences
Threats mapped to controls (my analysis, built on the sources above):

| Threat | Likelihood (hobby RP) | Main controls |
|---|---|---|
| Griefer joins with a leaked or guessed code | High: codes get pasted in Discord or party chat | DM approval of each joiner, short-lived or single-use codes, rate-limiting code guesses at the relay, DM kick/ban |
| Stalking or harassment via character names | Medium: names are public in-game, but the plugin could link RP group membership and schedule | Send no cleartext name to the relay (or hash it, per Dalamud), let players choose a display alias, no public lobby or user lists (Dalamud rule) |
| Relay operator reads content | Low, and mitigated | E2EE; open-source relay; self-hostable relay (Dalamud "user-defined server") |
| Malicious relay MITM (key substitution) | Low (needs an operator or compromise motivated to target a hobby game) | Optional out-of-band fingerprint, or binding the key to the session code or DM approval. Users will rarely verify (section 3), so the residual risk is accepted, as Tailscale does by default |
| IP exposure / DDoS between players | Low to medium: relay architecture means peers do not see each other's IPs | Relay keeps peer IPs private from each other; operator retains IPs minimally (Recital 49 basis) |
| DoS against the relay itself | Medium for any public service | Rate limits; self-host option |
| Local data on a shared or compromised PC | Low; outside the operator's control | Local delete controls are a convenience, not a legal requirement |

- The cleartext display name at join is the one design element where privacy-law exposure, Dalamud norms and stalking risk all point the same way. Minimising or hashing it reduces both the operator's GDPR footprint and the main realistic harm.

### Gaps
- I found no published incident data on griefing or stalking specific to FFXIV RP plugins or relays. The likelihood ratings are judgement, not sourced.
- I found no usable-security study on session-code (PIN-style) join flows for games specifically.
