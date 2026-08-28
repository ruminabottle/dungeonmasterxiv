using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A peer code may only enter through <see cref="PeerCode"/> (T-47, the durable half of BUG-57).
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived from the assembly, not from a list of files.</b> The ticket's rule is that a validated
/// type must be the ONLY door, and the failure it guards against is somebody adding an eleventh
/// entrance next month. A test that named the ten known ones would pass on the day it was written
/// and never fail again. This one reflects over every member in Core and asks a question that a new
/// member has to answer.
/// </para>
/// <para>
/// <b>The allowlist below IS the remaining work, and that is deliberate.</b> Each entry is a door
/// still taking a raw <c>string</c>, with the reason it is open. Two kinds are in there and only one
/// of them ever closes:
/// </para>
/// <list type="bullet">
/// <item><b>The wire DTO</b> — <c>RosterEntry</c> carries primitives because that is what the JSON
/// format declares, and changing a serialised field's shape is a wire change, not a refactor (D-14).
/// This one is permanent.</item>
/// <item><b>The held-file seam — GONE, and its removal is the point.</b> Six entries lived here
/// while <c>SessionCoordinator.cs</c> was held by DMXENG-12: its three <c>string</c> methods, and
/// the three <c>AdmissionControl</c> overloads that existed only to feed them. Those signatures now
/// take <see cref="PeerCode"/>, the overloads are deleted, and the six entries went with them while
/// this file stayed green. <b>The control caught the intermediate state</b> — doors closed, entries
/// left behind — which is what checking an allowlist in BOTH directions buys.</item>
/// </list>
/// <para>
/// <b>ONE SHAPE REMAINS INVISIBLE, AND IT IS OCCUPIED TODAY (found fixing BUG-72).</b> Compiler
/// generated members are skipped, and <c>RosterEntry.Deconstruct</c> is one: it takes
/// <c>out string PeerCode</c>, which is a raw-string door by exactly the definition below, and it
/// carries <c>[CompilerGenerated]</c> because the positional record synthesises it. Measured, not
/// inferred — it is why widening to <c>out</c>/<c>ref</c> parameters did not add a third entry to
/// the allowlist.
/// </para>
/// <para>
/// Skipping it is right rather than convenient: nobody can add that member without adding the
/// record component that generates it, and that component is the wire format the two entries below
/// already cover. It is recorded because a reader who knows <c>out string</c> is now swept would
/// otherwise conclude no such member exists — and because the exclusion is a rule about HOW a
/// member came to be, which is a different kind of thing from the rules about what it looks like.
/// </para>
/// <para>
/// <b>AND THE SWEEP SEES CORE ONLY.</b> It scopes to <c>typeof(PeerCode).Assembly</c>, so a
/// peer-code door added in the Relay assembly would not be found here. Declared rather than closed,
/// on the same reasoning as the <c>Windows/</c> boundary: widening the sweep MOVES the edge instead
/// of removing it, and some assembly has to be the last one looked in. The Relay mentions
/// <c>PeerCode</c> in no file today, so nothing is escaping — but that is a fact about today, which
/// is exactly the kind of fact a guard should not leave a reader to infer from silence.
/// </para>
/// <para>
/// <b>What a green run does NOT mean.</b> It means no member takes or exposes a peer code as a bare
/// string outside the listed exceptions. It says nothing about whether the value in a
/// <see cref="PeerCode"/> is the RIGHT one — that is the neighbouring tests' job.
/// </para>
/// </remarks>
public sealed class PeerCodeIsTheOnlyDoorTests
{
    // Every raw-string door still open, and why. Shrinking this list is the point; growing it needs
    // an argument. Format: "DeclaringType.Member".
    private static readonly Dictionary<string, string> DoorsDeliberatelyLeftOpen = new(StringComparer.Ordinal)
    {
        ["DungeonMasterXIV.Net.RosterEntry.PeerCode"] =
            "WIRE DTO, PER #86 -- not a door we missed. The Deployment Manager ruled on exactly "
            + "this question for DisplayName: 'Put the gate at the decode boundary so it is the "
            + "only door' and 'string stays in RosterEntry -- the wire format does not change.' A "
            + "DTO is not a door; it is the SHAPE OF WHAT CROSSED ONE, and the door is Vetted "
            + "inside TryDecode. DisplayName -- a validated type everywhere else in Core -- sits "
            + "beside this field as a bare string for the same reason. D-14 reaches the same place "
            + "independently: changing a serialised field's shape is a wire change. PERMANENT.",
        ["DungeonMasterXIV.Net.RosterEntry.RosterEntry"] =
            "The same wire DTO, per #86, reached through its positional constructor. Listed "
            + "separately because it IS a separate door -- the sweep found it when the property "
            + "alone was listed, which is the point of deriving this rather than naming members "
            + "by hand. PERMANENT.",

    };

