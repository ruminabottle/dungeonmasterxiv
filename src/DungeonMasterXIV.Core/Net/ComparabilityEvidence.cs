namespace DungeonMasterXIV.Net;

/// <summary>
/// What the host has ESTABLISHED about a joining client's ability to compare the fingerprint, at the
/// moment of the decision (R-1.3a-iv).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a fact about the HOST'S GROUNDS, not about the joiner.</b> That distinction is what
/// makes three states necessary rather than a two-valued fact read differently: <i>not established</i>
/// is a statement about what the host knows, and no boolean about the joiner can express it. The
/// Spec Owner's words: <i>"no reading of a boolean about the joiner can express it"</i> (SQ-59).
/// </para>
/// <para>
/// <b>It replaces a <c>bool</c> that collapsed two of these into one.</b> The previous model was
/// <c>JoinerCouldCompare</c>, defaulting <c>false</c> — so <i>not established</i> and <i>established
/// incapable</i> were the same value, and any guard reading it asserted the second whenever it meant
/// the first. That is precisely the assertion <b>A-1.2o</b> fails a build for making, and it is why
/// replacing this type was in scope rather than tidying.
/// </para>
/// <para>
/// <b>THE ZERO VALUE IS LOAD-BEARING.</b> <see cref="NotEstablished"/> is 0 so that
/// <c>default</c> — a field never assigned, a struct zeroed, a row read from an older store — means
/// <i>we do not know</i>. A-1.2q fails a build whose default means <i>incapable</i>
/// <b>by construction</b>, and a zero value is the one thing no caller can decline to supply.
/// </para>
/// <para>
/// <b>A state is entered only on POSITIVE EVIDENCE — never from silence, a timeout, or elapsed
/// time.</b> "We waited and heard nothing" is <see cref="NotEstablished"/> held longer, not a
/// transition. qa-2 measured a 171ms admission producing zero receipts from a joiner that could
/// compare, which is the proof that silence carries no information here.
/// </para>
/// </remarks>
public enum ComparabilityEvidence
{
    /// <summary>
    /// Neither has been established. <b>The default and the initial state</b>, and the resting state
    /// for most sessions — a fast admission decides before any receipt could arrive.
    /// </summary>
    NotEstablished = 0,

    /// <summary>
    /// Positive evidence the joining client held the host key and could render the fingerprint
    /// (R-1.3a-iii). Established by the client's receipt and by nothing else.
    /// </summary>
    EstablishedCapable = 1,

    /// <summary>
    /// Positive evidence the joining client could NOT compare.
    /// </summary>
    /// <remarks>
    /// <b>NOTHING PRODUCES THIS TODAY, AND THAT IS RECORDED RATHER THAN OVERLOOKED.</b> The Spec
    /// Owner checked the one remaining candidate — the protocol version — and it does not work:
    /// D-14 makes <see cref="WireMessageType.JoinPending"/> additive, so a client that ignores it
    /// carries the SAME version, connects normally under R-1.7b, and is refused by nothing.
    /// <para>
    /// So A-1.2f's <b>suppression is unreachable</b>: the control is qualified-or-unqualified and
    /// never suppressed. This member stays in the model because a future signal could produce it —
    /// but <b>nothing may write it now</b>, and a build that suppresses on it is asserting a state
    /// with no producer, which is what A-1.2o fails.
    /// </para>
    /// <para>
    /// <b>Nothing reaches this by exhausting <see cref="NotEstablished"/>.</b> No timeout, no
    /// deadline, no number of ticks. If a future change wants to produce this, it needs a signal
    /// that carries the fact, not a clock that runs out of patience.
    /// </para>
    /// </remarks>
    EstablishedIncapable = 2,
}
