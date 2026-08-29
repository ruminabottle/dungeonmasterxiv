using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-59: the joiner derived from the host's key without checking it could be used.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mirror of BUG-56, and the attacker position is better here.</b> That one refused a
/// stranger's key at the host's door; this one refuses the host's key at the joiner's. It arrives on
/// the <b>acceptance</b>, which is relayed — so it is reachable by controlling the relay, which is
/// the position D-11 assumes an attacker may occupy.
/// </para>
/// <para>
/// <b>Measured before the fix, through this same wire path:</b> a junk host key threw
/// <c>CryptographicException</c> and a P-384 key threw <c>ArgumentException</c> out of <c>Tick</c> —
/// and in both cases <c>attempt.Admitted()</c> had ALREADY RUN, leaving
/// <c>Phase=Admitted, SessionKey=null, MayReceiveSessionState=true</c>. Guarding the derive alone
/// would have kept that state and merely stopped the throw, which is why the guard sits ahead of
/// <c>Admitted()</c> and why <see cref="TheFailedStateIsTheGoodOneNotTheSilentOne"/> asserts the
/// state rather than the absence of an exception.
/// </para>
/// </remarks>
public class TheJoinerValidatesTheHostKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 3, 0, 0, TimeSpan.Zero);

    private static readonly byte[] NotAKey = { 1, 2, 3 };

    /// <summary>Well-formed SPKI, wrong curve — imports cleanly, fails only at the agreement.</summary>
    private static byte[] WrongCurve()
    {
        using var other = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        return other.PublicKey.ExportSubjectPublicKeyInfo();
    }

    public static TheoryData<string> UnusableHostKeys() => new() { "junk", "wrong-curve" };

    private static byte[] KeyFor(string which) => which == "junk" ? NotAKey : WrongCurve();

    // Both halves of the defect, driven through the production wire path. Wrong-curve is the one a
    // format check misses, and it is the one that used to throw ArgumentException rather than the
    // CryptographicException a caller would think to catch.
    [Theory]
    [MemberData(nameof(UnusableHostKeys))]
    public void AnAcceptanceCarryingAnUnusableKeyFailsTheAttempt(string which)
    {
        var (player, transport, code) = AwaitingDecision();

        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, KeyFor(which)));
        player.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Failed, player.Join.Phase);
        Assert.Equal(SessionFailure.HostKeyUnusable, player.Join.Failure);
    }

    // THE STATE, not merely the absence of a throw. Before the fix this same path left the joiner
    // Admitted with a null key and MayReceiveSessionState true — a participant that believes it is
    // in the session and can open nothing, which is exactly what BUG-56 removed at the other end.
    // Pinning the good state is what stops the fix regressing into the bad one.
    [Theory]
    [MemberData(nameof(UnusableHostKeys))]
    public void TheFailedStateIsTheGoodOneNotTheSilentOne(string which)
    {
        var (player, transport, code) = AwaitingDecision();

        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, KeyFor(which)));
        player.Tick(TimeSpan.Zero, Now);

        Assert.False(player.Join.MayReceiveSessionState);
        Assert.Null(player.SessionKey);
        Assert.NotEqual(JoinPhase.Admitted, player.Join.Phase);

        // A-1.5h: the ruling chose failing over dropping precisely so the player can act. Nothing
        // lapses a joiner locally, so a dropped acceptance would have left them here forever.
        Assert.True(player.Join.MayRequestAgain);
        Assert.NotEmpty(SessionFailureMessage.For(player.Join.Failure));
    }

    // THE POSITIVE CONTROL. Every assertion above is satisfied by a guard that refuses everything,
    // which would break joining outright. This is the test that reddens under that mutation.
    [Fact]
    public void ALegitimateAcceptanceStillAdmitsAndDerivesAWorkingKey()
    {
        var (player, transport, code) = AwaitingDecision();
        using var hostKeys = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, hostKeys.PublicKey));
        player.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, player.Join.Phase);
        Assert.True(player.Join.MayReceiveSessionState);
        Assert.NotNull(player.SessionKey);

        // Not merely non-null: it is the key the HOST derives for this joiner. A guard that admitted
        // the joiner but produced a key nobody shares would satisfy every assertion above.
        Assert.Equal(hostKeys.DeriveSharedKey(player.JoinerKeys!.PublicKey, code), player.SessionKey);
    }

    // The joiner must not be made to throw by an acceptance it did not author. Stated separately
    // because it is what a hostile relay could do to a session before this fix, and it is the
    // property a reader will look for first.
    [Theory]
    [MemberData(nameof(UnusableHostKeys))]
    public void AnUnusableAcceptanceDoesNotThrowOutOfTheDrain(string which)
    {
        var (player, transport, code) = AwaitingDecision();

        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, KeyFor(which)));

        var thrown = Record.Exception(() => player.Tick(TimeSpan.Zero, Now));

        Assert.Null(thrown);
    }

    // A refused acceptance must not cost a later, legitimate one. The joiner may ask again, so the
    // guard has to refuse a frame rather than poison the attempt.
    [Fact]
    public void AJoinerRefusedOnceCanStillJoinOnASecondAttempt()
    {
        var (player, transport, code) = AwaitingDecision();
        using var hostKeys = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, NotAKey));
        player.Tick(TimeSpan.Zero, Now);
        Assert.True(player.Join.MayRequestAgain);

        player.RequestJoin(code, DisplayName.OrNone("Bob"));
        player.SynchroniseTransport();
        player.Tick(TimeSpan.Zero, Now);
        transport.Deliver(WireEnvelope.ForJoinPending(
            code, player.JoinerKeys!.PublicKey, hostKeys.PublicKey, AdmissionDeadline.DecidedByHost(Now)));
        player.Tick(TimeSpan.Zero, Now);
        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, hostKeys.PublicKey));
        player.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, player.Join.Phase);
        Assert.NotNull(player.SessionKey);
    }

    /// <summary>A joiner that has asked and been told the DM is looking, which arms the deadline.</summary>
    private static (SessionCoordinator Player, FakeTransport Transport, SessionCode Code) AwaitingDecision()
    {
        var transport = new FakeTransport();
        var player = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance, capabilities: SessionCapabilities.Default);
        var code = SessionCode.FromValid("BCDFGH");
        using var hostKeys = new SessionKeyExchange();

        player.RequestJoin(code, DisplayName.OrNone("Bob"));
        player.SynchroniseTransport();
        player.Tick(TimeSpan.Zero, Now);

        transport.Deliver(WireEnvelope.ForJoinPending(
            code, player.JoinerKeys!.PublicKey, hostKeys.PublicKey, AdmissionDeadline.DecidedByHost(Now)));
        player.Tick(TimeSpan.Zero, Now);
        transport.Sent.Clear();

        return (player, transport, code);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        /// <summary>Puts a real encoded frame on the wire, the way the relay would.</summary>
        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
