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
    /// Every factory in <paramref name="source"/>, and how each one produces its envelope.
    /// </summary>
    internal static IReadOnlyList<(string Name, bool Constructs, bool DelegatesToSibling)> Factories(
        string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var body = WithoutComments(source);

        return Declaration.Matches(body)
            .Select(match =>
            {
                var name = match.Groups["name"].Value;
                var declaration = BodyOf(body, match.Index + match.Length);

                return (
                    Name: name,
                    Constructs: Constructs(declaration),
                    DelegatesToSibling: declaration.Contains(name + "(", StringComparison.Ordinal));
            })
            .ToList();
    }

    /// <summary>
    /// Factory names whose overloads are not all accounted for by one construction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An overload is ACCOUNTED FOR if it either constructs, or delegates to a sibling of its own
    /// name.</b> A name is an offence when more than one of its overloads constructs, OR when any of
    /// them does neither — because "neither" means it reaches a construction by some other route,
    /// and that route is not visible to the row that vouches for it.
    /// </para>
    /// <para>
    /// <b>THE SECOND CLAUSE EXISTS BECAUSE THE FIRST ONE ALONE COULD BE SATISFIED FALSELY, AND THAT
    /// WAS A REAL HOLE IN THIS FILE.</b> The original rule asked only <i>does the body contain a
    /// construction</i>. Two overloads that both delegate to a private <c>Build</c> helper contain
    /// none, so both read as non-constructing and the guard passed — <b>on precisely the defect it
    /// exists to catch, wearing one indirection.</b>
    /// </para>
    /// <para>
    /// feature-engineer-2 named the shape on a different guard: <i>deleting an entry reddens it;
    /// replacing it with a false one leaves it green.</i> A completeness check that asks whether an
    /// author wrote something, rather than whether what they wrote is true, has this hole by
    /// construction. Asking <b>accounted for</b> rather than <b>constructs</b> closes it.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<string> NamesCoveringTwoConstructions(string source)
    {
        var factories = Factories(source);

        var twoConstructions = factories
            .Where(factory => factory.Constructs)
            .GroupBy(factory => factory.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        var unaccounted = factories
            .Where(factory => !factory.Constructs && !factory.DelegatesToSibling)
            .Select(factory => factory.Name);

        return twoConstructions.Concat(unaccounted).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Whether a factory body builds an envelope of its own.
    /// </summary>
    /// <remarks>
    /// <b>BOTH SPELLINGS, AND MISSING THE SECOND ONE MADE THE FIRST VERSION OF THIS GUARD NEARLY
    /// WORTHLESS.</b> Four of <c>WireEnvelope</c>'s factories construct with a TARGET-TYPED
    /// <c>new(...)</c> — the idiomatic form in a method whose return type already names the type —
    /// and a detector looking only for <c>new WireEnvelope(</c> sees none of them.
    /// <para>
    /// <b>The near-miss is worth recording.</b> The real-file mutation that demonstrated this guard
    /// used <c>new WireEnvelope(</c> because that is what the deleted overload had used. Written in
    /// the file's ordinary style it would have been INVISIBLE, and the demonstration would have
    /// passed for a reason that did not generalise — a positive control that fires on the one
    /// spelling you happened to choose is not a control.
    /// </para>
    /// <para>
    /// A bare <c>new(</c> inside a body that returns <see cref="WireEnvelope"/> is taken to be that
    /// envelope. It could in principle be an unrelated target-typed construction; none of these
    /// factories has one, and the direction of the error is safe — it flags rather than excuses.
    /// </para>
    /// </remarks>
    private static bool Constructs(string body) =>
        body.Contains("new WireEnvelope(", StringComparison.Ordinal)
        || body.Contains("new(", StringComparison.Ordinal);

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
