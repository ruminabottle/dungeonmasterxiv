using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-107: <c>SessionCoordinator</c> takes its <see cref="SessionCapabilities"/> as a REQUIRED
/// argument — not optional, no default.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because no behavioural test can observe it, and that is a structural limit rather
/// than an oversight in the suite.</b> The guarantee DMXENG-13 bought has two halves: the
/// <c>ThrowIfNull</c> is a RUNTIME property, which a test can trigger, and requiredness is a
/// COMPILE-TIME property of the signature, which nothing running can see. Every test passes the
/// record explicitly, so every test stays green whether the parameter is required or optional.
/// qa-3 measured it: adding <c>= null</c> left <b>1023 passed, 0 failed</b>. A green suite was not
/// evidence, because the suite could not be red.
/// </para>
/// <para>
/// <b>What the loss would be, at its honest size.</b> No impact today — every caller supplies the
/// record and the code is correct. Making the parameter optional would DOWNGRADE a compile error to
/// a construction-time throw: the omission stops being impossible and becomes merely detected, which
/// is the exact move <see cref="SessionCapabilities"/>'s own doc forbids in its own words — <i>"a
/// default for the RECORD, never for the PARAMETER"</i>.
/// </para>
/// <para>
/// <b>ONE assertion, and the vacuity guard it would normally carry is deliberately absent because it
/// CANNOT FAIL.</b> A test like this usually needs a companion asserting the parameter exists at all,
/// so a rename or removal cannot make the real assertion pass against nothing. Here that companion
/// would be unfalsifiable: every caller in the suite passes <c>capabilities:</c> as a NAMED argument,
/// so removing the parameter, renaming it, or changing its type all fail to COMPILE — measured, all
/// three — long before any assertion runs. <b>The compiler is the existence check.</b> Shipping a
/// green test that no mutation can redden, inside the fix for a green test that no mutation could
/// redden, would have been the same defect one layer out.
/// </para>
/// <para>
/// The parameter is still located by TYPE rather than by name, so this says what it means: the
/// argument carrying capabilities is required, whatever it ends up being called.
/// </para>
/// </remarks>
public class TheCapabilitiesArgumentIsRequiredTests
{
    // Fails if: `= null` is added, which is the mutation qa-3 ran and the whole suite ignored.
    // HasDefaultValue and IsOptional are asserted separately because they are separate facts -- a
    // parameter can carry [Optional] without a default -- and either one alone would reopen the hole.
    [Fact]
    public void TheCapabilitiesArgumentIsNotOptionalAndCarriesNoDefault()
    {
        var capabilities = typeof(SessionCoordinator)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single()
            .GetParameters()
            .Single(parameter => parameter.ParameterType == typeof(SessionCapabilities));

        Assert.False(
            capabilities.HasDefaultValue,
            "The capabilities parameter has a default value, so a caller can omit it. That turns a "
            + "compile error into a construction-time throw and moves DMXENG-13's guarantee back to "
            + "where it was. The clause under test is HasDefaultValue.");

        Assert.False(
            capabilities.IsOptional,
            "The capabilities parameter is optional, so a caller can omit it. The clause under test "
            + "is IsOptional, which is a separate fact from HasDefaultValue.");
    }
}
