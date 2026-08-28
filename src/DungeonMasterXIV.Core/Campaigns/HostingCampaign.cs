using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>
/// Which campaign the session being hosted belongs to (A-1.9i, A-1.9j).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the connection that did not exist.</b> <c>Plugin.cs</c> constructed a
/// <see cref="CampaignStore"/> and a session coordinator and never joined them, so there was no
/// production path from "I am hosting BCDFGH" to "this is campaign X" — and therefore nothing for
/// R-1.5c to mint a participant into. <c>AddParticipant</c> had zero production callers for that
/// reason.
/// </para>
/// <para>
/// <b>It lives beside the campaigns, NOT on the session layer, and that is deliberate.</b>
/// <c>Net/</c> contains no campaign id anywhere and this does not add one: a campaign is a
/// host-side, persisted, D-8-scoped concept and the session layer is a transport. Putting the
/// association here keeps the coordinator campaign-free, which is the property that lets the same
/// session code be reused by an unrelated campaign later (R-1.2a) without anything having to
/// forget.
/// </para>
/// <para>
/// <b>Hosting NEVER waits on this and never fails because of it (A-1.9i).</b> <see cref="StartFor"/>
/// always returns a campaign — the chosen one if the DM picked, a fresh one if they did not. There
/// is no branch in which a DM presses the host button and is asked a question first. That is the
/// criterion in its own words: blocking, prompting-and-waiting, or refusing FAILS.
/// </para>
/// </remarks>
public sealed class HostingCampaign
{
    private readonly CampaignStore _store;

    /// <param name="store">Where campaigns live.</param>
    public HostingCampaign(CampaignStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <summary>
    /// Which prior campaign the DM has picked to resume, or null to start a new one.
    /// </summary>
    /// <remarks>
    /// <b>Null is a valid answer and is the default (A-1.9i).</b> "Not choosing" means a new
    /// campaign, so a DM who never looks at the control is never blocked by it.
    /// </remarks>
    public Guid? Chosen { get; set; }

    /// <summary>The campaign the running session belongs to, or null when not hosting.</summary>
    public Campaign? Current { get; private set; }

    /// <summary>
    /// Campaigns the DM could resume instead of starting a new one (A-1.9j).
    /// </summary>
    /// <remarks>
    /// Empty on a first run, which is what lets the host flow stay a single action for a DM with
    /// nothing to resume — the control is absent rather than empty.
    /// </remarks>
    public IReadOnlyList<Campaign> Resumable => _store.Campaigns;

    /// <summary>
    /// Settles which campaign this session belongs to and returns it. Never null, never blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A stale choice degrades to a new campaign rather than failing.</b> If the picked campaign
    /// has been deleted since it was picked, this creates one — because the alternative is refusing
    /// to host, and A-1.9i says refusing FAILS. The DM loses a resume they can retry; they do not
    /// lose the ability to start a game.
    /// </para>
    /// <para>
    /// <b>No preferred code is passed.</b> The code is assigned by the session that is starting and
    /// R-1.2a lets it change; recording one here would be recording a value that is not yet true.
    /// </para>
    /// </remarks>
    public Campaign StartFor()
    {
        Current = (Chosen is { } chosen ? _store.Find(chosen) : null) ?? _store.Create(null);
        return Current;
    }

    /// <summary>
    /// Records an admitted player as a participant of the running campaign (R-1.5c, R-1.6).
    /// </summary>
    /// <param name="label">What the DM calls them locally. May be a character name.</param>
    /// <param name="alreadyAParticipant">
    /// True when this admission RESOLVED a relink, so the person already has a participant here.
    /// </param>
    /// <returns>The new participant, or null when nothing was recorded.</returns>
    /// <remarks>
    /// <para>
    /// <b>This is what makes the resume offer honest.</b> Until it existed,
    /// <see cref="CampaignStore.AddParticipant"/> had ZERO production callers, so a campaign was
    /// never anything but a name and a date — and the picker offered a DM continuity the build could
    /// not deliver. A campaign IS its roster: <see cref="Campaign"/> holds no notes and no encounter
    /// state, so an empty participant list is an empty campaign.
    /// </para>
    /// <para>
    /// <b>D-8 permits the label HERE and would forbid it elsewhere.</b> A-1.11 was narrowed on
    /// 2026-08-27 to cover what LEAVES the machine — exports and relay traffic — and D-8 explicitly
    /// allows real character names in the DM's own file. <see cref="CampaignParticipant.ParticipantId"/>
    /// is minted per campaign and derived from nothing, so identifiers stay uncorrelated across
    /// campaigns; the label is not rotated and that limit is stated on the type rather than here.
    /// </para>
    /// <para>
    /// <b>A resolved relink records NOTHING, and that arm is currently unreachable.</b> Nothing yet
    /// tells a joiner its participant id, so no client can present a claim and
    /// <c>PendingAdmission.IsRelink</c> is always false in the shipped build. The guard is here
    /// anyway because the alternative is a returning player silently acquiring a SECOND participant
    /// in the same campaign the moment relink starts working — a defect that would arrive with
    /// somebody else's change and look like theirs.
    /// </para>
    /// <para>
    /// <b>Not hosting means nothing to record.</b> Returns null rather than throwing: an admission
    /// with no running session is not a state this can fix, and it must not take the session down.
    /// </para>
    /// </remarks>
    public CampaignParticipant? Record(string label, bool alreadyAParticipant = false)
    {
        if (alreadyAParticipant || Current is null)
        {
            return null;
        }

        return _store.AddParticipant(Current.CampaignId, label);
    }

    /// <summary>The session ended, so no campaign is current. The campaign itself is untouched.</summary>
    public void Ended() => Current = null;
}
