using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// R-1.2a: the session-code namespace is relay-wide, and the relay arbitrates it.
/// </summary>
public sealed class SessionRegistryTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly SessionCode Other = SessionCode.FromValid("JKMNPR");

    [Fact]
    public void FirstHostToAskGetsTheCode()
    {
        var registry = new SessionRegistry();

        Assert.True(registry.TryClaim(Code, "host-1"));
        Assert.Equal(1, registry.LiveSessionCount);
    }

    [Fact]
    public void SecondHostIsRefusedTheSameCode()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");

        Assert.False(registry.TryClaim(Code, "host-2"));
    }

    [Fact]
    public void AHostCannotHoldTwoCodesAtOnce()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");

        Assert.False(registry.TryClaim(Other, "host-1"));
    }

    [Fact]
    public void JoinersCannotWaitOnACodeNoSessionIsLiveUnder()
    {
        var registry = new SessionRegistry();

        Assert.False(registry.TryRegisterPending(Code.Value, "joiner-1", [1, 2, 3]));
    }

    /// <summary>
    /// R-1.3b's gate: asking to join does not put a connection into the session's traffic. It waits,
    /// and receives nothing at all until the DM accepts.
    /// </summary>
    [Fact]
    public void APendingJoinerIsNotYetAMemberAndReceivesNothing()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        registry.TryRegisterPending(Code.Value, "joiner-1", [1, 2, 3]);

        Assert.False(registry.IsMember(Code.Value, "joiner-1"));
        Assert.DoesNotContain("joiner-1", registry.MembersExcept(Code.Value, "host-1"));
    }

    [Fact]
    public void AdmittingAPendingJoinerMakesItAMember()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        registry.TryRegisterPending(Code.Value, "joiner-1", [1, 2, 3]);

        Assert.True(registry.TryAdmit(Code.Value, [1, 2, 3], out var admitted));
        Assert.Equal("joiner-1", admitted);
        Assert.True(registry.IsMember(Code.Value, "joiner-1"));
        Assert.Contains("joiner-1", registry.MembersExcept(Code.Value, "host-1"));
    }

    [Fact]
    public void ADeniedJoinerNeverBecomesAMember()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        registry.TryRegisterPending(Code.Value, "joiner-1", [1, 2, 3]);

        Assert.True(registry.TryDeny(Code.Value, [1, 2, 3], out var denied));
        Assert.Equal("joiner-1", denied);
        Assert.False(registry.IsMember(Code.Value, "joiner-1"));
        Assert.DoesNotContain("joiner-1", registry.MembersExcept(Code.Value, "host-1"));
    }

    /// <summary>
    /// A decision naming a joiner nobody is waiting for does nothing, rather than admitting whoever
    /// happens to be next. A stale or replayed decision must not let someone else in.
    /// </summary>
    [Fact]
    public void AnAdmissionForAnUnknownKeyAdmitsNobody()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        registry.TryRegisterPending(Code.Value, "joiner-1", [1, 2, 3]);

        Assert.False(registry.TryAdmit(Code.Value, [9, 9, 9], out _));
        Assert.False(registry.IsMember(Code.Value, "joiner-1"));
    }

    [Fact]
    public void PayloadRecipientsAreEveryoneButTheSender()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        Admit(registry, "joiner-1", [1]);
        Admit(registry, "joiner-2", [2]);

        var recipients = registry.MembersExcept(Code.Value, "joiner-1");

        Assert.Equal(2, recipients.Count);
        Assert.Contains("host-1", recipients);
        Assert.Contains("joiner-2", recipients);
        Assert.DoesNotContain("joiner-1", recipients);
    }

    [Fact]
    public void AJoinerLeavingDoesNotEndTheSession()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        Admit(registry, "joiner-1", [1]);

        var removal = registry.Remove("joiner-1");

        Assert.Empty(removal.Ended);
        Assert.Equal(1, registry.LiveSessionCount);
    }

    /// <summary>
    /// The host leaving ends the session and frees the code at once. The relay holds no grace
    /// window: R-1.4's grace period belongs to the DM's client, and a relay that held session
    /// state across a host's absence would be asserting the authority D-3 denies it.
    /// </summary>
    [Fact]
    public void TheHostLeavingEndsTheSessionAndFreesTheCode()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        Admit(registry, "joiner-1", [1]);

        var removal = registry.Remove("host-1");

        var ended = Assert.Single(removal.Ended);
        Assert.Equal(Code.Value, ended.Code);
        Assert.Equal(["joiner-1"], ended.OrphanedConnections);
        Assert.Equal(0, registry.LiveSessionCount);
        Assert.True(registry.TryClaim(Code, "host-2"));
    }

    [Fact]
    public void OrphanedJoinersAreDetachedSoTheyCannotSendIntoADeadSession()
    {
        var registry = new SessionRegistry();
        registry.TryClaim(Code, "host-1");
        Admit(registry, "joiner-1", [1]);
        registry.Remove("host-1");

        Assert.False(registry.IsParticipant(Code.Value, "joiner-1"));
    }

    /// <summary>Joins and admits in one step, for tests whose subject is not the gate itself.</summary>
    private static void Admit(SessionRegistry registry, string connectionId, byte[] publicKey)
    {
        registry.TryRegisterPending(Code.Value, connectionId, publicKey);
        registry.TryAdmit(Code.Value, publicKey, out _);
    }
}
