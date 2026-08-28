using System;
using System.Linq;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// <see cref="PeerCode"/> accepts what this product generates and refuses everything else.
/// </summary>
/// <remarks>
/// The rule is BUG-57's, moved off <c>SessionContentCodec</c> and onto the type so it applies at
/// every door rather than one. These assert the rule; <c>PeerCodeIsTheOnlyDoorTests</c> asserts that
/// nothing bypasses it.
/// </remarks>
public sealed class PeerCodeTests
{
    // A GRADUATED PROBE rather than one bad input, so a green run says WHICH rejections work. The
    // old fixtures are the reason this matters: "PEER-1" is SIX CHARACTERS, so it satisfied the
    // length rule and failed only on the alphabet. A suite that probed length alone would have
    // called the vet working.
    [Theory]
    [InlineData(null, "null is not a code")]
    [InlineData("", "empty")]
    [InlineData("BCDFG", "one short")]
    [InlineData("BCDFGHJ", "one long")]
    [InlineData("PEER-1", "right LENGTH, wrong alphabet -- the exact shape every old fixture used")]
    [InlineData("BCDFGA", "A is a vowel, excluded so a code cannot spell a word")]
    [InlineData("BCDFGE", "E is a vowel")]
    [InlineData("BCDFG0", "0 is excluded as confusable with O")]
    [InlineData("BCDFG1", "1 is excluded as confusable with I and L")]
    [InlineData("BCDFG5", "5 is excluded as confusable with S")]
    [InlineData("BCDFGL", "L is excluded as confusable with 1")]
    [InlineData("BCDFGS", "S is excluded as confusable with 5")]
    [InlineData("bcdfgh", "lower case -- PeerCodeFor renders upper only")]
    [InlineData("BCD-GH", "a hyphen, which is display grouping and never part of the value")]
    [InlineData("BCD GH ", "padded to length with a space")]
    public void ACodeThisProductCouldNotHaveGeneratedIsRefused(string? candidate, string why)
    {
        Assert.False(PeerCode.TryParse(candidate, out var parsed), why);
        Assert.False(parsed.IsPresent, $"a refused code must not yield a usable one ({why})");
    }

    // The other half of the graduated probe. Without this the rule above could be "refuse
    // everything" and every rejection test would still pass.
    [Theory]
    [InlineData("BCDFGH")]
    [InlineData("JKMNPR")]
    [InlineData("TVWXY2")]
    [InlineData("346789")]
    public void ACodeOfTheShapeThisProductGeneratesIsAccepted(string candidate)
    {
        Assert.True(PeerCode.TryParse(candidate, out var parsed));
        Assert.True(parsed.IsPresent);
        Assert.Equal(candidate, parsed.Value);
    }

    // THE REAL PRODUCER, not a string shaped like its output -- the argument
    // APeerCodeCannotForgeTheCompareLineTests already makes for the codec, applied to the type. A
    // hand-built "BCDFGH" would pass a rule that PeerCodeFor's actual output failed, and nothing
    // would say so.
    [Fact]
    public void WhatPeerCodeForActuallyEmitsIsAcceptedByTheRuleThatGuardsIt()
    {
        var control = new AdmissionControl(
            new AdmissionAnnouncer(new SilentTransport()),
            () => SessionCode.FromValid("BKD7RM"),
            () => null);

        var generated = control.PeerCodeFor(Encoding.UTF8.GetBytes("a joiner's public key"));

        Assert.True(generated.IsPresent);
        Assert.True(PeerCode.TryParse(generated.Value, out var reparsed));
        Assert.Equal(generated, reparsed);
    }

    // Two joiners must not be given one code, or the value that tells participants apart (A-1.2d)
    // does not. Derived from actual output rather than asserted about the algorithm.
    [Fact]
    public void TwoDifferentKeysAreNamedDifferently()
    {
        var control = new AdmissionControl(
            new AdmissionAnnouncer(new SilentTransport()),
            () => SessionCode.FromValid("BKD7RM"),
            () => null);

        var codes = Enumerable.Range(0, 32)
            .Select(i => control.PeerCodeFor(Encoding.UTF8.GetBytes($"joiner {i}")))
            .ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    // THE RESIDUAL HOLE, asserted rather than described. A readonly struct always has a default and
    // no parse gate can stop one being written, so what matters is that it can never be mistaken for
    // a code: absent, empty, and equal to no real one.
    [Fact]
    public void TheDefaultIsAbsentRatherThanAQuietlyValidCode()
    {
        var absent = default(PeerCode);

        Assert.False(absent.IsPresent);
        Assert.Equal(string.Empty, absent.Value);

        Assert.True(PeerCode.TryParse("BCDFGH", out var real));
        Assert.NotEqual(real, absent);
    }

    // Fails if: equality goes reference-shaped or case-insensitive. Codes are compared to find a
    // pending request, so two codes that differ must never match.
    [Fact]
    public void EqualityIsByValueAndExact()
    {
        Assert.True(PeerCode.TryParse("BCDFGH", out var one));
        Assert.True(PeerCode.TryParse("BCDFGH", out var same));
        Assert.True(PeerCode.TryParse("BCDFGJ", out var other));

        Assert.Equal(one, same);
        Assert.True(one == same);
        Assert.Equal(one.GetHashCode(), same.GetHashCode());

        Assert.NotEqual(one, other);
        Assert.True(one != other);
    }

    // PeerCodeFor touches neither the transport nor the keys; this exists only to satisfy the
    // constructor, so it does nothing rather than pretending to be a socket.
    private sealed class SilentTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public bool IsConnected => false;

        public bool IsReadyToSend => false;

        public void Connect(Uri relay)
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
}
