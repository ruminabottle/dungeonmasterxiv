namespace DungeonMasterXIV.Net;

public readonly record struct InboundHandlers(
    JoinerAdmission Admission = default,
    HostAuthoredContent HostAuthored = default,
    MemberAuthoredContent MemberAuthored = default,
    TransportNotices Transport = default);
