using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// DMXENG-57: the capabilities record, and the DMXENG-13 guarantee it must not quietly undo.
/// </summary>
/// <remarks>
/// <para>
/// <b>The point of the record is the constructor row, and no test can see that row</b> — a
/// parameter count is a compile-time property and the compiler is the only thing that enforces it.
/// What IS testable is everything the move could have broken on the way, and that is what this
/// file covers: the seam still reaches production, the defaults are the same defaults, and the
/// required-ness DMXENG-13 bought is still bought.
/// </para>
/// <para>
/// <b>The required-ness is the one to read carefully.</b> DMXENG-13 ruled that
/// <c>ISessionTransportLog? log = null</c> was wrong not because of what the default DID but because
/// of who SUPPLIED it — <i>"an optional dependency production happens to supply is one refactor away
/// from production not supplying it, and nothing would fail."</i> Folding two optional parameters
/// into a record is exactly such a refactor, so the guarantee is re-established deliberately: the
/// record is a REQUIRED argument, and a caller wanting the old behaviour names
/// <see cref="SessionCapabilities.Default"/> out loud.
/// </para>
/// </remarks>
public class WhatCoreCannotDoForItselfTests
{
    // The seam still reaches production. Fails if the constructor stops reading the supplied source
    // and falls back to the platform default -- which compiles, passes every other test in the
    // suite, and silently removes BUG-61's only observation point.
    [Fact]
    public void ASuppliedKeySourceIsTheOneUsed()
    {
        var asked = 0;
        var capabilities = new SessionCapabilities(NewKeys: () =>
        {
            asked++;
            return new SessionKeyExchange();
        });

        var coordinator = new SessionCoordinator(
            new SilentTransport(), () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, capabilities);
        coordinator.StartHosting();

        Assert.True(asked > 0, "The record's key source must be what the session actually uses.");
    }

    // The other seam, same shape. R-1.5c: a joiner is minted a participant on admission, and the
    // only route from the campaign store into Core is this member.
    [Fact]
    public void ASuppliedMintIsTheOneUsed()
    {
        var minted = Guid.NewGuid();
        var capabilities = new SessionCapabilities(MintParticipant: _ => minted);

        Assert.Equal(minted, capabilities.ParticipantSource(DisplayName.OrNone("Bob")));
    }

    // THE DEFAULTS ARE THE SAME DEFAULTS. Before this record, omitting the arguments gave platform
    // key generation and no campaign; SessionCapabilities.Default must mean exactly that and not
    // "nothing", which would be a null-reference on the first host rather than a working session.
    [Fact]
    public void TheDefaultRecordSuppliesTheSameDefaultsTheParametersDid()
    {
        var capabilities = SessionCapabilities.Default;

        Assert.Null(capabilities.NewKeys);
        Assert.Null(capabilities.MintParticipant);

        using var keys = capabilities.KeySource();
        Assert.NotNull(keys);
        Assert.NotNull(keys.PublicKey);
        Assert.Null(capabilities.ParticipantSource(DisplayName.OrNone("Bob")));
    }

    // The fallbacks live in ONE place. Two call sites each applying their own `?? default` is how
    // two paths come to disagree about what "no key source" means, and the record exists partly so
    // that expression has a single home.
    [Fact]
    public void AnUnsetMemberFallsBackWithoutTheCallerArrangingIt()
    {
        var capabilities = new SessionCapabilities(NewKeys: null, MintParticipant: null);

        Assert.NotNull(capabilities.KeySource);
        Assert.NotNull(capabilities.ParticipantSource);
        using var keys = capabilities.KeySource();
        Assert.NotNull(keys);
    }

    // DMXENG-13, RE-ESTABLISHED RATHER THAN ASSUMED. The record is required, so null is a caller
    // error and is refused at construction. Without the guard, a null record reaches KeySource on
    // the first host and reports itself as a NullReferenceException in the frame loop -- far from
    // the call site that was actually wrong.
    [Fact]
    public void AMissingCapabilitiesRecordIsRefusedAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => new SessionCoordinator(
            new SilentTransport(), () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, capabilities: null!));
    }

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
