using System;
using System.Linq;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.5b is a forced-failure criterion, so these test the failure states rather than the success
/// path: what the user is told when it does not work.
/// </summary>
public class SessionFailureMessageTests
{
    /// <summary>
    /// Every failure's sentence, as reviewed. A change here is a change a person read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An allowlist, deliberately, and not a list of forbidden phrases (BUG-49).</b> The defect
    /// this replaces was <c>RelayUnreachable</c> claiming "This is not your connection" — a
    /// statement about cause that a firewall rejecting with a TCP RST disproves, since a refusal is
    /// evidence something answered and that something can sit on the user's side. **No regex finds
    /// that.** The detector written to catch it searched for "not your network" while the sentence
    /// said "not your connection", and read clean.
    /// </para>
    /// <para>
    /// <b>So the job is to force a reading, not to recognise badness.</b> Adding a
    /// <see cref="SessionFailure"/> fails <see cref="EveryFailureHasASentenceSomebodyRead"/> until
    /// its sentence is written here, and changing any wording fails
    /// <see cref="EachSentenceIsTheOneThatWasReviewed"/> until this table is updated. Both are
    /// deliberate: user-facing text that can change without a second reader is how this one shipped.
    /// </para>
    /// <para>
    /// It also closes the enumeration gap. This file guarded <b>three</b> of eight sentences; the
    /// set below is checked against <see cref="Enum.GetValues{T}"/> at run time, so it cannot fall
    /// behind the enum the way a hand-kept array does.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<SessionFailure, string> ReviewedSentences =
        new Dictionary<SessionFailure, string>
        {
            [SessionFailure.None] = string.Empty,

            [SessionFailure.RelayUnreachable] =
                "The relay is not responding — the connection was refused or could not be made. "
                + "Reachability is a property of the path, so this does not say which end is at "
                + "fault: a firewall that rejects the connection outright looks exactly the same. "
                + "Check your own network as well as the relay address in settings, or point the "
                + "plugin at a different relay.",

            [SessionFailure.ConnectionLost] =
                "The connection to the relay dropped. The relay was reachable a moment ago, so check your "
                + "own network first.",

            [SessionFailure.SessionCodeNotActive] =
                "No session is running under that code. Check the code with your DM — codes belong to a "
                + "session that is live now, so one from last week will not work until they start again.",

            [SessionFailure.PluginBehindRelay] =
                "This plugin is too old for that relay. Update the plugin and try again — the relay "
                + "speaks a newer version of the session protocol than this build does.",

            [SessionFailure.RelayBehindPlugin] =
                "That relay is older than this plugin and cannot speak to it. Nothing on your side is "
                + "wrong: the relay has to be updated, or you can point the plugin at a different one in "
                + "settings.",

            [SessionFailure.RelayAddressUnreadable] =
                "The relay address in settings could not be read, so nothing was contacted — this says "
                + "nothing about the relay or about your own network. Check what you typed in settings: "
                + "it has to be a full address beginning with wss://, like " + RelayEndpoint.Default + ".",

            [SessionFailure.ConnectionNeverOpened] =
                "The connection to the relay never finished opening — it was still being attempted when "
                + "time ran out. That can be the relay, and it can equally be something between you and "
                + "it: a firewall that silently drops a connection looks exactly like a relay that is "
                + "not there, where one that refuses fails immediately. Check your own network as well "
                + "as the relay address in settings.",

            [SessionFailure.RegistrationNotAnswered] =
                "The relay accepted the connection but never confirmed the session code. The relay is "
                + "reachable, so this is not your network — try starting the session again, and if it "
                + "keeps happening the relay is not answering registrations.",
        };

    // Derived from the enum rather than restated, so a new failure cannot arrive with nobody having
    // read what it tells the user. Fails on BOTH directions of drift: a member with no sentence, and
    // a sentence for a member that no longer exists.
    [Fact]
    public void EveryFailureHasASentenceSomebodyRead()
    {
        Assert.Equal(
            Enum.GetValues<SessionFailure>().OrderBy(failure => failure).ToList(),
            ReviewedSentences.Keys.OrderBy(failure => failure).ToList());
    }

