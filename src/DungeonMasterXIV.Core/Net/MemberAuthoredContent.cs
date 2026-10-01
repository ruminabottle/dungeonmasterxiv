using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

public readonly record struct MemberAuthoredContent(
    Func<IEnumerable<PeerContentKey>>? OpenWith = null,
    Action<PeerCode, SessionContent>? OnContent = null);