    // THE UNIVERSAL. Fails when a member takes a peer code as a bare string without being listed --
    // which is what an eleventh door looks like the moment somebody adds one.
    [Fact]
    public void NoMemberTakesAPeerCodeAsABareString()
    {
        var doors = RawStringDoors()
            .Where(door => !DoorsDeliberatelyLeftOpen.ContainsKey(door))
            .ToList();

        Assert.True(
            doors.Count == 0,
            "These take or expose a peer code as a raw string and are not on the allowlist:\n  "
            + string.Join("\n  ", doors)
            + "\n\nEither use PeerCode, or add an entry to DoorsDeliberatelyLeftOpen saying why not.");
    }

    // THE CONTROL, and without it the universal above is worth nothing: if the sweep found no
    // members at all -- wrong assembly, wrong binding flags, a naming convention that moved -- every
    // run would pass while asking nothing. This proves the sweep can SEE doors.
    //
    // It asserts against the allowlist rather than a count, because a count goes stale the first
    // time anybody adds a method. The allowlist entries are real members that really do take
    // strings, so every one of them must be found by the same sweep the universal uses.
    [Fact]
    public void TheSweepActuallyFindsDoorsSoTheUniversalMeansSomething()
    {
        var found = RawStringDoors().ToHashSet(StringComparer.Ordinal);

        var listedButNotFound = DoorsDeliberatelyLeftOpen.Keys.Where(door => !found.Contains(door)).ToList();

        Assert.True(
            listedButNotFound.Count == 0,
            "The allowlist names doors the sweep cannot see, so the sweep is not looking where it "
            + "thinks it is -- or these were closed and the entry was left behind:\n  "
            + string.Join("\n  ", listedButNotFound));
    }

    // BUG-73. A type whose SHORT name collides with an allowlisted one must not inherit the
    // exemption. Red before the re-key: the fixture's key is "RosterEntry.PeerCode", which is a
    // live allowlist entry, so the door is filtered out and the sweep reports nothing.
    //
    // The control test cannot cover this and that is what made it survive: the genuine
    // RosterEntry.PeerCode is still found, so "every listed door is still found" stays satisfied.
    // The key was assumed 1:1 with the door and is in fact 1:many.
    [Fact]
    public void ATypeSharingAShortNameWithAnAllowlistedOneDoesNotInheritTheExemption()
    {
        var doors = RawStringDoorsIn(new[] { typeof(Fixtures.RosterEntry) }).ToList();

        Assert.NotEmpty(doors);
        Assert.DoesNotContain(
            doors,
            door => DoorsDeliberatelyLeftOpen.ContainsKey(door));
    }

    // BUG-72. Four member shapes carry a peer code as a raw string while satisfying none of the
    // sweep's three conditions. Each fixture is planted alone, so a failure names one shape rather
    // than a combination.
    [Theory]
    [InlineData(typeof(Fixtures.OutParameter), "an out parameter -- ParameterType is string&")]
    [InlineData(typeof(Fixtures.RefParameter), "a ref parameter -- same, and GetElementType is the fix")]
    [InlineData(typeof(Fixtures.PlainField), "a field -- GetFields was never called")]
    [InlineData(typeof(Fixtures.StringList), "a collection -- the element type carries it")]
    [InlineData(typeof(Fixtures.StringArray), "an array -- likewise")]
    public void EveryShapeThatCarriesAPeerCodeIsADoor(Type shape, string why)
    {
        Assert.True(
            RawStringDoorsIn(new[] { shape }).Any(),
            $"The sweep cannot see {shape.Name}: {why}. A door it cannot see is a door.");
    }

    // THE VACUITY CONTROL ON THE WIDENING, and without it the theory above is satisfied by a
    // derivation that returns everything named peerCode regardless of type. Both members here are
    // named peerCode and neither carries a raw string -- the second is the validated type itself,
    // reached through an out parameter, which is exactly the signature PeerCode.TryParse already
    // has. A widening that flagged it would report the door as its own violation.
    [Fact]
    public void AMemberNamedPeerCodeThatCarriesNoRawStringIsNotADoor()
    {
        Assert.Empty(RawStringDoorsIn(new[] { typeof(Fixtures.NotRawStrings) }));
    }

    // Shape fixtures. In the test assembly deliberately: the universal sweeps Core, so nothing here
    // can reach it, and probe types never belong in shipped code.
    private static class Fixtures
    {
        // Short name collides with a live allowlist entry; full name cannot.
        internal sealed class RosterEntry
        {
            public string PeerCode { get; init; } = string.Empty;
        }

        internal sealed class OutParameter
        {
            internal static bool TryGet(out string peerCode)
            {
                peerCode = string.Empty;
                return true;
            }
        }

        internal sealed class RefParameter
        {
            internal static void Set(ref string peerCode) => peerCode = string.Empty;
        }

        internal sealed class PlainField
        {
            internal string peerCode = string.Empty;
        }

        internal sealed class StringList
        {
            internal static void Admit(IReadOnlyList<string> peerCode) => _ = peerCode;
        }

        internal sealed class StringArray
        {
            internal static void Admit(string[] peerCode) => _ = peerCode;
        }