    // Fails if any user-facing sentence changes without this table changing with it. That is the
    // point rather than a cost: BUG-49 was a wording defect that survived because wording could move
    // without a second reader.
    [Fact]
    public void EachSentenceIsTheOneThatWasReviewed()
    {
        foreach (var (failure, reviewed) in ReviewedSentences)
        {
            Assert.Equal(reviewed, SessionFailureMessage.For(failure));
        }
    }

    private static readonly SessionFailure[] RealFailures =
    {
        SessionFailure.RelayUnreachable,
        SessionFailure.ConnectionLost,
        SessionFailure.SessionCodeNotActive,
    };

    // A-1.5b's core requirement. Fails if: two failures produce the same sentence — at which point
    // they are no longer distinguishable to the user, which is the whole criterion.
    [Fact]
    public void TheThreeFailuresAreDistinguishableToTheUser()
    {
        var messages = RealFailures.Select(SessionFailureMessage.For).ToList();

        Assert.Equal(3, messages.Distinct().Count());
        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
    }

    // Fails if: any message degrades to the generic text R-1.8 forbids by name. "Connection failed"
    // is the specific phrasing the requirement rules out, because it is true of all three and
    // useful for none.
    [Theory]
    [InlineData("connection failed")]
    [InlineData("something went wrong")]
    [InlineData("please wait")]
    public void NoMessageIsGenericFailureText(string forbidden)
    {
        Assert.All(RealFailures, failure =>
            Assert.DoesNotContain(forbidden, SessionFailureMessage.For(failure), StringComparison.OrdinalIgnoreCase));
    }

    // R-1.7a's forbidden-phrasing list, applied to copy R-1.7a does not itself supply. Fails if:
    // failure text drifts into a privacy claim. Each of these is false under D-8, and the last is
    // false even with D-11 encryption in place.
    [Theory]
    [InlineData("anonymous")]
    [InlineData("private")]
    [InlineData("we can't see anything")]
    [InlineData("no one can see your session")]
    public void NoMessageUsesAForbiddenPhrasing(string forbidden)
    {
        Assert.All(RealFailures, failure =>
            Assert.DoesNotContain(forbidden, SessionFailureMessage.For(failure), StringComparison.OrdinalIgnoreCase));
    }

    // BUG-37. Fails if: the sentence for a malformed address asserts something the failure never
    // established. Nothing was contacted, so any claim about the relay's health — in either
    // direction — is invented. Paired against RelayUnreachable's sentence because the defect was
    // that they were the SAME sentence.
    [Fact]
    public void TheUnreadableAddressSentenceClaimsNothingAboutTheRelay()
    {
        var message = SessionFailureMessage.For(SessionFailure.RelayAddressUnreadable);

        Assert.NotEqual(SessionFailureMessage.For(SessionFailure.RelayUnreachable), message);
        Assert.DoesNotContain("unreachable", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not responding", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nothing was contacted", message, StringComparison.OrdinalIgnoreCase);
    }

    // The same guards the three R-1.8 failures get, applied to the value this bug added. NOTE: the
    // arrays above still name three of the enum's seven values, so PluginBehindRelay,
    // RelayBehindPlugin and RegistrationNotAnswered remain unguarded by them. That gap predates
    // BUG-37 and is reported rather than widened here.
    [Theory]
    [InlineData("connection failed")]
    [InlineData("something went wrong")]
    [InlineData("please wait")]
    [InlineData("anonymous")]
    [InlineData("private")]
    public void TheUnreadableAddressSentenceAvoidsTheForbiddenPhrasings(string forbidden)
    {
        Assert.DoesNotContain(
            forbidden,
            SessionFailureMessage.For(SessionFailure.RelayAddressUnreadable),
            StringComparison.OrdinalIgnoreCase);
    }

    // Fails if: the no-failure case starts producing text, which would put an error in front of a
    // user whose session is working.
    [Fact]
    public void TheAbsenceOfFailureSaysNothing()
    {
        Assert.Equal(string.Empty, SessionFailureMessage.For(SessionFailure.None));
    }
}
