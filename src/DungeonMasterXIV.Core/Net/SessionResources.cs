using System;

namespace DungeonMasterXIV.Net;

/// <summary>Holds host admission, member and recording state; clears it, the inbox and grace when hosting stops.</summary>
internal sealed class SessionResources
{
    private readonly AdmissionControl _admissions;
    private readonly AdmissionInbox _inbox;
    private readonly Func<GraceWindow> _grace;

    public SessionResources(
        AdmissionControl admissions,
        AdmissionInbox inbox,
        Func<GraceWindow> grace,
        MemberContentKeys memberKeys,
        MemberContentReceipts memberContent)
    {
        ArgumentNullException.ThrowIfNull(admissions);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(grace);
        ArgumentNullException.ThrowIfNull(memberKeys);
        ArgumentNullException.ThrowIfNull(memberContent);

        _admissions = admissions;
        _inbox = inbox;
        _grace = grace;
        MemberKeys = memberKeys;
        MemberContent = memberContent;
    }

    public MemberContentKeys MemberKeys { get; }

    public MemberContentReceipts MemberContent { get; }

    public SessionRecording Recording { get; } = new();

    public void Release()
    {
        _admissions.Clear();
        _inbox.Clear();
        MemberKeys.Forget();
        MemberContent.Clear();
        _grace().Reset();

        Recording.Release();
    }
}
