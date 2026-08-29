using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.13(b): a joined player sees the DM's name, because the host authors its own roster entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>This was not a rendering gap — there was nothing to render.</b> The roster was built from
/// <see cref="SessionAudience.Recipients"/> alone, and the host is deliberately absent from that
/// list <i>"so nothing can be addressed to it"</i>. Correct for a SEND list, wrong for a MEMBERSHIP
/// list, and the two were one expression. <c>SessionRole.DungeonMaster</c> existed and <b>nothing
/// anywhere had ever constructed a DM roster entry</b> — measured before this file: the only hit in
/// <c>src/</c> and <c>Windows/</c> was <c>SessionRoleLabel</c>, a label lookup.
/// </para>
/// <para>
/// <b>Driven through the COORDINATOR, deliberately.</b> The peer code the host publishes must be the
/// one a joiner would compute for it, and that derivation lives on <c>AdmissionControl</c> — so a
/// test that constructed <see cref="RosterBroadcast"/> directly would have to supply a code, which
/// means inventing one, which is the thing this design refuses. Driving the coordinator exercises
/// the wiring that actually produces the value.
/// </para>
/// <para>
/// <b>And the joiner-side check is the one that matters</b>, because an entry whose peer code does
/// not parse is DROPPED by <see cref="SessionContentCodec"/>. A test asserting on what the host put
/// in the list would pass on a build whose DM entry is deleted before anyone sees it.
/// </para>
/// </remarks>
public class TheHostIsInItsOwnRosterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 4, 0, 0, TimeSpan.Zero);

    /// <summary>The tail of the alphabet, so a peer code can never equal the session code.</summary>
    private static readonly string PeerCode = SpeakableAlphabet.Characters[^SessionCode.Length..];

    // THE CRITERION.
    //
    // NOT "fails on origin/main" — this test cannot compile there, because the name it needs has no
    // way in. Saying it would be the overclaim this file is otherwise careful about. What IS
    // established about main, by two things that predate this change:
    //
    //   TheRosterTravelsSealedTests asserted Assert.Single on the roster a joined player opens,
    //     and passed. So the roster reaching a player carried EXACTLY ONE entry, the player's own.
    //   git grep "SessionRole.DungeonMaster" -- src/ Windows/  returned one hit, SessionRoleLabel,
    //     a label lookup. Nothing had ever CONSTRUCTED a DM entry.
    //
    // That is the starting state, measured rather than remembered. What this test adds is that the
    // entry now exists AND survives the joiner-side vetting.
    [Fact]
    public void AJoinedPlayerSeesTheDungeonMaster()
    {
        var roster = RosterReachingAJoinedPlayer(DisplayName.OrNone("Nanamo"));

        var dm = Assert.Single(roster, entry => entry.Role == SessionRole.DungeonMaster);
        Assert.Equal("Nanamo", dm.DisplayName);
    }

    // The player is still there. A build that replaced the roster with the host rather than
    // prepending to it would pass the criterion above and break A-1.13a in the same stroke.
    [Fact]
    public void ThePlayersOwnEntrySurvivesTheHostJoiningTheList()
    {
        var roster = RosterReachingAJoinedPlayer(DisplayName.OrNone("Nanamo"));

        var player = Assert.Single(roster, entry => entry.Role == SessionRole.Player);
        Assert.Equal("Ysera", player.DisplayName);
        Assert.Equal(PeerCode, player.PeerCode);
    }

    // >>> THE ASSERTION THE WHOLE DESIGN RESTS ON <<<
    //
    // The DM's code must be the one AdmissionControl would derive, because that is the only
    // derivation and SessionContentCodec drops any entry whose code will not parse. An invented or
    // constant code passes "there is a DM entry" on the HOST's side and is deleted before it
    // reaches anyone -- reintroducing the absence this ticket exists to fix.
    //
    // Compared against a SECOND, INDEPENDENT derivation of the same value rather than against the
    // string the host happened to publish: asserting the entry equals itself is the vacuity that has
    // been caught four times tonight.
    [Fact]
    public void TheDungeonMastersCodeIsTheOneAJoinerWouldComputeForIt()
    {
        var (host, transport, joiner) = HostingWithAnAdmittedPlayer(DisplayName.OrNone("Nanamo"));
        var expected = new AdmissionControl(
            new AdmissionAnnouncer(new SilentTransport()),
            () => host.Host.Code,
            () => host.HostKeys,
            static _ => null,
            SilentLog.Instance).PeerCodeFor(host.HostKeys!.PublicKey);

        var dm = Assert.Single(RosterOpenedBy(joiner, host, transport), e => e.Role == SessionRole.DungeonMaster);

        Assert.Equal(expected.Value, dm.PeerCode);
        Assert.True(DungeonMasterXIV.Net.PeerCode.TryParse(dm.PeerCode, out _), "An unparseable code is dropped by the codec.");
        joiner.Dispose();
    }

    // The DM comes first, because the DM is who a joiner is looking for. Ordering is a rendering
    // choice and this is the only place with an opinion about it, so it is pinned where it is made.
    [Fact]
    public void TheDungeonMasterIsFirstInTheList()
    {
        var roster = RosterReachingAJoinedPlayer(DisplayName.OrNone("Nanamo"));

        Assert.Equal(SessionRole.DungeonMaster, roster[0].Role);
    }

    // A DM that set no name is shown as unstated rather than blank. An empty label beside a code
    // somebody is comparing reads as a rendering fault and invites the reader to look past it --
    // DisplayName's own argument, applied to the one client that never sends its name over the wire.
    [Fact]
    public void ADungeonMasterWithNoNameIsStillNamedSomething()
    {
        var roster = RosterReachingAJoinedPlayer(DisplayName.None);

        var dm = Assert.Single(roster, entry => entry.Role == SessionRole.DungeonMaster);
        Assert.Equal(DisplayName.Unstated, dm.DisplayName);
        Assert.NotEqual(string.Empty, dm.DisplayName);
    }

    private static IReadOnlyList<RosterEntry> RosterReachingAJoinedPlayer(DisplayName hostName)
    {
        var (host, transport, joiner) = HostingWithAnAdmittedPlayer(hostName);
        var roster = RosterOpenedBy(joiner, host, transport);
        joiner.Dispose();
        return roster;
    }

    private static (SessionCoordinator Host, FakeTransport Transport, SessionKeyExchange Joiner)
        HostingWithAnAdmittedPlayer(DisplayName hostName)
    {
        var transport = new FakeTransport();
        var host = new SessionCoordinator(
            transport,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            SilentLog.Instance,
            new SessionCapabilities(HostDisplayName: () => hostName));

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();

        var joiner = new SessionKeyExchange();
        host.ReceiveJoinRequest(
            PeerCodes.Of(PeerCode), joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        transport.Sent.Clear();
        host.Admit(PeerCodes.Of(PeerCode));

        return (host, transport, joiner);
    }

    /// <summary>The roster as the joined player actually opens it, not as the host wrote it.</summary>
    private static IReadOnlyList<RosterEntry> RosterOpenedBy(
        SessionKeyExchange joiner, SessionCoordinator host, FakeTransport transport)
    {
        var key = joiner.DeriveSharedKey(host.HostKeys!.PublicKey, host.Host.Code!.Value);

        foreach (var sent in transport.Sent)
        {
            if (!EnvelopeCodec.TryDecode(sent, out var envelope) || envelope!.TryGetSealedPayload() is not { } sealedPayload)
            {
                continue;
            }

            byte[] plaintext;
            try
            {
                plaintext = SessionCipher.Open(key, sealedPayload, envelope!.AssociatedData());
            }
            catch (CryptographicException)
            {
                continue;   // sealed for somebody else
            }

            // TryDecode, not the raw content: this is the gate that DROPS entries whose peer code
            // will not parse, and passing through it is the whole point of the assertion.
            if (SessionContentCodec.TryDecode(plaintext, out var content) && content!.Roster is { } roster)
            {
                return roster;
            }
        }

        throw new InvalidOperationException("No roster reached the joined player at all.");
    }

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected => true;

        public bool IsReadyToSend => true;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay)
        {
        }

        public void Disconnect()
        {
        }

        public void Send(byte[] envelope)
        {
        }
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);
    }
}
