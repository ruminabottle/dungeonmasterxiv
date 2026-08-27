using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.5b is a forced-failure criterion, so these test the failure states rather than the success
/// path: what the user is told when it does not work.
/// </summary>
public class SessionFailureMessageTests
{
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
