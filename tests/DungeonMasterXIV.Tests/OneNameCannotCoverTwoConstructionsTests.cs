using System;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A factory NAME must not cover overloads that construct independently (DMXENG-39, A-1.12a).
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap.</b> <c>EveryMessageAClientSendsIsSentTests</c> keys its table on the factory name and
/// says so — <i>"overloads collapse to one row on purpose"</i>. That is right while the overloads
/// are one protocol action: a wrapper delegating to a sibling cannot produce anything the sibling
/// would not, so a trigger exercising either exercises the construction. <b>It stops being right the
/// moment two overloads under one name build their own envelope</b> — then the row goes green
/// because the trigger reached one of them, and the other is unreachable behind a satisfied row.
/// That is the relink defect one level down: there a message TYPE covered two actions, here a
/// factory NAME does.
/// </para>
/// <para>
/// <b>THE GAP HAS ZERO INSTANCES TODAY, AND THAT IS WHY THIS FILE IS SHAPED THE WAY IT IS.</b>
/// <c>ForJoinRequest</c> had a third overload taking an <c>AdmissionDeadline</c> that built its own
/// envelope and had no caller — the motivating instance. <b>DMXENG-41 deleted it.</b> So a guard
/// wired only to the real file would be green with nothing to find, which reads identically to a
/// guard that cannot find anything. <b>Do not build a guard whose only evidence is that the suite
/// stays green.</b>
/// </para>
/// <para>
/// <b>So the demonstration is a mutation, and it is asserted rather than described.</b>
/// <see cref="TwoConstructingOverloadsUnderOneNameAreCaught"/> puts the deleted overload's shape
/// back — as text — and requires the detector to name it. That control runs every build, not once
/// on the afternoon somebody wrote it.
/// </para>
/// <para>
/// <b>And a synthetic control alone would not have been enough.</b> It proves the DETECTOR works and
/// says nothing about whether the real guard is pointed at the real file — the "mechanism exists
/// versus mechanism is used" split, which has cost this project three defects. So the overload was
/// also added to <c>WireEnvelope.cs</c> for real, the suite run, and the failure confirmed to name
/// <c>ForJoinRequest</c>; it is recorded in the PR because the code cannot carry it.
/// </para>
/// </remarks>
public sealed class OneNameCannotCoverTwoConstructionsTests
{
    // THE GUARD. Fails if any factory name covers two overloads that each build their own envelope.
    //
    // The message names the factory, because "a name covers two constructions" is not actionable
    // without knowing which -- and the fix is a decision (split the name, or make one delegate)
    // rather than a mechanical edit.
    [Fact]
    public void NoFactoryNameCoversTwoIndependentConstructions()
    {
        var offenders = FactoryOverloads.NamesCoveringTwoConstructions(WireEnvelopeSource());

        Assert.True(
            offenders.Count == 0,
            $"{offenders.Count} factory name(s) cover overloads that each construct their own "
            + $"envelope: {string.Join(", ", offenders)}. EveryMessageAClientSendsIsSentTests keys "
            + "its table by NAME, so one row would vouch for both and a trigger reaching either "
            + "would leave the other unreachable behind a green row. Either give them distinct "
            + "names, or make one delegate to the other.");
    }

    // THE MUTATION, AS A PERMANENT CONTROL RATHER THAN A ONE-OFF. This is the shape DMXENG-41
    // deleted: a third ForJoinRequest that builds its own envelope instead of delegating.
    //
    // Fails if the detector stops finding it -- which is the only way to tell the guard above apart
    // from a guard that returns an empty list whatever it is given.
    [Fact]
    public void TwoConstructingOverloadsUnderOneNameAreCaught()
    {
        const string Mutated = """
            public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey) =>
                ForJoinRequest(code, publicKey, DisplayName.None);

            public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey, DisplayName name)
            {
                return new WireEnvelope(WireMessageType.JoinRequest, code.Value) { PublicKey = publicKey };
            }

            public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey, AdmissionDeadline deadline)
            {
                return new WireEnvelope(WireMessageType.JoinRequest, code.Value)
                {
                    PublicKey = publicKey,
                    DeadlineUtcTicks = deadline.UtcTicks,
                };
            }
            """;

        Assert.Equal(["ForJoinRequest"], FactoryOverloads.NamesCoveringTwoConstructions(Mutated));
    }

    // THE NEGATIVE HALF, and without it the guard would pass on a codebase where every overload
    // constructs. Two overloads under one name are FINE when one delegates -- that is exactly
    // ForJoinRequest today, and the guard must not ask anyone to split it.
    [Fact]
    public void AWrapperThatDelegatesToItsSiblingIsNotAnOffence()
    {
        const string Delegating = """
            public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey) =>
                ForJoinRequest(code, publicKey, DisplayName.None);

            public static WireEnvelope ForJoinRequest(SessionCode code, byte[] publicKey, DisplayName name)
            {
                return new WireEnvelope(WireMessageType.JoinRequest, code.Value) { PublicKey = publicKey };
            }
            """;

        Assert.Empty(FactoryOverloads.NamesCoveringTwoConstructions(Delegating));
    }

    // THE READER IS POINTED AT SOMETHING, WHICH THE THREE TESTS ABOVE CANNOT TELL YOU.
    //
    // NoFactoryNameCoversTwoIndependentConstructions would pass just as well against an empty
    // string, a missing file read as "", or a path that stopped resolving after a project move --
    // every one of those yields zero offenders. This pins that the reader finds the real factories,
    // by name and by count, so an empty read fails loudly instead of reading as compliance.
    [Fact]
    public void TheReaderSeesTheFileItClaimsTo()
    {
        var found = FactoryOverloads.Factories(WireEnvelopeSource());

        Assert.Contains(found, factory => factory.Name == nameof(WireEnvelope.ForJoinRequest));
        Assert.Contains(found, factory => factory.Name == nameof(WireEnvelope.ForRelinkRequest));

        // Exactly one ForJoinRequest constructs; the other delegates. If this ever reads 0 the
        // detector has stopped recognising construction and the guard above is vacuous.
        Assert.Equal(
            1,
            found.Count(factory => factory.Name == nameof(WireEnvelope.ForJoinRequest) && factory.Constructs));
    }

    // The reflection-derived list and the source-derived list must describe the same file. They are
    // taken by completely different means -- metadata versus text -- so a divergence means one of
    // them has stopped seeing the type, and the source reader is the one that can do so silently.
    [Fact]
    public void EveryPublicFactoryReflectionFindsIsAlsoFoundInTheSource()
    {
        var inSource = FactoryOverloads.Factories(WireEnvelopeSource())
            .Select(factory => factory.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var reflected = typeof(WireEnvelope)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(WireEnvelope))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var missed = reflected.Except(inSource, StringComparer.Ordinal).ToList();

        Assert.True(
            missed.Count == 0,
            $"Reflection finds {missed.Count} public factory/factories the source reader did not: "
            + $"{string.Join(", ", missed)}. The reader's pattern has drifted from how the file is "
            + "written, so the guard covers less than it claims.");
    }

    private static string WireEnvelopeSource() =>
        File.ReadAllText(Path.Combine(
            ShippedCopyCorpus.RepositoryRoot(),
            "src",
            "DungeonMasterXIV.Core",
            "Net",
            "WireEnvelope.cs"));
}
