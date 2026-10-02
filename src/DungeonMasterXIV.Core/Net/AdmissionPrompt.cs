namespace DungeonMasterXIV.Net;

/// <summary>Supplies the headline and favoured choice the host sees for a join request.</summary>
public static class AdmissionPrompt
{
    public static AdmissionAction Favoured(PendingAdmission request) => AdmissionAction.None;

    public static string Headline(PendingAdmission request) =>
        request.Relink is { Matched: true, Label: { Length: > 0 } label }
            ? $"{request.DisplayName} ({request.PeerCode}) is asking to relink as {label}"
            : $"{request.DisplayName} ({request.PeerCode}) is asking to join";
}
