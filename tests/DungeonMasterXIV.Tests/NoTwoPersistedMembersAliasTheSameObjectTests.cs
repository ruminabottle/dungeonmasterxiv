using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// No two persisted members of a stored type hand back the same object (BUG-146's whole class).
/// </summary>
/// <remarks>
/// <para>
/// <b>Built by qa-1 and left uncommitted as a candidate; adapted here.</b> The insight is theirs and
/// it is the reason this exists rather than another shape check: <c>RelinkMemory</c> exposed the same
/// <c>List</c> through two public gettable members, the serialiser wrote it twice, and Newtonsoft
/// appended on load — the memory doubled every save/load, unbounded.
/// </para>
/// <para>
/// <b>THE EXISTING SHAPE GUARD CAUGHT IT AND WAS ANSWERED WRONGLY.</b>
/// <c>NothingCanBeAddedToAStoredTypeWithoutThisTestSayingSo</c> fired when <c>All</c> was added,
/// exactly as designed, and the answer given was to add <c>All</c> to its expected list — so every
/// run afterwards <i>confirmed</i> the bug. A shape guard asks "did the persisted surface change"; it
/// cannot ask "should it have".
/// </para>
/// <para>
/// <b>The difference is that this has no expected list to corrupt.</b> Two persisted members handing
/// back the same object is always wrong, because the serialiser writes each member independently, so
/// the answer can never be "add it to the list". <b>A guard whose verdict depends on a maintained
/// expectation can be answered wrongly once and then defends that answer forever; one that derives
/// its verdict from the artefact cannot.</b>
/// </para>
/// <para>
/// <b>WHAT I CHANGED FROM THE CANDIDATE, AND WHY IT IS THE SAME ARGUMENT ONE LEVEL UP.</b> qa-1
/// listed the three stored types by hand. That list is itself a maintained expectation — a stored
/// type added later is simply not checked, and nothing says so. The types are now DERIVED by walking
/// outward from the settings root, so the guard escapes the maintained list for types as well as for
/// member names.
/// </para>
/// <para>
/// <b>Three limits, measured rather than assumed. Two are qa-1's and stated as they wrote them.</b>
/// </para>
/// <list type="number">
/// <item><b>Silent on a COPYING second view</b> (<c>=&gt; Remembered.ToList()</c>). qa-1 checked what
/// that variant actually does: it bloats the document but does NOT reproduce the defect, because a
/// get-only copy cannot be populated back on load. Aliasing is the mechanism, which is why this aims
/// at aliasing.</item>
/// <item><b>It sees only aliases present on a FRESHLY CONSTRUCTED instance.</b> Two members that
/// begin null and alias only once populated would slip past.</item>
/// <item><b>The root is <see cref="PluginSettings"/>, not <c>Configuration</c> — mine, and forced.</b>
/// <c>Configuration</c> is what Dalamud actually persists, and it carries the settings. It lives in
/// the plugin project, which this assembly references and may never reference. So a stored type
/// reachable ONLY from <c>Configuration</c> and not from the settings is outside this walk.</item>
/// </list>
/// </remarks>
public class NoTwoPersistedMembersAliasTheSameObjectTests
{
    /// <summary>The one class this guard is measured against reverting (BUG-146).</summary>
    private static readonly Type[] KnownStoredTypes =
        [typeof(PluginSettings), typeof(RelinkMemory), typeof(RememberedParticipant)];

    // THE PROPERTY. No expected list anywhere in it.
    [Fact]
    public void NoTwoPersistedMembersOfAStoredTypeAliasTheSameObject()
    {
        var offenders =
            from stored in StoredTypes()
            let instance = Activator.CreateInstance(stored)!
            from pair in Pairs(PersistedMembersOf(stored))
            let first = pair.First.GetValue(instance)
            where first is not null && ReferenceEquals(first, pair.Second.GetValue(instance))
            select $"{stored.Name}.{pair.First.Name} and {stored.Name}.{pair.Second.Name} are the same object";

        Assert.True(offenders.ToList() is [], string.Join("\n", offenders));
    }

    // >>> THE CONTROL ON THE INTAKE, WHICH THE CANDIDATE DID NOT NEED AND THIS ONE DOES. <<<
    //
    // Deriving the types removed a maintained list and bought a new failure mode with it: a walk
    // that finds NOTHING checks nothing and passes. That green is indistinguishable from a green
    // earned over every stored type, which is the exact shape this guard exists to refuse.
    //
    // So the derivation is asserted to reach the types BUG-146 actually lived in. This list is not
    // an expectation the verdict depends on -- the property above never reads it -- it is a floor
    // under the instrument.
    [Fact]
    public void TheWalkReachesTheTypesTheDefectLivedIn()
    {
        var reached = StoredTypes();

        Assert.All(KnownStoredTypes, known => Assert.Contains(known, reached));
    }

    // And the walk must not simply be returning everything it is handed: a type unreachable from the
    // settings root is out of scope, and a walk that included it would be measuring its own inputs.
    [Fact]
    public void TheWalkDoesNotReachATypeThatIsNotStored()
    {
        Assert.DoesNotContain(typeof(NoTwoPersistedMembersAliasTheSameObjectTests), StoredTypes());
    }

    /// <summary>
    /// Every type the persisted document can contain, reached from the settings root.
    /// </summary>
    /// <remarks>
    /// Restricted to types declared in the same assembly as <see cref="PluginSettings"/>, so the walk
    /// stops at the framework rather than descending into <c>string</c> and the BCL.
    /// </remarks>
    private static IReadOnlyList<Type> StoredTypes()
    {
        var found = new List<Type>();
        var pending = new Queue<Type>([typeof(PluginSettings)]);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();

            if (!found.Contains(current))
            {
                found.Add(current);

                foreach (var next in PersistedMembersOf(current).SelectMany(m => Reachable(m.PropertyType)))
                {
                    pending.Enqueue(next);
                }
            }
        }

        return found;
    }

    /// <summary>The types a member can carry into the document — itself, or its collection element.</summary>
    private static IEnumerable<Type> Reachable(Type member) =>
        (member.IsGenericType ? member.GetGenericArguments().Append(member) : [member])
        .Where(candidate => candidate.Assembly == typeof(PluginSettings).Assembly);

    /// <summary>
    /// What the serialiser writes for a type: public readable instance properties, no indexers.
    /// </summary>
    /// <remarks>
    /// Value types and <c>string</c> are excluded because neither can be ALIASED in the sense that
    /// matters — two members holding the same string are written twice and read back twice with no
    /// accumulation, which is the copying case limit 1 records as out of scope.
    /// </remarks>
    private static PropertyInfo[] PersistedMembersOf(Type stored) =>
        stored.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Where(property => property.CanRead)
            .Where(property => !property.PropertyType.IsValueType && property.PropertyType != typeof(string))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Each unordered pair once, so an offender is reported once rather than twice.</summary>
    private static IEnumerable<(PropertyInfo First, PropertyInfo Second)> Pairs(PropertyInfo[] members) =>
        from index in Enumerable.Range(0, members.Length)
        from other in Enumerable.Range(index + 1, Math.Max(0, members.Length - index - 1))
        select (members[index], members[other]);
}
