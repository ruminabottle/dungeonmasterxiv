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
/// <item><b>The held-file seam</b> — <c>SessionCoordinator</c> and the <c>string</c> overloads on
/// <c>AdmissionControl</c> that exist to feed it. <c>SessionCoordinator.cs</c> is held by DMXENG-12,
/// the v0.1.5 release blocker. <b>When that merges, those entries are deleted from this list and the
/// overloads with them, and this test must still pass.</b> That is the whole remaining task, written
/// down where it cannot be forgotten.</item>
/// </list>
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
        ["RosterEntry.PeerCode"] =
            "WIRE DTO, PER #86 -- not a door we missed. The Deployment Manager ruled on exactly "
            + "this question for DisplayName: 'Put the gate at the decode boundary so it is the "
            + "only door' and 'string stays in RosterEntry -- the wire format does not change.' A "
            + "DTO is not a door; it is the SHAPE OF WHAT CROSSED ONE, and the door is Vetted "
            + "inside TryDecode. DisplayName -- a validated type everywhere else in Core -- sits "
            + "beside this field as a bare string for the same reason. D-14 reaches the same place "
            + "independently: changing a serialised field's shape is a wire change. PERMANENT.",
        ["RosterEntry.RosterEntry"] =
            "The same wire DTO, per #86, reached through its positional constructor. Listed "
            + "separately because it IS a separate door -- the sweep found it when the property "
            + "alone was listed, which is the point of deriving this rather than naming members "
            + "by hand. PERMANENT.",

        ["SessionCoordinator.ReceiveJoinRequest"] =
            "SessionCoordinator.cs is HELD by DMXENG-12 (v0.1.5 release blocker). CLOSES when it merges.",
        ["SessionCoordinator.Admit"] =
            "SessionCoordinator.cs is HELD by DMXENG-12 (v0.1.5 release blocker). CLOSES when it merges.",
        ["SessionCoordinator.Deny"] =
            "SessionCoordinator.cs is HELD by DMXENG-12 (v0.1.5 release blocker). CLOSES when it merges.",

        ["AdmissionControl.Receive"] =
            "The string overload exists ONLY to feed the held coordinator. CLOSES with it.",
        ["AdmissionControl.Admit"] =
            "The string overload exists ONLY to feed the held coordinator. CLOSES with it.",
        ["AdmissionControl.Deny"] =
            "The string overload exists ONLY to feed the held coordinator. CLOSES with it.",
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

    // Every member in Core that handles a peer code as a bare string, by the two shapes it can take:
    // a parameter named peerCode, or a property named PeerCode.
    //
    // Named rather than typed, deliberately. A sweep that looked for "parameters of type PeerCode"
    // could only ever find the doors already closed -- the open ones are exactly the ones typed
    // string, so the NAME is the only handle available. That is also why the case-insensitive
    // comparison matters here: SessionCoordinator declares no PeerCode property and threads the
    // value through as the camelCase parameter, which is precisely how a case-sensitive grep missed
    // it when this chunk was scoped.
    private static IEnumerable<string> RawStringDoors()
    {
        const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var type in typeof(PeerCode).Assembly.GetTypes().Where(t => !IsCompilerGenerated(t)))
        {
            foreach (var property in type.GetProperties(Everything))
            {
                if (Names(property.Name) && property.PropertyType == typeof(string))
                {
                    yield return $"{type.Name}.{property.Name}";
                }
            }

            foreach (var method in type.GetMethods(Everything).Cast<MethodBase>().Concat(type.GetConstructors(Everything)))
            {
                if (IsCompilerGenerated(method))
                {
                    continue;
                }

                if (method.GetParameters().Any(p => Names(p.Name) && p.ParameterType == typeof(string)))
                {
                    yield return $"{type.Name}.{DisplayNameOf(method)}";
                }
            }
        }
    }

    private static bool Names(string? name) =>
        string.Equals(name, "peerCode", StringComparison.OrdinalIgnoreCase);

    private static string DisplayNameOf(MethodBase method) =>
        method is ConstructorInfo ? method.DeclaringType!.Name : method.Name;

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.Name.Contains('<', StringComparison.Ordinal)
        || member.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false).Length != 0;
}
