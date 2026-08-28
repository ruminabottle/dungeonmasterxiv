using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.5c: the admit button records a participant, not just admits one.
/// </summary>
/// <remarks>
/// <para>
/// <b>A SOURCE SCAN, because no test project references the plugin</b> — window behaviour can be
/// read and never executed. Its limit is stated rather than implied: this asserts the CALL IS
/// PRESENT, not that it runs. <c>HostingCampaignTests</c> covers what the call does.
/// </para>
/// <para>
/// <b>Why this guard exists at all, and it is the specific defect being fixed.</b>
/// <c>CampaignStore.AddParticipant</c> sat with ZERO production callers while three tickets circled
/// it. A minting method nothing calls is exactly the state this PR was held for — a picker offering
/// continuity the build could not deliver. Without a guard here, the same thing could be reached
/// again by deleting one line in a file no unit test can execute.
/// </para>
/// </remarks>
public class AdmittingRecordsAParticipantTests
{
    private static string AdmissionPromptSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Windows", "AdmissionPromptView.cs");
            if (File.Exists(candidate))
            {
                return string.Join(
                    "\n",
                    File.ReadAllLines(candidate)
                        .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            }
        }

        throw new InvalidOperationException("No Windows/AdmissionPromptView.cs above the test binary.");
    }

    // The vacuity control: if the reader returned nothing, every Contains below would fail loudly,
    // but nothing would prove the right file was read. This names something only this file holds.
    [Fact]
    public void TheReaderIsReadingTheAdmissionPrompt()
    {
        var source = AdmissionPromptSource();

        Assert.NotEmpty(source);
        Assert.Contains("_coordinator.Admit(request.PeerCode)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AdmittingAlsoRecordsTheParticipant()
    {
        Assert.Contains("_hosting.Record(", AdmissionPromptSource(), StringComparison.Ordinal);
    }

    // The relink arm is passed through rather than dropped. Currently unreachable — nothing tells a
    // joiner its id — so a caller could pass nothing and no test of BEHAVIOUR would notice. This is
    // the only thing standing between here and a returning player silently acquiring a second
    // participant the moment relink starts working.
    [Fact]
    public void TheRelinkFlagIsPassedRatherThanDefaulted()
    {
        Assert.Contains("request.IsRelink", AdmissionPromptSource(), StringComparison.Ordinal);
    }
}
