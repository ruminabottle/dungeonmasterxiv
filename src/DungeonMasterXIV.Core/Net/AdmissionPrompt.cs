namespace DungeonMasterXIV.Net;

/// <summary>
/// What the DM's admission prompt says about a pending request.
/// </summary>
/// <remarks>
/// <para>
/// In Core so the wording is under test rather than only looked at, and so the one place a relink
/// differs from a join is a string this file returns.
/// </para>
/// <para>
/// <b>A resolved relink changes this sentence and nothing else.</b> It does not remove the
/// fingerprint comparison, does not pre-confirm it, does not add a shortcut, and does not admit
/// anyone — R-1.5 requires the DM to approve every relink, every session. If a future change makes
/// a relink take fewer steps than a join, this type is not where that happens and it must not
/// become where it happens.
/// </para>
/// </remarks>
public static class AdmissionPrompt
{
    /// <summary>
    /// The headline for a request. Names the participant only when one was actually resolved from
    /// the campaign store — never from anything the requesting client sent.
    /// </summary>
    /// <param name="request">The pending request.</param>
    public static string Headline(PendingAdmission request) =>
        request.Relink is { Matched: true, Label: { Length: > 0 } label }
            ? $"{request.PeerCode} is asking to relink as {label}"
            : $"{request.PeerCode} is asking to join";
}
