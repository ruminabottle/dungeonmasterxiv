using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// Controls for <c>NoTlsValidationBypass</c>, the build guard in <c>Directory.Build.targets</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A negative control and a positive control are different instruments, and C15 shipped only
/// one.</b> Its control injected a bypass and watched the guard fire, which proves the guard fires
/// when it should and says nothing about whether it stays silent when it should. It did not: the
/// token <c>CheckCertificateRevocation</c> is a contiguous substring of
/// <c>CheckCertificateRevocationList</c>, the property set <c>true</c> to make revocation checking
/// <i>stricter</i>, so code that hardened TLS failed the build with a message asserting its author
/// had disabled validation. Nothing in the PR could have caught that, because nothing asked the
/// question.
/// </para>
/// <para>
/// <b>Through the same instrument, which is the whole of precondition 23.</b> These build a throwaway
/// project that imports the repository's real <c>Directory.Build.targets</c> and invoke
/// <c>Build</c> — so the guard runs from its own file, through its own <c>BeforeTargets</c> wiring,
/// over its own token list. A test that re-implemented the matching rule in C# would pass while the
/// build disagreed with it, which is the drift this repository keeps paying for.
/// </para>
/// <para>
/// <b>The tokens are read out of the guard, never listed here.</b> Precondition 25: derive the check
/// from the artefact rather than enumerating a world that grows. A token added tomorrow is probed
/// tomorrow, and this file contains no bypass name of its own — which is also why it does not trip
/// the fence it is testing.
/// </para>
/// <para>
/// The fixture lives outside the repository tree, because a probe file under the root would be
/// swept into the plugin project's own <c>**/*.cs</c> glob.
/// </para>
/// </remarks>
public sealed class TlsBypassFenceTests
{
    /// <summary>The guard's own file, found by walking up from the test binary.</summary>
    private static readonly string Guard = LocateGuard();

    /// <summary>Every name the guard matches on, read from the guard itself.</summary>
    public static TheoryData<string> DeclaredTokens()
    {
        var data = new TheoryData<string>();
        foreach (var token in TokensDeclaredByTheGuard())
        {
            data.Add(token);
        }

        return data;
    }

