using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Turns a <see cref="SessionContent"/> into the bytes that go inside the seal, and back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separate from <see cref="EnvelopeCodec"/> on purpose.</b> That one encodes what the relay
/// reads in order to route; this encodes what only session members can read. Two codecs make the
/// boundary a type rather than a convention — a field added to the wrong one is a visible mistake
/// instead of a quiet disclosure, which is the class of error that put a display name in the clear.
/// </para>
/// <para>
/// <b>Decoding never throws and never trusts.</b> These bytes arrive from another client, and while
/// the seal proves they came from someone holding the shared key, it does not make their contents
/// well-formed. Malformed content is refused the way a malformed envelope is: a false return and no
/// exception, because a participant sending nonsense must not be able to end anyone's session.
/// </para>
/// <para>
/// <b>Two fields of a roster entry are vetted here — <see cref="RosterEntry.DisplayName"/> and
/// <see cref="RosterEntry.PeerCode"/> — and naming them is the point.</b> The previous version of
/// this paragraph said every display name goes through <see cref="DisplayName"/> "and this is the
/// only door". That was narrowly true and read as a claim about the whole record, so a reviewer met
/// it and was told the boundary was closed. It was not: <c>PeerCode</c> passed through untouched,
/// and the forged <c>"Code to compare"</c> line the name gate exists to stop simply moved one field
/// over (BUG-57).
/// </para>
/// <para>
/// <b>Why that hole was not obvious, which is the part worth carrying.</b> A peer code is derived
/// host-side by <c>AdmissionControl.PeerCodeFor</c>, so within one client it has always been
/// trustworthy. <b>The roster is the first place a peer code arrives from ANOTHER client</b> — the
/// field did not change, the direction it travels did.
/// </para>
/// <para>
/// The seal authenticates the sender; it says nothing about what they sent. Both fields are
/// untrusted data rendered beside a fingerprint, so a multi-line one could draw a forged
/// <c>"Code to compare"</c> line — displacing the fingerprint, which the D-8 gate denies on sight.
/// Validating at the boundary rather than at each consumer is the difference between a rule and a
/// reminder: a later reader of a decoded <see cref="SessionContent"/> must not have to remember.
/// </para>
/// <para>
/// <b><see cref="RosterEntry.Role"/> is not vetted, and that is a finding rather than an omission —
/// an unmentioned field is what let <c>PeerCode</c> through.</b> It is an <c>enum</c> over
/// <c>int</c> with no string converter, and this was MEASURED rather than reasoned: a numeric value
/// out of range decodes to an undefined member — <c>99</c> and <c>int.MaxValue</c> both arrive, and
/// <c>ToString()</c> gives back the digits with no newline — while every STRING form, including
/// <c>"DungeonMaster\nCode to compare: FORGED"</c>, is refused at decode outright because the
/// deserialiser throws and <c>TryDecode</c> returns false. So Role cannot carry text and cannot
/// forge a line. What it can do is present a value matching no case, which is a rendering question
/// for whoever builds T-14 and not an injection one.
/// </para>
/// </remarks>
public static class SessionContentCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The same document with every entry put through the types that own its two fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two fields deliberately fail in OPPOSITE directions, and this is the one place both
    /// answers are visible at once.</b> A name the join path would refuse degrades to
    /// <see cref="DisplayName.Unstated"/> and <b>the participant stays in the roster</b> — exactly
    /// what the admission prompt already does, because dropping them would let a malformed name
    /// erase somebody from the session, a worse outcome than showing them unnamed.
    /// </para>
    /// <para>
    /// <b>A bad peer code DROPS the entry instead.</b> A display name is a label; a peer code is the
    /// IDENTITY — it is what tells two participants with the same name apart (A-1.2d) — so an entry
    /// whose code is unusable identifies nobody, and degrading it would manufacture a participant
    /// rather than remove a forgery. The roster is host-authored and sealed, so a malformed code
    /// means our own encoder is broken or a keyholder is forging, and dropping is the safe answer to
    /// both. That is why <see cref="PeerCode"/> has no <c>OrNone</c> and
    /// <see cref="DisplayName.OrNone"/> exists.
    /// </para>
    /// <para>
    /// <b>The shape rule moved to <see cref="PeerCode.TryParse"/> and is no longer restated here.</b>
    /// BUG-57's hotfix vetted the code at this one door, which was right for a hotfix and wrong as
    /// the end state — a point-vet leaves every other door open. The rule it applied is unchanged:
    /// the shape <c>AdmissionControl.PeerCodeFor</c> emits, deliberately not
    /// <see cref="SessionCode.TryParse"/>, which strips hyphens and upper-cases so a pasted code
    /// works and would therefore accept <c>"PEE-R3"</c> that the product never generated.
    /// </para>
    /// </remarks>
    private static SessionContent Vetted(SessionContent content, out int dropped)
    {
        dropped = 0;

        if (content.Roster is null)
        {
            return content;
        }

        var kept = content.Roster
            .Where(entry => PeerCode.TryParse(entry.PeerCode, out _))
            .Select(entry => entry with
            {
                DisplayName = DisplayName.OrNone(entry.DisplayName).Value,
            })
            .ToList();

        // Vetted still only decides; it does not announce. It reports HOW MANY it removed and the
        // caller decides whether anyone hears about it — this stays the door rather than becoming
        // the diagnostics layer (BUG-70).
        dropped = content.Roster.Count - kept.Count;
        // EVERY SECTION IS CARRIED FORWARD EXPLICITLY, AND A NEW ONE MUST BE ADDED HERE.
        //
        // This REBUILDS the document rather than editing it, because Roster is init-only. So a
        // section added to SessionContent and not added to this line is SILENTLY DROPPED ON DECODE
        // — the sender sets it, the wire carries it, the receiver never sees it, and nothing fails.
        // That is the same shape as the peer-code hole this method was written to close (BUG-57):
        // vetting that quietly deletes what it does not recognise.
        //
        // ASectionOtherThanTheRosterSurvivesVetting is the guard. It fails if a future section is
        // added to the type and forgotten here, which is the only moment anyone would notice.
        return new SessionContent
        {
            Roster = kept,
            ClosingAtUtcTicks = content.ClosingAtUtcTicks,
            Leaving = content.Leaving,
        };
    }

    /// <summary>Serialises <paramref name="content"/> for sealing.</summary>
    /// <param name="content">The document to encode.</param>
    public static byte[] Encode(SessionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return JsonSerializer.SerializeToUtf8Bytes(content, Options);
    }

    /// <summary>
    /// Reads content that has already been opened, or reports that it is not content.
    /// </summary>
    /// <param name="plaintext">Bytes returned by <see cref="SessionCipher.Open"/>.</param>
    /// <param name="content">The decoded document, or null.</param>
    /// <param name="log">
    /// Where a stripped roster entry is reported. Optional, and null is the silent case: the
    /// entry is refused either way, so this decides whether a developer finds out, not whether
    /// the door holds (BUG-70). The rejected value is deliberately never written — a log is the
    /// artefact most likely to be pasted into a bug report, so echoing an attacker-chosen
    /// string here would be a disclosure decision, not a formatting one.
    /// </param>
    /// <returns>Whether the bytes were a document this build understands.</returns>
    public static bool TryDecode(
        byte[] plaintext,
        out SessionContent? content,
        ISessionTransportLog? log = null)
    {
        content = null;

        if (plaintext is null || plaintext.Length == 0)
        {
            return false;
        }

        try
        {
            content = JsonSerializer.Deserialize<SessionContent>(plaintext, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        if (content is null)
        {
            return false;
        }

        content = Vetted(content, out var dropped);

        // BUG-70. The drop itself is right for both of its causes; the SILENCE was right for only
        // one. A forged entry rejected is nothing to announce — but the other cause is that OUR OWN
        // ENCODER wrote a code it cannot parse back, and then we delete a genuine participant to
        // hide our own bug and nothing anywhere says so. This cannot tell the two apart at the point
        // of drop, so it reports the fact and leaves the reading to whoever is looking.
        //
        // THE COUNT, NEVER THE VALUE. The rejected code is a string somebody else chose, and a log
        // is the artifact most likely to end up pasted into a bug report.
        if (dropped > 0)
        {
            log?.Warning(
                $"Dropped {dropped} roster {(dropped == 1 ? "entry" : "entries")} whose peer code "
                + "this build cannot have produced. Either this client's encoder is writing codes it "
                + "cannot parse back, or a keyholder is sending forged ones. The rejected value is "
                + "deliberately not recorded here.");
        }

        return true;
    }
}
