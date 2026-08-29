using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Which factory overloads build their own envelope, and which delegate to a sibling.
/// </summary>
/// <remarks>
/// <para>
/// <b>The distinction <c>EveryMessageAClientSendsIsSentTests</c> keys on is the factory NAME</b>, so
/// one row vouches for every overload sharing that name. That is correct when the overloads are one
/// protocol action seen with and without an optional argument — a wrapper delegating to a sibling
/// cannot produce anything the sibling would not. <b>It is wrong the moment two overloads under one
/// name construct SEPARATELY</b>: the row goes green because the trigger exercised one of them, and
/// the other is unreachable behind a satisfied row.
/// </para>
/// <para>
/// <b>Why this reads source rather than reflection.</b> Reflection sees that
/// <c>ForJoinRequest</c> has two overloads and cannot see that one is <c>=> ForJoinRequest(code,
/// publicKey, DisplayName.None)</c>. The question is about the BODY, and the body is not in the
/// metadata. Reading IL would answer it too and was considered; it needs an opcode-length table to
/// walk safely, which is more machinery than this guard is worth, and its failure mode is silent
/// where a source reader's is loud. <c>ShippedCopyCorpus</c> and <c>TlsBypassFenceTests</c> already
/// read source in this suite.
/// </para>
/// <para>
/// <b>Pure over TEXT rather than over a path, so the failing case is a permanent test rather than a
/// mutation somebody once ran.</b> The gap this guard exists for currently has ZERO instances —
/// <c>ForJoinRequest</c>'s deadline overload was deleted by DMXENG-41 — so a guard wired only to the
/// real file would be green with nothing to find, which is indistinguishable from a guard that
/// cannot find anything. Taking a string means the positive case is asserted every run.
/// </para>
/// <para>
/// <b>Its own fragility, stated.</b> A source reader breaks loudly on a formatting change and
/// silently on a construction spelled differently — <c>WireEnvelope.Make(...)</c>, say, or a factory
/// that returns a sibling's output through a private helper. Today every construction in that file
/// is <c>new WireEnvelope(</c>, and <c>TheReaderSeesTheFileItClaimsTo</c> pins that the reader finds
/// the real factories rather than an empty set.
/// </para>
/// </remarks>
internal static class FactoryOverloads
{
    private static readonly Regex Declaration = new(
        @"^\s*(?:public|internal)\s+static\s+WireEnvelope\s+(?<name>\w+)\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Every factory in <paramref name="source"/>, and whether each builds its own envelope.
    /// </summary>
    internal static IReadOnlyList<(string Name, bool Constructs)> Factories(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var body = WithoutComments(source);

        return Declaration.Matches(body)
            .Select(match => (
                Name: match.Groups["name"].Value,
                Constructs: BodyOf(body, match.Index + match.Length)
                    .Contains("new WireEnvelope(", StringComparison.Ordinal)))
            .ToList();
    }

    /// <summary>
    /// Factory names under which more than one overload constructs independently.
    /// </summary>
    /// <remarks>
    /// <b>Two constructing overloads is the failure, not two overloads.</b> A name with three
    /// overloads of which one constructs and two delegate is one protocol action with two
    /// conveniences, and one row covers it honestly.
    /// </remarks>
    internal static IReadOnlyList<string> NamesCoveringTwoConstructions(string source) =>
        Factories(source)
            .Where(factory => factory.Constructs)
            .GroupBy(factory => factory.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

    /// <summary>
    /// Comment lines removed, because this file's own prose and the source it reads both quote the
    /// construction it looks for.
    /// </summary>
    private static string WithoutComments(string source) =>
        string.Join(
            "\n",
            source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    /// <summary>
    /// The body of the member whose parameter list opens at <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// <b>Bounded rather than run to the next factory.</b> Taking everything up to the following
    /// declaration would attribute a neighbouring member's construction to whichever factory
    /// preceded it — and <c>WireEnvelope.FromWire</c>, which constructs, sits among them. Both body
    /// forms are handled: an expression body ends at its semicolon, a block body at its matching
    /// brace.
    /// </remarks>
    private static string BodyOf(string source, int from)
    {
        // ONE, NOT ZERO: the declaration match consumed the opening parenthesis, so the scan starts
        // INSIDE the parameter list. Starting at zero sent the first ')' to -1 and no later '=>' or
        // '{' was ever seen at depth 0 -- every body read as empty, every factory read as
        // non-constructing, and the guard returned an empty offender list whatever it was given.
        // Caught by TwoConstructingOverloadsUnderOneNameAreCaught and TheReaderSeesTheFileItClaimsTo
        // on the first run, which is the entire reason those two exist.
        var depth = 1;

        for (var index = from; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '(':
                    depth++;
                    break;

                case ')':
                    depth--;
                    break;

                case '=' when depth == 0 && index + 1 < source.Length && source[index + 1] == '>':
                    return Until(source, index, ';');

                case '{' when depth == 0:
                    return Braced(source, index);
            }
        }

        return string.Empty;
    }

    private static string Until(string source, int from, char terminator)
    {
        var end = source.IndexOf(terminator, from);
        return end < 0 ? source[from..] : source[from..end];
    }

    private static string Braced(string source, int from)
    {
        var depth = 0;

        for (var index = from; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[from..index];
            }
        }

        return source[from..];
    }
}
