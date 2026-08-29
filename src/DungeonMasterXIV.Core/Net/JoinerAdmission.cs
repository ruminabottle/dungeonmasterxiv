using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What a host is told while a joiner is trying to get in.
/// </summary>
/// <remarks>
/// <para>
/// <b>These two are null together, always, and the file said so twice before they shared a type.</b>
/// Both members carried the same sentence — <i>"null when there is nobody to tell, which is every
/// joiner-only client"</i> — because both are things only a HOST has anybody to tell. A joiner-only
/// client now says that once, as <c>default</c>, instead of passing two nulls that a reader has to
/// check are null for the same reason.
/// </para>
/// <para>
/// <b>They are grouped by WHO IS TOLD, not by what the two mean, and the difference matters.</b>
/// <see cref="OnJoinRequest"/> is a request to act on; <see cref="OnComparabilityReceipt"/> is a
/// capability report that R-1.3a-iii forbids acting on. Putting them in one type asserts they arrive
/// at one collaborator — both are supplied from the admission side at
/// <c>SessionCoordinator.Tick</c> — and asserts nothing about them answering one question. They stay
/// two delegates; nothing is merged, and the rule about what may be concluded from a receipt stays
/// on the receipt.
/// </para>
/// <para>
/// <b>Not folded in with either content door (DMXENG-59).</b> This pair shares its nullity condition
/// with <see cref="MemberAuthoredContent"/> — both are host-only — so co-nullity alone would permit
/// merging them. The D-3 boundary that separates the two doors is a stronger claim than co-nullity,
/// and it wins: see <see cref="InboundHandlers"/>.
/// </para>
/// </remarks>
/// <param name="OnJoinRequest">
/// Called with the joiner's public key, self-declared name, and the participant id it CLAIMS, for
/// each inbound <see cref="WireMessageType.JoinRequest"/>, when this client is a host. Null when
/// there is nobody to tell, which is every joiner-only client (BUG-42).
/// <para>
/// <b>The claim travels as the raw string it arrived as (R-1.5, T-37).</b> This layer decodes and
/// routes; deciding whether a claimed participant is one this campaign knows needs the campaign,
/// which is not Core's to look up — see <see cref="SessionCapabilities.RelinkSource"/>. Null means
/// no claim was made, which is every first-time join.
/// </para>
/// </param>
/// <param name="OnComparabilityReceipt">
/// Called with the joiner's public key when that joiner reports it held the host key and could
/// render the fingerprint (R-1.3a-iv, BUG-75). Null when there is nobody to tell, which is every
/// joiner-only client — only a host keeps a record this can establish anything on.
/// <para>
/// <b>It carries a CAPABILITY, never a comparison.</b> R-1.3a-iii forbids the second: an
/// acknowledgement of the human act would ride the channel an attacker controls, so it is forgeable
/// exactly when it matters. Its ABSENCE establishes nothing either — a fast admission (A-1.2p)
/// decides before any receipt could arrive, which is why
/// <see cref="ComparabilityEvidence.NotEstablished"/> is a state and not a false.
/// </para>
/// </param>
public readonly record struct JoinerAdmission(
    Action<byte[], DisplayName, string?>? OnJoinRequest = null,
    Action<byte[]>? OnComparabilityReceipt = null);
