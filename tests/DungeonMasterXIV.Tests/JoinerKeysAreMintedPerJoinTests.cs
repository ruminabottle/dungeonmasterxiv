using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-117: a joining client mints a FRESH key pair on every join, so a retained joiner key names one
/// attempt rather than one person.
/// </summary>
/// <remarks>
/// <para>
/// <b>This guards a CLEARANCE, not current behaviour.</b> DMXENG-58 has the relay retain a joiner's
/// public key so it can name a departed member, and that was cleared under D-8 on the ground that the
/// key is <i>ephemeral by construction</i> — a fresh pair per join, so the retained value is
/// per-attempt and no more linkable than a connection id. <b>The behaviour held and nothing enforced
/// it:</b> making joiner keys durable across joins left all 1478 tests green.
/// </para>
/// <para>
/// If joiner keys ever became durable, the relay's retention would silently become CROSS-SESSION
/// LINKAGE — with nothing in the relay changing and no test failing. <b>Relink is exactly the feature
/// that would tempt it</b>, a returning client that remembers who it is, which is why the relink shape
/// is a case here rather than a footnote.
/// </para>
/// <para>
/// <b>It asserts the KEYS, not that Dispose was called.</b> A test that watched for the call would be
/// satisfied by a Dispose that did nothing, and by any future re-mint that reused material.
/// </para>
/// <para>
/// <b>THE LIMIT, AND IT IS HALF THE PROPERTY.</b> This pins that the CLIENT re-mints per join. It
/// does <b>not</b> pin that the relay's retention is unlinkable — a future relay change could make a
/// retained key linkable by another route (timing, ordering, a second field correlated with it) and
/// this test would stay green. <b>The half closed here is "the value retained is per-attempt"; the
/// half left open is "nothing else re-identifies the attempt."</b> Stated rather than implied, because
/// a guard whose comment promises more than it delivers is the defect PR #141 was about.
/// </para>
/// </remarks>
public class JoinerKeysAreMintedPerJoinTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    /// <summary>The relink case is the named temptation; the plain case is the ordinary path.</summary>
    public static TheoryData<string> SecondJoinShapes() => new() { "ordinary", "relink" };

    // THE PROPERTY. Fails if a joiner's key pair survives a second join — which is exactly the
    // mutation that left 1478 tests green: remove the dispose-and-null from JoinRequester.Request and
    // the pair persists.
    //
    // The relink case matters beyond repeating the plain one: relink reaches the SAME entry point
    // today, so a durable-key change could not hide there — but if a separate relink door were ever
    // added, a plain-join-only test would not see it and this case would.
    [Theory]
    [MemberData(nameof(SecondJoinShapes))]
    public void EachJoinMintsKeyMaterialTheLastJoinDidNotHave(string secondJoin)
    {
        var player = Joining();

        player.RequestJoin(Code, DisplayName.OrNone("Bob"), claimedParticipantId: null);
        var first = player.JoinerKeys!.PublicKey;

        player.RequestJoin(
            Code,
            DisplayName.OrNone("Bob"),
            secondJoin == "relink" ? Guid.NewGuid() : null);
        var second = player.JoinerKeys!.PublicKey;

        // THE PREMISE, asserted rather than trusted: two empty or absent keys would compare unequal
        // for the wrong reason, and a failure to mint would make the assertion below meaningless.
        Assert.NotEmpty(first);
        Assert.NotEmpty(second);

        Assert.NotEqual(first, second);
    }

    // The negative half, and it is what stops the test above passing against a build that simply
    // fails to produce a key on the second call. Reading the SAME key twice must be stable -- if
    // PublicKey were nondeterministic, "different across joins" would prove nothing about minting.
    [Fact]
    public void ReadingOneKeyTwiceGivesTheSameBytes()
    {
        var player = Joining();

        player.RequestJoin(Code, DisplayName.OrNone("Bob"), claimedParticipantId: null);

        Assert.Equal(player.JoinerKeys!.PublicKey, player.JoinerKeys!.PublicKey);
    }

    private static SessionCoordinator Joining() =>
        new(
            new SilentTransport(),
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: SilentLog.Instance,
            capabilities: SessionCapabilities.Default);

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }
    }
}
