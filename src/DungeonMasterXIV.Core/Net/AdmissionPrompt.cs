namespace DungeonMasterXIV.Net;

/// <summary>Supplies the headline, comparability note and confirmation choice the host sees for a join request.</summary>
public static class AdmissionPrompt
{
    public static AdmissionAction Favoured(PendingAdmission request) => AdmissionAction.None;

    public static string Headline(PendingAdmission request) =>
        request.Relink is { Matched: true, Label: { Length: > 0 } label }
            ? $"{request.DisplayName} ({request.PeerCode}) is asking to relink as {label}"
            : $"{request.DisplayName} ({request.PeerCode}) is asking to join";

    public static bool OffersConfirmation(PendingAdmission request) =>
        request is not null
        && request.Comparability != ComparabilityEvidence.EstablishedIncapable;

    public static string ComparabilityNote(PendingAdmission request) =>
        request?.Comparability switch
        {
            ComparabilityEvidence.NotEstablished => NotEstablished,
            ComparabilityEvidence.EstablishedIncapable => Incapable,
            _ => string.Empty,
        };

    private const string NotEstablished =
        "We have not heard whether this player's client can show them a code to read back. That is "
        + "neither a yes nor a no - a quick decision usually beats the message. Compare out of band "
        + "as usual, and only tick the box if they actually read the code back to you.";

    private const string Incapable =
        "This player's client reported that it cannot show them a code, so there is nothing for them "
        + "to read back and nothing to confirm. Admitting them leaves this session unprotected "
        + "against someone sitting in the middle of it.";
}
