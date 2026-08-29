using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The join flow passes a relink claim when it asks to join (R-1.5, BUG-100).
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because a comment said it existed.</b> <c>EveryMessageAClientSendsIsSentTests</c>
/// stated that "the UI supplies one" was covered by <c>TheJoinerRemembersWhoItIsTests</c>. It was
/// not: that file tests <c>RelinkMemory</c> storage and touches no view, no join and no envelope.
/// An uncovered path reads as uncovered; an uncovered path WITH A CITATION reads as covered, which
/// is why the missing test was worth more than the wrong sentence.
/// </para>
/// <para>
/// <b>The name is the fix as much as the assertions are.</b> The false citation was written in good
/// faith because <c>AStoredParticipantIsWhatAJoinWouldCarry</c> is named for a claim its body does
/// not make, so it reads like the coverage someone would be looking for. This class is named for
/// exactly what it checks, so the next citation to it resolves.
/// </para>
/// <para>
/// <b>Source-reading, and its limit is stated rather than implied.</b> This assembly cannot
/// reference the plugin, so the window cannot be constructed here; reading the file is the
/// established way round that in this project. It asserts the SHAPE OF THE CALL, not that a join
/// reaches a relay. The end-to-end coverage is the in-game check.
/// </para>
/// <para>
/// <b>And it proves the call is WRITTEN, never that it RUNS.</b> A correct call inside a branch
/// that never executes, or in a method nothing invokes, satisfies every assertion here. That is the
/// honest boundary of reading text rather than driving the window, and it is stated because the
/// weaker sentence above does not cover it: "the shape of the call" and "the call happens" are
/// different claims, and only the first one is checked.
/// </para>
/// </remarks>
public class TheJoinFlowSuppliesTheRelinkClaimTests
{
    private const string Call = "RequestJoin(";

    // THE ASSERTION. RequestJoin has three overloads and only the widest one carries the claim, so
    // the property is about the ARGUMENT COUNT rather than about the method being called at all.
    //
    // NOT the bare substring "RequestJoin": every overload satisfies it, including the one-argument
    // form that drops the claim entirely. An assertion satisfied by the defect is the defect one
    // layer up, which is the whole subject of BUG-100.
    //
    // And the third argument must not be the null literal -- BUG-41 was this value being nulled on
    // its way through, and a call site written `RequestJoin(code, name, null)` supplies nothing
    // while satisfying a count.
    [Fact]
    public void TheJoinFlowPassesAClaimAndNotJustACode()
    {
        var source = JoinFlowSource();

        // DMXENG-75 moved the button, and this asserts the file read is the one that holds it --
        // a path that resolves to the wrong file would otherwise fail the argument check below for
        // a reason no message would explain.
        Assert.Contains("class JoinRequestForm", source, StringComparison.Ordinal);

        var arguments = ArgumentsOfTheJoinCall(source);

        Assert.True(
            arguments.Count == 3,
            $"The join flow calls RequestJoin with {arguments.Count} argument(s): "
            + string.Join(" | ", arguments)
            + ". Only the three-argument overload carries the relink claim, so fewer means a "
            + "returning player silently arrives as someone new (R-1.5, BUG-100).");

        Assert.False(
            arguments[2] == "null",
            "The join flow passes a literal null as the relink claim, which supplies nothing while "
            + "still calling the widest overload. That is BUG-41's shape moved to the call site.");
    }

    /// <summary>The arguments of the join flow's <c>RequestJoin</c> call, split at top level.</summary>
    /// <remarks>
    /// <para>
    /// Split by PAREN DEPTH rather than by a comma, because the claim argument is itself a call and
    /// contains its own parentheses. Depth counting is exact for balanced source; a comma inside a
    /// string literal in this argument list would miscount it, and there is none.
    /// </para>
    /// <para>
    /// Reading the whole call rather than one line, so wrapping the arguments across lines is not a
    /// failure. The failure direction of anything this cannot parse is a wrong count and therefore a
    /// false FAIL, which is the one somebody investigates.
    /// </para>
    /// </remarks>
    private static System.Collections.Generic.IReadOnlyList<string> ArgumentsOfTheJoinCall(string source)
    {
        var at = source.IndexOf(Call, StringComparison.Ordinal);
        Assert.True(at >= 0, "The join flow no longer calls RequestJoin at all.");

        var open = at + Call.Length;
        var depth = 1;
        var arguments = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();

        for (var i = open; i < source.Length && depth > 0; i++)
        {
            var c = source[i];

            depth += c switch { '(' => 1, ')' => -1, _ => 0 };

            if (depth == 0)
            {
                break;
            }

            if (c == ',' && depth == 1)
            {
                arguments.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        Assert.True(depth == 0, "The RequestJoin call in the join flow has unbalanced parentheses.");

        if (current.ToString().Trim().Length > 0)
        {
            arguments.Add(current.ToString().Trim());
        }

        return arguments;
    }

    /// <summary>The join flow's source, with comment lines removed.</summary>
    /// <remarks>
    /// Resolved by NAMING THE FILE rather than by globbing the window directory. The sibling scans
    /// share a helper that enumerates <c>Windows/*.cs</c> top level only, which is BUG-67's defect;
    /// it was fixed in one copy and two others still carry it. Naming one file needs no enumeration
    /// and so cannot inherit that.
    /// </remarks>
    private static string JoinFlowSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "JoinRequestForm.cs");

            if (File.Exists(candidate))
            {
                return string.Join(
                    "\n",
                    File.ReadAllLines(candidate)
                        .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            }
        }

        throw new InvalidOperationException("No Windows/JoinRequestForm.cs above the test binary.");
    }
}
