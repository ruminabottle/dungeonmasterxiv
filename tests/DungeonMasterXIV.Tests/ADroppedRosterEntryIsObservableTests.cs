using System.Collections.Generic;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-70. <c>Vetted</c> drops a roster entry whose peer code no legitimate sender could have
/// produced, and said nothing about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The drop is correct and is not what this tests.</b> Degrade what a legitimate future version
/// could send, drop what no legitimate sender can produce — a peer code is the identifier, and a
/// degraded identifier yields an entry nobody can target or de-duplicate.
/// </para>
/// <para>
/// <b>The defect was the silence, and only for one of its two causes.</b> The drop happens either
/// because a keyholder is forging or because OUR OWN ENCODER IS BROKEN. Silence is right for the
/// first and wrong for the second: we would be deleting a genuine participant to hide our own bug,
/// and nothing anywhere would say so.
/// </para>
/// <para>
/// <b>So these test the SIGNAL, not the outcome.</b> The roster contents are unchanged by design,
/// so any assertion about them passes whether or not the fix exists. That is the shape an
/// observability test fails in.
/// </para>
/// </remarks>
public class ADroppedRosterEntryIsObservableTests
{
    private static readonly string Usable = new PeerCodeFixture().Value;

    // THE CRITERION. Fails before the fix, where the drop is silent.
    [Fact]
    public void DroppingAnEntryTellsTheDeveloperItHappened()
    {
        var log = new RecordingLog();

        Decode($$"""
            { "Roster": [ { "PeerCode": "not-a-code", "DisplayName": "Mallory", "Role": 0 },
                          { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """, log);

        Assert.NotEmpty(log.Warnings);
    }

    // THE VACUITY CONTROL, and for an observability fix it is the one that matters: a log that
    // fires on every decode is exactly as useless as one that never fires, and both satisfy the
    // assertion above.
    [Fact]
    public void ARosterWithNothingWrongSaysNothing()
    {
        var log = new RecordingLog();

        Decode($$"""
            { "Roster": [ { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """, log);

        Assert.Empty(log.Warnings);
    }

    // It has to say HOW MANY, or a developer watching their own encoder break cannot tell one bad
    // entry from a roster that arrived empty.
    [Fact]
    public void TheSignalSaysHowManyWereDropped()
    {
        var log = new RecordingLog();

        Decode($$"""
            { "Roster": [ { "PeerCode": "not-a-code", "DisplayName": "A", "Role": 0 },
                          { "PeerCode": "also-not-one", "DisplayName": "B", "Role": 0 },
                          { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """, log);

        Assert.Contains(log.Warnings, line => line.Contains('2'));
    }

    // D-8 and the practical half: a log is the artifact most likely to be pasted into a bug report,
    // and the rejected peer code is a value an attacker chose. The count is ours; the value is not.
    [Fact]
    public void TheSignalDoesNotRepeatTheRejectedValue()
    {
        var log = new RecordingLog();

        Decode($$"""
            { "Roster": [ { "PeerCode": "FORGED-BY-MALLORY", "DisplayName": "A", "Role": 0 },
                          { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """, log);

        Assert.DoesNotContain(log.Warnings, line => line.Contains("FORGED-BY-MALLORY"));
    }

    // The behaviour half, asserted so the fix cannot quietly change what a user sees. This passes
    // before and after — which is exactly why it is not the regression test.
    [Fact]
    public void TheRosterAUserSeesIsUnchanged()
    {
        var content = Decode($$"""
            { "Roster": [ { "PeerCode": "not-a-code", "DisplayName": "Mallory", "Role": 0 },
                          { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """, new RecordingLog());

        var entry = Assert.Single(content.Roster!);
        Assert.Equal("Bob", entry.DisplayName);
    }

    private static SessionContent Decode(string json, RecordingLog log)
    {
        Assert.True(
            SessionContentCodec.TryDecode(Encoding.UTF8.GetBytes(json), out var content, log),
            "The codec refused the document outright; these tests are about what it says while accepting one.");

        return content!;
    }

    private sealed class RecordingLog : ISessionTransportLog
    {
        public List<string> Warnings { get; } = [];

        public void Information(string message)
        {
        }

        public void Warning(string message) => Warnings.Add(message);

        public void Warning(System.Exception exception, string message) => Warnings.Add(message);
    }

    // A peer code the product could actually produce, derived rather than typed.
    private sealed class PeerCodeFixture
    {
        public string Value { get; } = SpeakableAlphabet.Characters[^SessionCode.Length..];
    }
}
