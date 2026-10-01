using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>The candidate keys and callback for opening and handling session content sent by admitted members.</summary>
public readonly record struct MemberAuthoredContent(
    Func<IEnumerable<PeerContentKey>>? OpenWith = null,
    Action<PeerCode, SessionContent>? OnContent = null);
