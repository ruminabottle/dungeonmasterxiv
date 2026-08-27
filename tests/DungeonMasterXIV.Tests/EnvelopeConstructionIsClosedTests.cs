using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The envelope cannot be assembled from outside its own factories.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists.</b> <c>WireEnvelope.FromWire</c> used to take seven parameters, and the
/// compiler enforced that anyone rebuilding an envelope had to supply exactly those. Collapsing it
/// to take a <c>WireShape</c> was necessary — a further optional field would have pushed it past
/// the standards' blocking limit — but it moved a guarantee from the compiler to a sentence in a
/// comment: <i>"only the codec can obtain a WireShape."</i>
/// </para>
/// <para>
/// A guarantee that used to be checked by the language and is now checked by a comment is a
/// downgrade however good the refactor. These tests put the check back somewhere that fails.
/// </para>
/// </remarks>
public class EnvelopeConstructionIsClosedTests
{
    private static readonly Assembly Core = typeof(WireEnvelope).Assembly;

    // Fails if WireShape is ever made public. That single keyword is the whole of the guarantee:
    // a public shape lets any caller populate an envelope field by field and hand it to FromWire,
    // going around factories that exist to refuse things -- ForSessionPayload takes a SealedPayload
    // specifically so no overload accepts plaintext.
    [Fact]
    public void TheWireShapeIsNotVisibleOutsideCore()
    {
        var shape = Core.GetType("DungeonMasterXIV.Net.WireShape", throwOnError: true)!;

        Assert.False(shape.IsPublic);
        Assert.True(shape.IsNotPublic);
        Assert.Null(typeof(WireEnvelope).Assembly.GetExportedTypes()
            .FirstOrDefault(exported => exported.Name == "WireShape"));
    }

    // Fails if a public constructor appears. Construction goes through the named factories, each of
    // which decides what a valid envelope of that kind looks like.
    [Fact]
    public void TheEnvelopeHasNoPublicConstructor()
    {
        Assert.Empty(typeof(WireEnvelope).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    // Fails if FromWire is made public -- which would expose the rebuild path even while WireShape
    // stayed internal, since a public method taking an internal type is still callable via reflection
    // and, more to the point, signals the path is open.
    [Fact]
    public void TheRebuildPathIsNotPublic()
    {
        var fromWire = typeof(WireEnvelope)
            .GetMethod("FromWire", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(fromWire);
        Assert.False(fromWire!.IsPublic);
    }

    // Fails if any envelope field gains a public setter. Every one is private init, so an envelope
    // cannot be edited after a factory built it.
    [Fact]
    public void NoEnvelopeFieldCanBeSetFromOutside()
    {
        var settable = typeof(WireEnvelope)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(settable);
    }
}
