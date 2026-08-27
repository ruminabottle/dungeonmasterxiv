using System;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>
/// The relay's path comes from <see cref="RelayEndpoint.SessionPath"/> on every construction path.
/// </summary>
/// <remarks>
/// <para>
/// This replaces <c>RelayPathMatchesTheClientDefaultTests</c>, which was deleted rather than kept.
/// That guard compared two independently written literals; once both derive from one constant they
/// cannot disagree, so the assertion could no longer come out negative and would have been a check
/// that cannot fail — added by a fix, which is the most respectable way into that catalogue.
/// </para>
/// <para>
/// <b>What these guard instead, and it is not the same thing.</b> The old guard asserted against
/// <c>new RelayOptions()</c>. The container does not use that constructor — <c>Program.cs</c> calls
/// <see cref="RelayOptions.FromEnvironment"/>, which had its <i>own</i> literal fallback of
/// <c>"/relay"</c> that the guard never looked at. So the deployed relay would have served
/// <c>/relay</c> while every client dialled <c>/session</c>: the exact failure the earlier fix
/// described, still live on the path production actually takes.
/// </para>
/// <para>
/// The describable failing input for both tests below is therefore a real edit somebody has already
/// made once: replacing a derivation with a literal.
/// </para>
/// </remarks>
public sealed class RelayPathComesFromTheSharedConstantTests
{
    private const string PathVariable = "DMX_RELAY_PATH_PREFIX";

    // Fails if: the environment fallback is written as a literal again. That is not hypothetical —
    // it was "/relay" until this chunk, on the one construction path the container uses.
    [Fact]
    public void TheEnvironmentFallbackIsTheClientsPath()
    {
        using var _ = new EnvironmentVariable(PathVariable, null);

        Assert.Equal(RelayEndpoint.SessionPath, RelayOptions.FromEnvironment().Path);
    }

    // Fails if: the property default is written as a literal again.
    [Fact]
    public void ThePropertyDefaultIsTheClientsPath()
    {
        Assert.Equal(RelayEndpoint.SessionPath, new RelayOptions().Path);
    }

    // The other half, so the pair cannot be satisfied by hard-coding the path and ignoring the
    // variable. Fails if: configurability is lost — an operator behind a reverse proxy needs it,
    // and R-1.8's swappable relay would be a weaker promise without it.
    [Fact]
    public void AnOperatorCanStillChooseADifferentPath()
    {
        using var _ = new EnvironmentVariable(PathVariable, "/somewhere-else");

        Assert.Equal("/somewhere-else", RelayOptions.FromEnvironment().Path);
    }

    // Sets a variable for the duration of one test and restores whatever was there. Environment is
    // process-wide, so leaving one set would silently change another test's meaning.
    private sealed class EnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _original;

        public EnvironmentVariable(string name, string? value)
        {
            _name = name;
            _original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
    }
}
