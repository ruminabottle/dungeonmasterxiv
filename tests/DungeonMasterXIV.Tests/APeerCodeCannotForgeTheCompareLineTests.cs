using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-57. PR #86 gated <see cref="DisplayName"/> at the decode boundary; the forged
/// "Code to compare:" line simply moved one field over into <c>PeerCode</c>, which
/// <c>SessionContentCodec.Vetted</c> passed through untouched.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gate is the codec, deliberately, so it stays the ONE door.</b> That is the rule the
/// DisplayName fix established and this is the field it did not cover — the property is "nothing
/// hostile reaches a decoded <see cref="SessionContent"/>", not "the reported field is clean".
/// </para>
/// <para>
/// <b>A bad peer code DROPS the entry rather than degrading it, and that is the opposite of the
/// DisplayName ruling on purpose.</b> A name is a label, so a refused one degrades to Unstated and
/// the participant stays. A peer code is the IDENTITY — it is what tells two participants with the
/// same display name apart (A-1.2d) — so an entry whose code is unusable identifies nobody, and
/// degrading it would manufacture a fake identity, which is worse than the forgery being removed.
/// </para>
/// </remarks>
public class APeerCodeCannotForgeTheCompareLineTests
{
    // qa-1's probe, turned into a test. Fails on the shipped build, where Vetted() rewrites only
    // DisplayName and returns PeerCode verbatim -- so the multi-line forged fingerprint line the
    // D-8 gate exists to stop arrives intact one field along.
    [Fact]
    public void AForgedCompareLineInThePeerCodeDoesNotReachAConsumer()
    {
        var forged = "PEER-3\nCode to compare: FORGED";

        var decoded = Decode($$"""
            { "Roster": [ { "PeerCode": {{Quoted(forged)}}, "DisplayName": "Bob", "Role": 0 } ] }
            """);

        Assert.DoesNotContain(
            decoded.Roster ?? [],
            entry => entry.PeerCode.Contains('\n') || entry.PeerCode.Contains("Code to compare"));
    }

    // THE POSITIVE HALF, and it is where this class of fix keeps failing us: a validator that
    // refuses everything defeats every attack above and looks perfect. A code the product actually
    // produces must arrive intact AND its entry must still be there -- dropping the wrong entries
    // is the same defect as passing the wrong ones, in the expensive direction.
    [Fact]
    public void AWellFormedPeerCodeArrivesIntactAndItsEntryStays()
    {
        var wellFormed = WellFormed();

        var decoded = Decode($$"""
            { "Roster": [ { "PeerCode": {{Quoted(wellFormed)}}, "DisplayName": "Bob", "Role": 0 } ] }
            """);

        var entry = Assert.Single(decoded.Roster!);
        Assert.Equal(wellFormed, entry.PeerCode);
        Assert.Equal("Bob", entry.DisplayName);
    }

    // The entry goes, rather than the code being blanked or replaced. A degraded code would name a
    // participant who does not exist, which is a worse outcome than the forgery.
    [Fact]
    public void AnEntryWhosePeerCodeIsUnusableIsDroppedRatherThanRepaired()
    {
        var decoded = Decode($$"""
            { "Roster": [
                { "PeerCode": "not-a-code", "DisplayName": "Mallory", "Role": 0 },
                { "PeerCode": {{Quoted(WellFormed())}}, "DisplayName": "Bob", "Role": 0 } ] }
            """);

        var entry = Assert.Single(decoded.Roster!);
        Assert.Equal("Bob", entry.DisplayName);
    }

    // The REAL producer, not a string shaped like its output. A hand-built "BCDFGH" would pass a
    // shape check that PeerCodeFor's actual output failed, and the test would not say so.
    private static string WellFormed() =>
        new AdmissionControl(
                new AdmissionAnnouncer(new SilentTransport()),
                () => SessionCode.FromValid("BKD7RM"),
                () => null)
            .PeerCodeFor(Encoding.UTF8.GetBytes("a joiner's public key"));

    // PeerCodeFor touches neither the transport nor the keys; this exists only to satisfy the
    // constructor, so it does nothing rather than pretending to be a socket.
    private sealed class SilentTransport : ISessionTransport
    {
        public event System.Action<SessionFailure>? Failed;

        public event System.Action<byte[]>? Received;

        public bool IsConnected => false;

        public bool IsReadyToSend => false;

        public void Connect(System.Uri relay)
        {
        }

        public void Disconnect()
        {
        }

        public void Send(byte[] envelope)
        {
        }

        public void Raise(SessionFailure failure) => Failed?.Invoke(failure);

        public void Deliver(byte[] frame) => Received?.Invoke(frame);
    }

    private static string Quoted(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);

    private static SessionContent Decode(string json)
    {
        Assert.True(
            SessionContentCodec.TryDecode(Encoding.UTF8.GetBytes(json), out var content),
            "The codec refused the document outright; this test is about what survives decoding.");

        return content!;
    }
}
