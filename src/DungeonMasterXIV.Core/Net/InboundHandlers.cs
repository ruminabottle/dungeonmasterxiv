namespace DungeonMasterXIV.Net;

/// <summary>Groups the callbacks that receive inbound join requests, content and transport notices.</summary>
public readonly record struct InboundHandlers(
    JoinerAdmission Admission = default,
    HostAuthoredContent HostAuthored = default,
    MemberAuthoredContent MemberAuthored = default,
    TransportNotices Transport = default);