        internal sealed class NotRawStrings
        {
            // Assigned explicitly. Never read by design -- what matters is its TYPE, not its value --
            // and an unassigned field is CS0649, which would be a new warning for a deliberate
            // construct.
            internal int peerCode = 0;

            internal static bool TryGet(out PeerCode peerCode)
            {
                peerCode = default;
                return false;
            }
        }
    }

    // Every member in Core that handles a peer code as a bare string, by the two shapes it can take:
    // a parameter named peerCode, or a property named PeerCode.
    //
    // Named rather than typed, deliberately. A sweep that looked for "parameters of type PeerCode"
    // could only ever find the doors already closed -- the open ones are exactly the ones typed
    // string, so the NAME is the only handle available. That is also why the case-insensitive
    // comparison matters here: SessionCoordinator declares no PeerCode property and threads the
    // value through as the camelCase parameter, which is precisely how a case-sensitive grep missed
    // it when this chunk was scoped.
    private static IEnumerable<string> RawStringDoors() =>
        RawStringDoorsIn(typeof(PeerCode).Assembly.GetTypes());

    /// <summary>The same sweep, over a supplied set of types, so its RULES can be tested directly.</summary>
    /// <remarks>
    /// The universal above scopes to Core's assembly, which means a regression test for the sweep's
    /// own rules would otherwise have to plant probe types IN CORE. Taking the types as an argument
    /// lets the shape fixtures live in this file, where they belong, instead of in shipped code.
    /// </remarks>
    private static IEnumerable<string> RawStringDoorsIn(IEnumerable<Type> types)
    {
        const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var type in types.Where(t => !IsCompilerGenerated(t)))
        {
            // KEYED ON THE FULL NAME (BUG-73). The short name is not unique, so a type sharing one
            // with an allowlisted type inherited its exemption AND its stated reason -- which here
            // reads PERMANENT and cites a wire-format ruling. The control could not catch it: the
            // genuine door is still found, so "every listed door is still found" stayed satisfied.
            // The key was assumed 1:1 with the door and was in fact 1:many.
            var owner = type.FullName ?? type.Name;

            foreach (var property in type.GetProperties(Everything))
            {
                if (Names(property.Name) && CarriesARawString(property.PropertyType))
                {
                    yield return $"{owner}.{property.Name}";
                }
            }

            // FIELDS (BUG-72). GetFields was never called, so a field was not a door no matter what
            // it held. Compiler-generated backing fields are excluded by name, as everywhere here.
            foreach (var field in type.GetFields(Everything))
            {
                if (!IsCompilerGenerated(field) && Names(field.Name) && CarriesARawString(field.FieldType))
                {
                    yield return $"{owner}.{field.Name}";
                }
            }

            foreach (var method in type.GetMethods(Everything).Cast<MethodBase>().Concat(type.GetConstructors(Everything)))
            {
                if (IsCompilerGenerated(method))
                {
                    continue;
                }

                if (method.GetParameters().Any(p => Names(p.Name) && CarriesARawString(p.ParameterType)))
                {
                    yield return $"{owner}.{DisplayNameOf(method)}";
                }
            }
        }
    }

    /// <summary>Whether a member of this type hands someone a peer code as a raw <c>string</c>.</summary>
    /// <remarks>
    /// <para>
    /// <b>Was <c>== typeof(string)</c>, which three shapes walked past (BUG-72).</b> An
    /// <c>out</c>/<c>ref</c> parameter has type <c>string&amp;</c>, an array holds strings without
    /// being one, and a collection holds them in a type argument. Each carries the value just as
    /// plainly as a bare parameter does.
    /// </para>
    /// <para>
    /// <b>It asks what the type CARRIES, and that is the load-bearing choice.</b> The alternative
    /// -- treat anything named <c>peerCode</c> as a door -- would flag
    /// <c>PeerCode.TryParse(string?, out PeerCode peerCode)</c>, reporting the validated type's own
    /// entrance as a violation of the rule it enforces.
    /// <c>AMemberNamedPeerCodeThatCarriesNoRawStringIsNotADoor</c> is what holds that line.
    /// </para>
    /// <para>
    /// Generous on purpose: any generic argument or element type being a string is enough. The name
    /// filter is what makes that safe, and the failure direction is a false FAIL, which is the one
    /// somebody notices.
    /// </para>
    /// </remarks>
    private static bool CarriesARawString(Type type)
    {
        if (type.IsByRef || type.IsArray)
        {
            return CarriesARawString(type.GetElementType()!);
        }

        if (type == typeof(string))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(CarriesARawString);
    }

    private static bool Names(string? name) =>
        string.Equals(name, "peerCode", StringComparison.OrdinalIgnoreCase);

    private static string DisplayNameOf(MethodBase method) =>
        method is ConstructorInfo ? method.DeclaringType!.Name : method.Name;

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.Name.Contains('<', StringComparison.Ordinal)
        || member.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false).Length != 0;
}
