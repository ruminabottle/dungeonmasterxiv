using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A violation of <c>SessionCoordinator</c>'s construction order is DETECTED (DMXENG-45).
/// </summary>
/// <remarks>
/// <para>
/// <b>The hazard was silence, not breakage.</b> Three of <c>JoinRequester</c>'s arguments arrive
/// from fields assigned earlier in <c>SessionCoordinator</c>'s constructor, so building it too early
/// passed a <c>null</c> — and nothing refused it. The assignment succeeded, the object looked valid,
/// and the failure surfaced later on a join path or never in a test that does not join. Every test
/// uses a fully-constructed coordinator, so nothing exercised construction order at all.
/// </para>
/// <para>
/// <b>The detector is a guard rather than a test that reads the constructor, and that is the
/// choice.</b> A source-reading test asserts the SHAPE of code and goes stale when the file is
/// reformatted; a guard makes the violation loud AT CONSTRUCTION, so any coordinator built in the
/// wrong order throws and every test that builds one fails at once. The property holds by
/// construction rather than because somebody remembered to look.
/// </para>
/// <para>
/// <b>Uninitialised instances stand in for the collaborators on purpose.</b> The guard's contract is
/// "not null" — it needs a reference of the right type, not a working object. Building real ones
/// would drag a transport and a relay link into a test about null-checking, and would rot the moment
/// those constructors changed. <see cref="RuntimeHelpers.GetUninitializedObject"/> gives identity
/// without behaviour, which is exactly and only what is under test.
/// </para>
/// </remarks>
public class TheConstructionOrderIsDetectedTests
{
    private static ConstructorInfo TheConstructor =>
        typeof(JoinRequester).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();

    // DERIVED over the constructor's parameters rather than listing them. An argument added later is
    // covered without anyone editing this test -- and if one is added WITHOUT a guard, this fails.
    public static TheoryData<int> EveryParameterPosition()
    {
        var data = new TheoryData<int>();

        for (var position = 0; position < TheConstructor.GetParameters().Length; position++)
        {
            data.Add(position);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryParameterPosition))]
    public void EveryCollaboratorIsRefusedWhenNull(int position)
    {
        var parameters = TheConstructor.GetParameters();
        var arguments = parameters
            .Select(parameter => (object?)Placeholder(parameter.ParameterType))
            .ToArray();

        arguments[position] = null;

        var thrown = Assert.Throws<TargetInvocationException>(() => TheConstructor.Invoke(arguments));
        var refusal = Assert.IsType<ArgumentNullException>(thrown.InnerException);

        Assert.Equal(parameters[position].Name, refusal.ParamName);
    }

    // THE POSITIVE CONTROL. Without it every case above is satisfied by a constructor that throws
    // unconditionally -- which would "detect" the violation and also refuse every correct build.
    [Fact]
    public void AFullySuppliedConstructionIsAccepted()
    {
        var arguments = TheConstructor.GetParameters()
            .Select(parameter => (object?)Placeholder(parameter.ParameterType))
            .ToArray();

        Assert.Null(Record.Exception(() => TheConstructor.Invoke(arguments)));
    }

    // And the guard set must COVER the constructor. A parameter added without a guard passes
    // EveryCollaboratorIsRefusedWhenNull only if someone also adds a case; deriving the cases from
    // the constructor is what stops that, and this asserts the derivation is not empty.
    [Fact]
    public void TheDerivationActuallyFoundParameters()
    {
        Assert.NotEmpty(TheConstructor.GetParameters());
        Assert.Equal(TheConstructor.GetParameters().Length, EveryParameterPosition().Cast<object>().Count());
    }

    /// <summary>A non-null reference of the right type, with no constructor run.</summary>
    private static object Placeholder(Type type) =>
        type == typeof(Action) ? (Action)(() => { })
        : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<>)
            ? BuildFunc(type)
            : RuntimeHelpers.GetUninitializedObject(type);

    private static object BuildFunc(Type funcType)
    {
        var returns = funcType.GetGenericArguments()[0];
        var method = typeof(TheConstructionOrderIsDetectedTests)
            .GetMethod(nameof(NullOf), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(returns);

        return Delegate.CreateDelegate(funcType, method);
    }

    private static T NullOf<T>() => default!;
}
