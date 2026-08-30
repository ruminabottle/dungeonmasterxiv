using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.19 / A-2.34: a non-host member says something and every other admitted member receives it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE ASSERTIONS ARE ON WHAT A DIFFERENT MEMBER CAN OPEN, WHICH IS THE WHOLE ROW.</b> A-2.34
/// says so in terms: <i>assert it arrives at a DIFFERENT member, not merely that it appears on the
/// sender's own screen — local echo passes a build that never reached the wire.</i> So no test here
/// asserts on the sender's log, and the load-bearing ones decrypt the host's outbound envelope with
/// a THIRD party's key.
/// </para>
/// <para>
/// <b>AND NON-HOST ORIGINATION IS PINNED SEPARATELY, because a build where only the host can
/// originate satisfies every other message requirement in the document.</b> The speaker in these
/// fixtures is an admitted member sealing to the host with its own derived key; nothing here lets
/// the host author the content it broadcasts.
/// </para>
/// <para>
/// <b>WHAT THIS FILE DOES NOT COVER, stated rather than left to be discovered.</b> The compose
/// surface is not drawn here — that is UI. These tests reach <c>SessionMembership.Say</c> and the
/// host's inbound door directly, which is honest for a Core test and is not a claim that a player
/// can type into a box today.
/// </para>
/// </remarks>
public sealed class BaseChatReachesEveryMemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 15, 0, 0, TimeSpan.Zero);
    private const string Speaker = "PRBCD2";
    private const string Listener = "JNKBCD";

    // THE BAR, AND IT IS A-2.34 ENTIRE. Fails if: a member's message reaches the host and stops
    // there -- which is every build before this one, since a member seals to the host and to nobody
    // else, so without a rebroadcast the message reaches exactly one machine.
    //
    // The assertion opens the host's OUTBOUND envelope with the LISTENER's key. A build that
    // recorded the message in the host's own log and sent nothing passes every other message
    // requirement and fails here.
    [Fact]
    public void AMessageFromOneMemberReachesADifferentMember()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        var speakerCode = Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "the door is trapped" }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(StampedLinesFor(listener, host, transport));

        Assert.Equal("the door is trapped", line.Text);
        Assert.Equal(speakerCode.Value, line.Peer);
        Assert.Equal(StreamEventKind.Message, line.Kind);
    }

    // NON-HOST ORIGINATION, PINNED AS ITS OWN ROW. Fails if: the speaker on the rebroadcast is the
    // host rather than the member who said it.
    //
    // A-2.34's whole point is that a host-only origination build looks correct everywhere else. The
    // peer code here comes from the KEY THE PAYLOAD OPENED UNDER, never from the payload -- there is
    // no speaker field in what a member sends -- so this also pins that a member cannot speak as
    // somebody else.
    [Fact]
    public void TheSpeakerIsTheMemberAndNotTheHost()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        var speakerCode = Admitted(host, Speaker, speaker);
        var listenerCode = Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "I check for traps" }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(StampedLinesFor(listener, host, transport));

        Assert.Equal(speakerCode.Value, line.Peer);
        Assert.NotEqual(listenerCode.Value, line.Peer);
        // NOT THE HOST: the host is not in its own audience -- RosterBroadcast inserts its
        // roster entry separately for exactly that reason -- so membership here IS the
        // non-host property A-2.34 is about.
        Assert.Contains(host.Audience.Recipients, peer => peer.PeerCode.Value == line.Peer);
    }

    // ORDER IS THE HOST'S (R-2.4). Fails if: a member can choose where its line lands, or the host
    // mints a sequence below 1 -- which the decode door refuses, so a zero would make the line
    // vanish at every receiver while looking sent.
    [Fact]
    public void TheHostMintsTheSequenceAndItIsUsable()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "first" }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(StampedLinesFor(listener, host, transport));

        Assert.True(line.Sequence >= 1, "the host is the sole minter and issues from 1");
        Assert.True(line.TryToEntry(out _), "a line the decode door refuses reaches nobody");
    }

    // A-2.35, THE SENDER'S HALF. Fails if: an over-long message is truncated, or dropped in silence.
    //
    // THREE OUTCOMES AND ONLY ONE PASSES. This asserts the refusal NAMES ITS FAULT and that the
    // text was not quietly shortened -- the failure mode where the sender believes they said
    // something they did not.
    //
    // NO NUMBER IS PINNED. A-2.35 asserts none and says the value is engineering's, so the fixture
    // builds its over-long input RELATIVE to the configured bound. A test hard-coding 2000 would
    // fail the day somebody tunes it, for no reason the criterion recognises.
    [Fact]
    public void AnOverLongMessageIsRefusedWithTheFaultNamed()
    {
        var limits = MessageLimits.Default;
        var tooLong = new string('a', limits.MaxLength + 1);

        var draft = MessageDraft.Compose(tooLong, limits);

        Assert.False(draft.IsAccepted);
        Assert.Equal(MessageFault.TooLong, draft.Fault);
        Assert.NotNull(draft.Reason);
        Assert.Null(draft.Text);
    }

    // THE TRUNCATION ARM, SEPARATELY. Fails if: a refusal hands back a shortened body that a caller
    // would then send. Asserting only the fault above would pass a build that refused AND supplied
    // text, which is the silent-truncation outcome wearing a receipt.
    [Fact]
    public void ARefusedMessageCarriesNoTextToSendInstead()
    {
        var limits = MessageLimits.Default;

        var draft = MessageDraft.Compose(new string('a', limits.MaxLength + 1), limits);

        Assert.Null(draft.Text);
        Assert.NotEqual(limits.MaxLength, draft.Text?.Length ?? -1);
    }

    // THE BOUND THE CHARACTER COUNT CANNOT EXPRESS. Fails if: a message inside the character bound
    // but far outside the byte bound is accepted.
    //
    // A grapheme cluster carries arbitrarily many combining marks, so N characters has no finite
    // byte ceiling -- DisplayName records the same measurement for names. A build bounded only on
    // characters is bounded against what a person perceives and not against what the wire carries,
    // which is the hostile case R-2.19 names.
    [Fact]
    public void AMessageInsideTheCharacterBoundCanStillBeTooLarge()
    {
        var limits = new MessageLimits { MaxLength = 8, BytesPerCharacter = 2 };
        var heavy = new string('e', 4) + new string('́', 40);

        var draft = MessageDraft.Compose(heavy, limits);

        Assert.False(draft.IsAccepted);
        Assert.Equal(MessageFault.TooLarge, draft.Fault);
    }

    // A-2.35 AT THE HOST, WHICH THE SENDER'S CHECK CANNOT STAND IN FOR. Fails if: an over-long
    // message that arrives from a peer running its own client is stamped and rebroadcast.
    //
    // A PEER IS NOT OBLIGED TO RUN OUR SENDING CODE. This fixture builds the payload directly,
    // exactly as a hostile client would, and asserts nothing goes out.
    [Fact]
    public void AnOverLongArrivalIsNotStampedOrRebroadcast()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        var tooLong = new string('a', MessageLimits.Default.MaxLength + 1);
        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = tooLong }));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Empty(StampedLinesFor(listener, host, transport));
    }

    // THE ANTI-VACUITY CONTROL FOR THE TEST ABOVE, AND WITHOUT IT THAT ONE PROVES LESS THAN IT
    // LOOKS. Fails if: nothing is ever rebroadcast, which would make the empty assertion above pass
    // for a reason unrelated to the bound.
    [Fact]
    public void AMessageInsideTheBoundIsRebroadcast()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "short enough" }));
        host.Tick(TimeSpan.Zero, Now);

        Assert.NotEmpty(StampedLinesFor(listener, host, transport));
    }

    // THE SECTION GUARD, REQUIRED BY SessionContentCodec's own comment. Delete `Saying` from
    // Vetted's rebuild and this is what reddens.
    //
    // Vetted REBUILDS the document from an enumerated member list, so a section added to
    // SessionContent and forgotten there is silently dropped on decode -- sender sets it, wire
    // carries it, receiver never sees it, and nothing fails. Measured on DMXENG-118: there is no
    // general guard for deletion, every section needs its own.
    //
    // ASSERTED WITH NO ROSTER PRESENT, for the reason the Entries guard is: a member's message
    // carries no roster, so a guard that only held when one was present would not hold on the case
    // that actually travels.
    [Fact]
    public void ASayingSectionSurvivesVettingWithNoRosterPresent()
    {
        var encoded = SessionContentCodec.Encode(new SessionContent { Saying = "still here" });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.Equal("still here", decoded!.Saying);
        Assert.Null(decoded.Roster);
    }

    /// <summary>
    /// Every stamped line the host sent that <paramref name="member"/> can actually open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE POPULATION IS "ENTRIES THIS MEMBER CAN DECRYPT", NOT "ENVELOPES SENT".</b> Admitting a
    /// participant publishes a roster, so the transport is never empty and an assertion over
    /// everything sent would pass or fail on traffic that has nothing to do with chat. That is the
    /// intake error this helper exists to prevent, and the over-long test below is exactly where it
    /// would have produced a confident wrong answer.
    /// </para>
    /// <para>
    /// <b>A wrong key THROWS rather than returning null</b> — <c>AesGcm.Decrypt</c> raises an
    /// authentication-tag mismatch — so envelopes sealed for somebody else are skipped by catching,
    /// not by a null check. Written after the first version of this helper crashed on the roster
    /// broadcast sealed for the other member.
    /// </para>
    /// </remarks>
    private static List<StreamLine> StampedLinesFor(
        SessionKeyExchange member, SessionCoordinator host, CapturingTransport transport)
    {
        var code = host.Host.Code!.Value;
        var key = member.DeriveSharedKey(host.HostKeys!.PublicKey, code);
        var associatedData = WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload);
        var lines = new List<StreamLine>();

        foreach (var sent in transport.Sent)
        {
            if (!EnvelopeCodec.TryDecode(sent, out var envelope)
                || envelope!.TryGetSealedPayload() is not { } payload)
            {
                continue;
            }

            byte[] opened;

            try
            {
                opened = SessionCipher.Open(key, payload, associatedData);
            }
            catch (CryptographicException)
            {
                continue;
            }

            if (SessionContentCodec.TryDecode(opened, out var content) && content!.Entries is { } entries)
            {
                lines.AddRange(entries);
            }
        }

        return lines;
    }

    private static SessionCoordinator Hosting(out CapturingTransport transport)
    {
        transport = new CapturingTransport();
        var host = new SessionCoordinator(
            transport,
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: new SilentLog(),
            capabilities: SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return host;
    }

    private static PeerCode Admitted(SessionCoordinator host, string code, SessionKeyExchange keys)
    {
        var peerCode = PeerCodes.Of(code);
        host.ReceiveJoinRequest(peerCode, keys.PublicKey, Now);
        host.Admit(peerCode);
        return peerCode;
    }

    private static WireEnvelope SealedBy(
        SessionKeyExchange member, SessionCoordinator host, SessionContent content)
    {
        var code = host.Host.Code!.Value;
        var sealedPayload = SessionCipher.Seal(
            member.DeriveSharedKey(host.HostKeys!.PublicKey, code),
            SessionContentCodec.Encode(content),
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(code, sealedPayload);
    }

    private sealed class SilentLog : ISessionTransportLog
    {
        public void Information(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Warning(Exception exception, string message)
        {
        }
    }

    private sealed class CapturingTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
