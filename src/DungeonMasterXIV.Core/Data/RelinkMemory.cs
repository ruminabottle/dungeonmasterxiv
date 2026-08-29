using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Data;

/// <summary>
/// One session code this client has been admitted under, and the participant the host said it was.
/// </summary>
/// <remarks>
/// <b>NO TIMESTAMP, AND ITS ABSENCE IS THE POINT (A-1.9d).</b> Nothing here records when the id was
/// stored, because a build that ages one out <b>on any timer</b> fails — D-8 guards linkage across
/// campaigns, not duration within one, so a clock protects nothing it exists to protect and breaks
/// relink for a campaign that meets monthly. A date field would also be the obvious thing for a
/// future change to age on. <b>The field that does not exist cannot be expired.</b>
/// </remarks>
public sealed class RememberedParticipant
{
    /// <summary>The session code this client joined under. What the player typed.</summary>
    public string SessionCode { get; set; } = string.Empty;

    /// <summary>The participant the host created for this client there (R-1.5c).</summary>
    public Guid ParticipantId { get; set; }
}

/// <summary>
/// What this client remembers about who it is, per session code — the joiner's half of relink
/// (R-1.5b).
/// </summary>
/// <remarks>
/// <para>
/// <b>KEYED ON THE SESSION CODE, because that is the only handle a JOINER has.</b> R-1.5b says the
/// stored UUID is <i>"bound under a session code"</i>. Campaigns are the host's concept and live in
/// the DM's <c>CampaignStore</c>; a joining client is never told one exists. The code is what the
/// player typed and what they will type again.
/// </para>
/// <para>
/// <b>ONE ENTRY PER CODE, and a second admission under the same code REPLACES it.</b> The host is
/// authoritative for who a participant is (D-3), so if it says we are someone new under a code we
/// already knew, it is right and we were stale — a host whose campaign was deleted and recreated is
/// exactly that case. Keeping both would leave this client holding two claims for one code and no
/// rule for choosing.
/// </para>
/// <para>
/// <b>RETENTION IS UNBOUNDED AND THERE IS NO TIMER ANYWHERE IN THIS TYPE (A-1.9d).</b> R-1.5b:
/// <i>"if a number appears in this requirement, something has gone wrong."</i> Nothing here counts,
/// ages, prunes or caps.
/// </para>
/// <para>
/// <b>AND NOTHING HERE SENDS (A-1.9e).</b> This type has no transport, no coordinator and no way to
/// reach one. Deleting is a local act by construction rather than by a caller remembering not to
/// announce it — a notification would manufacture exactly the signal linking a deletion to a player
/// that R-1.5b refuses.
/// </para>
/// </remarks>
public sealed class RelinkMemory
{
    /// <summary>
    /// What is stored, in the order it was learned. <b>Serialised, so this is the on-disk shape.</b>
    /// </summary>
    public List<RememberedParticipant> Remembered { get; set; } = new();

    /// <summary>
    /// Everything this client is storing, for the player to read before deciding to delete any of it
    /// (A-1.9b).
    /// </summary>
    /// <remarks>
    /// <b>A-1.9b makes listing a precondition of deleting, not a companion to it:</b> <i>"a build
    /// offering deletion without first showing what is stored FAILS — you cannot meaningfully delete
    /// what you cannot see."</i> So this exists for the UI to render and is not an afterthought of
    /// the delete path.
    /// <para>
    /// <b>BUG-146: A METHOD, NOT A PROPERTY, AND THAT IS THE WHOLE FIX.</b> As a property this was a
    /// second PUBLIC GETTABLE view of <see cref="Remembered"/>, so the serialiser wrote the list
    /// twice — and on load Newtonsoft populates a read-only collection property by ADDING to it, into
    /// the same list. Every save/load DOUBLED the memory: 1, 2, 4, 8, 16, 32, and about a million
    /// after twenty ordinary settings cycles.
    /// </para>
    /// <para>
    /// <c>[JsonIgnore]</c> is the usual remedy and is not available: this project does not reference
    /// Newtonsoft, and adding a serialiser dependency to the domain to describe how the domain is
    /// serialised is the wrong direction. A method is not a serialisable member at all, so the defect
    /// is removed by construction rather than by an annotation someone must remember to copy onto the
    /// next alias.
    /// </para>
    /// <para>
    /// <b>Kept rather than deleted, deliberately.</b> Dropping it and pointing the UI at
    /// <see cref="Remembered"/> also fixes the doubling, and hands the render path a mutable
    /// <see cref="List{T}"/> with a public setter — the read-only view is the thing A-1.9b's
    /// "show it before you delete it" actually leans on.
    /// </para>
    /// </remarks>
    /// <returns>Everything stored, in the order it was learned.</returns>
    public IReadOnlyList<RememberedParticipant> All() => Remembered;

    /// <summary>
    /// The participant this client is under <paramref name="code"/>, or null if it has never been
    /// admitted there.
    /// </summary>
    /// <remarks>
    /// <b>Null is the ordinary answer and means "join as a stranger".</b> A first join, a code never
    /// seen, and a code the player has deleted are all the same to a caller — which is what makes
    /// deletion actually undo the relink rather than merely hide it.
    /// </remarks>
    /// <param name="code">The session code about to be joined.</param>
    public Guid? IdFor(SessionCode code) =>
        Remembered.FirstOrDefault(entry => Matches(entry, code))?.ParticipantId;

    /// <summary>
    /// Records that the host under <paramref name="code"/> told this client it is
    /// <paramref name="participantId"/>. Returns whether anything changed.
    /// </summary>
    /// <remarks>
    /// <b>Returns whether it CHANGED so the caller can avoid writing to disk every frame.</b> This is
    /// reached from the framework update, which runs at frame rate; a save on every tick would be a
    /// write amplification bug wearing the clothes of a feature.
    /// </remarks>
    /// <param name="code">The session code this client was admitted under.</param>
    /// <param name="participantId">The participant the host created for it.</param>
    public bool Remember(SessionCode code, Guid participantId)
    {
        var existing = Remembered.FirstOrDefault(entry => Matches(entry, code));

        if (existing is not null)
        {
            if (existing.ParticipantId == participantId)
            {
                return false;
            }

            existing.ParticipantId = participantId;
            return true;
        }

        Remembered.Add(new RememberedParticipant
        {
            SessionCode = code.Value,
            ParticipantId = participantId,
        });

        return true;
    }

    /// <summary>
    /// Forgets what this client is under <paramref name="code"/>. Returns whether anything was
    /// removed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ENTRY goes, not just the id (A-1.9b).</b> <i>"After deletion no file on their disk
    /// contains that UUID."</i> Blanking the id while keeping the row would leave the code behind as
    /// a record that this client was in that session — a smaller disclosure than the UUID and still
    /// one the player asked to be rid of.
    /// </para>
    /// <para>
    /// <b>The caller must persist.</b> Returning <c>true</c> and never being written is a deletion
    /// the player was shown and the disk never heard about, which is worse than refusing to delete:
    /// the id comes back on next launch and nothing says why.
    /// </para>
    /// </remarks>
    /// <param name="code">The session code to forget.</param>
    public bool Forget(SessionCode code) =>
        Remembered.RemoveAll(entry => Matches(entry, code)) > 0;

    // Ordinal, and codes are compared as the STRINGS they were typed and stored as. SessionCode
    // validates on the way in, so anything on disk that no longer parses is data this build cannot
    // act on -- it stays listed, so a player can still see and delete it, and never matches a join.
    private static bool Matches(RememberedParticipant entry, SessionCode code) =>
        string.Equals(entry.SessionCode, code.Value, StringComparison.Ordinal);
}