    /// <summary>
    /// The negative control, once per token the guard declares.
    /// </summary>
    /// <remarks>
    /// A token that matches nothing is a check that cannot fail, and it would look exactly like a
    /// clean repository. Asserting the guard's own text rather than merely a non-zero exit is
    /// precondition 24: a control that fails for the wrong reason retires the question.
    /// </remarks>
    [Theory]
    [MemberData(nameof(DeclaredTokens))]
    public void EveryTokenTheGuardDeclaresIsOneItActuallyFiresOn(string token)
    {
        var output = ScanProbe("Probe", $"class Probe {{ object? Field = \"{token}\"; }}");

        Assert.Contains("disables TLS certificate validation", output, StringComparison.Ordinal);
        Assert.Contains(token, output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The positive control.</b> Code that makes TLS stricter must not fail the build.
    /// </summary>
    /// <remarks>
    /// This is the case that was broken, and the literal below is the specimen: setting
    /// <c>CheckCertificateRevocationList</c> to <c>true</c> turns revocation checking on. An
    /// instrument that produces false FAILs is worse than one that cannot fail — the first person
    /// to meet it concludes the guard is noise, and a noisy guard gets relaxed rather than fixed.
    /// </remarks>
    [Fact]
    public void TheGuardIsSilentOnCodeThatHardensTls()
    {
        var output = ScanProbe(
            "Probe",
            "class Probe { void M() { var h = new System.Net.Http.HttpClientHandler(); "
            + "h.CheckCertificateRevocationList = true; } }");

        Assert.DoesNotContain("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A comment may name a bypass, so that the rule can be documented where it matters — in any of
    /// the forms C# actually offers for writing one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A rule that forbids explaining itself gets explained wrongly or not at all, and the natural
    /// place to warn the next person is an XML doc on the transport type in Core — a project the
    /// guard scans.
    /// </para>
    /// <para>
    /// <b>BUG-18: this was a Fact covering <c>//</c> alone, and the guard permitted only that.</b>
    /// A <c>/* */</c> block comment naming a token failed the build, so the fence permitted one of
    /// the two standard ways of explaining itself and forbade the other. The row list is the fix's
    /// shape: each form a reader might reasonably reach for is now its own case.
    /// </para>
    /// <para>
    /// <b>The last row is the one that mattered.</b> Its interior line is plain indented prose, so
    /// no line-shape rule can tell it from code — it is BUG-18's own reproduction, and it survived
    /// the first fix while every row above it passed. It is silent now because the guard strips
    /// comments from the file's whole text rather than judging lines one at a time.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("// never assign {0} in a shipped project")]
    [InlineData("/// <summary>Never assign {0} here.</summary>")]
    [InlineData("/* Never assign {0} in a shipped project. */")]
    [InlineData("/*\n * Never assign {0} in a shipped project.\n */")]
    [InlineData("/*\n    Never assign {0} in a shipped project.\n*/")]
    public void TheGuardIsSilentOnACommentSoTheRuleCanBeDocumented(string commentFormat)
    {
        var token = TokensDeclaredByTheGuard()[0];
        var comment = string.Format(commentFormat, token);
        var output = ScanProbe("Probe", $"{comment}\nclass Probe {{ }}");

        Assert.DoesNotContain("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A comment containing a semicolon is still a comment.
    /// </summary>
    /// <remarks>
    /// BUG-19. The guard recorded, as one of its known limitations, that MSBuild splits items on
    /// <c>;</c> so a semicolon-bearing comment is scanned as two fragments and the tail can still
    /// fire. It cannot: <c>ReadLinesFromFile</c> escapes the semicolons in what it emits, so the
    /// line arrives as one item. The general MSBuild fact is real for <c>Include</c> attributes and
    /// does not reach this. The note is deleted; this is what keeps the behaviour it described true.
    /// </remarks>
    [Fact]
    public void TheGuardIsSilentOnACommentContainingASemicolon()
    {
        var token = TokensDeclaredByTheGuard()[0];
        var output = ScanProbe("Probe", $"// never do this; {token} = x\nclass Probe {{ }}");

        Assert.DoesNotContain("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The control for the block-comment skip.</b> A line that begins with an asterisk because it
    /// is real code, not because it continues a comment, must still fire.
    /// </summary>
    /// <remarks>
    /// qa-2's caution on BUG-18, and it is the reason the skip matches <c>"* "</c> rather than
    /// <c>"*"</c>: an asterisk is both how a continued block-comment line looks and how a pointer
    /// dereference starts. Skipping every line that opens with one would be easy to get subtly wrong
    /// and impossible to notice, because the hole it opens is silence — which is what a clean
    /// repository looks like too.
    /// </remarks>
    [Fact]
    public void TheGuardStillFiresOnARealAssignmentBeginningWithAnAsterisk()
    {
        var token = TokensDeclaredByTheGuard()[0];
        var output = ScanProbe(
            "Probe",
            $"unsafe class Probe {{ void M(object** p) {{\n        *p = {token};\n    }} }}");

        Assert.Contains("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The skip ends where the comment ends: a real assignment after <c>*/</c> still fires.
    /// </summary>
    [Fact]
    public void TheGuardStillFiresAfterABlockCommentCloses()
    {
        var token = TokensDeclaredByTheGuard()[0];
        var output = ScanProbe(
            "Probe",
            $"class Probe {{ void M(object h) {{\n        /*\n         * documented\n         */\n        h.{token} = null;\n    }} }}");

        Assert.Contains("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The allowlisted project is exempt, and the exemption is read from the guard rather than named.
    /// </summary>
    /// <remarks>
    /// The other half of the same instrument: without this, an allowlist that matched nothing would
    /// be indistinguishable from one that worked, right up until the smoke test's own project failed
    /// to build.
    /// </remarks>
    [Fact]
    public void TheAllowlistedProjectIsNotScanned()
    {
        var permitted = Read(@"<TlsValidationBypassPermittedIn>([^<]+)</TlsValidationBypassPermittedIn>");
        var token = TokensDeclaredByTheGuard()[0];

        var output = ScanProbe(permitted, $"class Probe {{ object? Field = \"{token}\"; }}");

        Assert.DoesNotContain("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Code between two block comments is still code.
    /// </summary>
    /// <remarks>
    /// The stripper's <c>[\s\S]*?</c> is non-greedy, and this is what says so. A greedy match would
    /// run from the first <c>/*</c> to the LAST <c>*/</c>, swallow everything between them, and the
    /// fence would go silent over exactly the code it exists to watch.
    /// </remarks>
    [Fact]
    public void TheGuardFiresOnCodeBetweenTwoBlockComments()
    {
        var token = TokensDeclaredByTheGuard()[0];
        var output = ScanProbe(
            "Probe",
            $"/* first, mentioning {token} */\nclass Probe {{ void M(object h) {{ h.{token} = null; }} }}\n/* second, mentioning {token} */");

        Assert.Contains("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A comment marker inside a string literal must not open a comment.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the hazard the per-file rewrite introduced and the reason the stripper protects
    /// literals instead of removing comments naively. An unbalanced <c>/*</c> in a string opens a
    /// block that runs to the next <c>*/</c> anywhere in the file, taking real code with it — and
    /// the fence's failure mode there is SILENCE, which is indistinguishable from a clean file.
    /// </para>
    /// <para>
    /// Not hypothetical: this repository has <c>"wss://…"</c> in several places and a
    /// <c>**/*.cs</c> in an XML doc. Measured against a naive strip of <c>/*…*/</c> alone, these
    /// rows blind the guard; they are here so a future simplification of that pattern cannot.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("const string G = \"**/*.cs\";", "const string C = \"a */ b\";")]
    [InlineData("const string G = @\"pattern  /*  \";", "const string C = @\" */ \";")]
    public void TheGuardIsNotBlindedByACommentMarkerInsideAStringLiteral(string before, string after)
    {
        var token = TokensDeclaredByTheGuard()[0];
        var output = ScanProbe(
            "Probe",
            $"class Probe {{\n    {before}\n    void M(object h) {{ h.{token} = null; }}\n    {after}\n}}");

        Assert.Contains("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A token in a string literal fires, and that is a decision rather than an oversight.
    /// </summary>
    /// <remarks>
    /// Once comments are stripped, a literal is the remaining place a bypass name can sit
    /// innocently. It fires anyway: a name in a string is either reflection reaching for the
    /// property or documentation that should have been a comment, and for a guard whose failure
    /// mode is silence the conservative reading is the safe one. Documenting the rule has four
    /// working forms, all tested above; none of them is a string.
    /// </remarks>
    [Fact]
    public void TheGuardFiresOnATokenInAStringLiteral()
    {
        var token = TokensDeclaredByTheGuard()[0];
        var output = ScanProbe("Probe", $"class Probe {{ const string S = \"{token}\"; }}");

        Assert.Contains("disables TLS certificate validation", output, StringComparison.Ordinal);
    }

    /// <summary>The names inside the guard's <c>Contains('…')</c> conditions, in declaration order.</summary>
    private static IReadOnlyList<string> TokensDeclaredByTheGuard()
    {
        var tokens = Regex.Matches(File.ReadAllText(Guard), @"\.Contains\('([^']+)'\)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(tokens);
        return tokens;
    }

    private static string Read(string pattern)
    {
        var match = Regex.Match(File.ReadAllText(Guard), pattern);
        Assert.True(match.Success, $"{Guard} no longer contains a match for {pattern}.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Runs the guard over <paramref name="source"/> as though it were project
    /// <paramref name="projectName"/>, and returns everything MSBuild said.
    /// </summary>
    /// <remarks>
    /// The fixture declares an empty <c>Build</c> target and the run asks for <c>Build</c>, so the
    /// guard is reached through its real <c>BeforeTargets</c> hook rather than invoked directly.
    /// There is no SDK and nothing to restore or compile: the only thing that can fail here is the
    /// guard, which is what makes the failure attributable to it.
    /// </remarks>
    private static string ScanProbe(string projectName, string source)
    {
        var directory = Directory.CreateTempSubdirectory("dmx-fence-");

        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Probe.cs"), source);
            File.WriteAllText(
                Path.Combine(directory.FullName, projectName + ".proj"),
                $"""
                 <Project>
                   <ItemGroup><Compile Include="Probe.cs" /></ItemGroup>
                   <Target Name="Build" />
                   <Import Project="{Guard}" />
                 </Project>
                 """);

            using var msbuild = Process.Start(new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "msbuild", projectName + ".proj", "-target:Build", "-nologo" },
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new InvalidOperationException("Could not start dotnet msbuild.");

            var output = msbuild.StandardOutput.ReadToEnd() + msbuild.StandardError.ReadToEnd();
            msbuild.WaitForExit();
            return output;
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Walks up from the test binary to the repository root.</summary>
    private static string LocateGuard()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Directory.Build.targets");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"No Directory.Build.targets above {AppContext.BaseDirectory}; the guard this tests is missing.");
    }
}
