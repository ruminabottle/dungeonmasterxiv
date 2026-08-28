using System;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-61, sev1. On the human's machine <c>ECDiffieHellman.Create</c> throws
/// <see cref="CryptographicException"/> <c>0x80090029</c>, and nothing caught it: the throw unwound
/// out of the button handler, out of <c>Draw</c>, and the client saw it every frame.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the GUARD half only.</b> Why key creation is refused on that machine is unestablished
/// — the leading account is inference from an error code, nobody has the machine, and choosing a
/// different curve or provider is a D-11 crypto decision rather than a bug fix. So nothing here
/// asserts a cause, and the message the user sees does not claim one either.
/// </para>
/// <para>
/// <b>Driven through the coordinator, not the constructor.</b> A test that watched
/// <c>new SessionKeyExchange()</c> throw would prove something about the crypto library. What was
/// broken is that the two things the product does had no guard between that throw and the frame
/// loop, so the test has to start where the button starts.
/// </para>
/// </remarks>
public class KeyGenerationFailureDoesNotEscapeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 15, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // THE CRITERION, host side. Fails on the shipped build, where the exception leaves StartHosting
    // and keeps going until it is out of Draw.
    [Fact]
    public void HostingWhenKeysCannotBeCreatedDoesNotThrow()
    {
        var coordinator = WithKeysThatCannotBeCreated();

        Assert.Null(Record.Exception(() => coordinator.StartHosting()));
    }

    // The same for the other of the product's two functions. Joining fails identically to hosting
    // because both construct a key pair, which is why an affected machine has nothing left working.
    [Fact]
    public void JoiningWhenKeysCannotBeCreatedDoesNotThrow()
    {
        var coordinator = WithKeysThatCannotBeCreated();

        Assert.Null(Record.Exception(
            () => coordinator.RequestJoin(Code, DisplayName.OrNone("Ysera"), claimedParticipantId: null)));
    }

    // THE DEFINING SYMPTOM. One failure, one outcome — a guard that swallowed the throw but left the
    // client re-entering would fix the stack trace and not the storm the user actually sees.
    [Fact]
    public void TheFailureIsReportedOnceAndDoesNotReturnOnLaterFrames()
    {
        var coordinator = WithKeysThatCannotBeCreated(out var attempts);

        coordinator.StartHosting();
        var afterTheClick = attempts.Count;

        for (var frame = 0; frame < 10; frame++)
        {
            Assert.Null(Record.Exception(() => coordinator.Tick(TimeSpan.FromMilliseconds(16), Now)));
        }

        Assert.Equal(afterTheClick, attempts.Count);
        Assert.Equal(HostingPhase.Failed, coordinator.Host.Phase);
        Assert.Equal(SessionFailure.SessionKeysUnavailable, coordinator.Host.Failure);
    }

    // A-1.7e. The user gets ONE honest sentence: what happened, that nothing started, and no claim
    // about WHY -- the client has not established why and A-1.5j forbids asserting what it has not.
    [Fact]
    public void TheUserIsToldWhatHappenedWithoutBeingToldWhy()
    {
        var coordinator = WithKeysThatCannotBeCreated();

        coordinator.StartHosting();
        var message = SessionFailureMessage.For(coordinator.Host.Failure);

        Assert.NotEmpty(message);
        Assert.DoesNotContain("your network", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not supported", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("encrypted", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protected", message, StringComparison.OrdinalIgnoreCase);
    }

    // THE POSITIVE HALF. A guard that swallowed everything would pass every test above and leave a
    // product that never starts -- which is the defect, not the fix.
    [Fact]
    public void KeysThatCanBeCreatedStillStartASession()
    {
        var transport = new SilentTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);

        coordinator.StartHosting();

        Assert.Equal(HostingPhase.Registering, coordinator.Host.Phase);
        Assert.NotNull(coordinator.HostKeys);
        Assert.Equal(SessionFailure.None, coordinator.Host.Failure);
    }

    // BUG-62, AND IT NEEDS BOTH HALVES. TryMakeKeys catches CryptographicException only, and its doc
    // comment calls that narrowness deliberate: "a broader catch here would hide a genuine defect in
    // this method's own callers behind a message about keys". Nothing asserted it. Widening the catch
    // to Exception — the single most likely edit anyone makes to a try/catch — left all 979 tests
    // green, because a catch-all does not break the success path and nothing here ever threw a
    // non-cryptographic exception from the seam.
    //
    // The property is "CRYPTOGRAPHIC FAILURES ARE CAUGHT AND NOTHING ELSE IS". Asserting only the
    // first half invites the opposite break — narrowing to CryptographicException EXACTLY and
    // refusing a real cryptographic failure — which is the same one-directional shape as the gap
    // this closes.
    [Fact]
    public void ADefectInTheCallerIsNotSwallowedAsAKeyFailure()
    {
        var coordinator = WithKeysThatThrow(() => new InvalidOperationException("a defect in the caller"));

        var thrown = Record.Exception(() => coordinator.StartHosting());

        Assert.IsType<InvalidOperationException>(thrown);
    }

    // The other entry point, because both of the product's two functions construct a key pair and a
    // guard widened on one path is widened on both.
    [Fact]
    public void ADefectInTheCallerIsNotSwallowedWhenJoiningEither()
    {
        var coordinator = WithKeysThatThrow(() => new InvalidOperationException("a defect in the caller"));

        var thrown = Record.Exception(
            () => coordinator.RequestJoin(Code, DisplayName.OrNone("Ysera"), claimedParticipantId: null));

        Assert.IsType<InvalidOperationException>(thrown);
    }

    // THE OTHER HALF, and the one a narrowing edit breaks. AuthenticationTagMismatchException is a
    // CryptographicException SUBCLASS and a genuine cryptographic failure, so it must still be
    // caught. A catch written as `when (ex.GetType() == typeof(CryptographicException))` would pass
    // both tests above and fail here, having turned a real crypto failure into a crash.
    [Fact]
    public void AGenuineCryptographicFailureIsCaughtEvenWhenItIsASubclass()
    {
        var coordinator = WithKeysThatThrow(() => new AuthenticationTagMismatchException());

        Assert.Null(Record.Exception(() => coordinator.StartHosting()));
        Assert.Equal(HostingPhase.Failed, coordinator.Host.Phase);
        Assert.Equal(SessionFailure.SessionKeysUnavailable, coordinator.Host.Failure);
    }

    /// <summary>A coordinator whose key seam throws whatever the caller names, once per attempt.</summary>
    /// <remarks>
    /// Separate from <see cref="WithKeysThatCannotBeCreated()"/> rather than a generalisation of it:
    /// that helper carries the reported specimen <c>0x80090029</c> and its attempt counter, and both
    /// are load-bearing for the tests above it.
    /// </remarks>
    private static SessionCoordinator WithKeysThatThrow(Func<Exception> failure) =>
        new(
            new SilentTransport(),
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            SilentLog.Instance,
            newKeys: () => throw failure());

    private static SessionCoordinator WithKeysThatCannotBeCreated() =>
        WithKeysThatCannotBeCreated(out _);

    private static SessionCoordinator WithKeysThatCannotBeCreated(out System.Collections.Generic.List<int> attempts)
    {
        var seen = new System.Collections.Generic.List<int>();
        attempts = seen;

        // 0x80090029 is NTE_NOT_SUPPORTED, the code the human's machine actually produced. The
        // number is here so the specimen is the reported one rather than a convenient stand-in.
        return new SessionCoordinator(
            new SilentTransport(),
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            SilentLog.Instance,
            newKeys: () =>
            {
                seen.Add(seen.Count);
                throw new CryptographicException(unchecked((int)0x80090029));
            });
    }

    private sealed class SilentTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }

        public void Raise(SessionFailure failure) => Failed?.Invoke(failure);

        public void Deliver(byte[] frame) => Received?.Invoke(frame);
    }
}
