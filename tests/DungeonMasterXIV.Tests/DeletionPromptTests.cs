using System;
using System.Collections.Generic;
using DungeonMasterXIV.Campaigns;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-9. Note what none of these assert: that a confirmation was displayed. A window that shows
/// "Delete permanently?" and deletes on the same click satisfies that perfectly, which is the shape
/// being fixed rather than the fix. The property is that the file is still there, so every test here
/// asserts which deletes were and were not performed.
/// </summary>
public class DeletionPromptTests
{
    private readonly List<Guid> _deletedCampaigns = new();
    private readonly List<string> _deletedFiles = new();

    private DeletionPrompt NewPrompt() => new(_deletedCampaigns.Add, _deletedFiles.Add);

    // Fails if: the first click deletes. This is the bug -- the unreadable row called
    // DeleteUnreadable straight from the button, so the file was gone before anyone confirmed.
    [Fact]
    public void AnUnreadableFileIsNotDeletedOnTheFirstClick()
    {
        NewPrompt().Request("broken.json");

        Assert.Empty(_deletedFiles);
    }

    [Fact]
    public void AnUnreadableFileIsDeletedOnceConfirmed()
    {
        var prompt = NewPrompt();

        prompt.Request("broken.json");
        prompt.Confirm();

        Assert.Equal(new[] { "broken.json" }, _deletedFiles);
    }

    // Fails if: Cancel only hides the prompt. The file has to survive, not just the question.
    [Fact]
    public void AnUnreadableFileIsNotDeletedAfterCancelling()
    {
        var prompt = NewPrompt();

        prompt.Request("broken.json");
        prompt.Cancel();
        prompt.Confirm();

        Assert.Empty(_deletedFiles);
    }

    // The readable row is the reference behaviour this fix copies, so it is pinned too -- widening
    // the state must not have quietly changed what the rows above it do.
    [Fact]
    public void ACampaignIsNotDeletedOnTheFirstClick()
    {
        NewPrompt().Request(Guid.NewGuid());

        Assert.Empty(_deletedCampaigns);
    }

    [Fact]
    public void ACampaignIsDeletedOnceConfirmed()
    {
        var id = Guid.NewGuid();
        var prompt = NewPrompt();

        prompt.Request(id);
        prompt.Confirm();

        Assert.Equal(new[] { id }, _deletedCampaigns);
    }

    [Fact]
    public void ACampaignIsNotDeletedAfterCancelling()
    {
        var prompt = NewPrompt();

        prompt.Request(Guid.NewGuid());
        prompt.Cancel();
        prompt.Confirm();

        Assert.Empty(_deletedCampaigns);
    }

    // Fails if: the two row kinds get separate confirmation state. With two flags, the campaign
    // would still be pending while the file was, and confirming would delete both -- one of them a
    // row the user had navigated away from.
    [Fact]
    public void RequestingAFileReplacesAPendingCampaignRatherThanQueueingBesideIt()
    {
        var id = Guid.NewGuid();
        var prompt = NewPrompt();

        prompt.Request(id);
        prompt.Request("broken.json");
        prompt.Confirm();

        Assert.Equal(new[] { "broken.json" }, _deletedFiles);
        Assert.Empty(_deletedCampaigns);
    }

    [Fact]
    public void RequestingACampaignReplacesAPendingFileRatherThanQueueingBesideIt()
    {
        var id = Guid.NewGuid();
        var prompt = NewPrompt();

        prompt.Request("broken.json");
        prompt.Request(id);
        prompt.Confirm();

        Assert.Equal(new[] { id }, _deletedCampaigns);
        Assert.Empty(_deletedFiles);
    }

    // Fails if: confirming does not clear the pending row. A row left primed would delete again on
    // the next frame, and for a campaign that is a second delete of an id that no longer exists.
    [Fact]
    public void ConfirmingTwiceDeletesOnce()
    {
        var prompt = NewPrompt();

        prompt.Request("broken.json");
        prompt.Confirm();
        prompt.Confirm();

        Assert.Equal(new[] { "broken.json" }, _deletedFiles);
    }

    [Fact]
    public void ConfirmingWithNothingPendingDeletesNothing()
    {
        NewPrompt().Confirm();

        Assert.Empty(_deletedFiles);
        Assert.Empty(_deletedCampaigns);
    }

    // Fails if: the prompt reports every row as awaiting, which would show the confirmation on all
    // of them at once and make Yes ambiguous.
    [Fact]
    public void OnlyTheRequestedRowIsAwaitingConfirmation()
    {
        var prompt = NewPrompt();

        prompt.Request("broken.json");

        Assert.True(prompt.IsAwaiting("broken.json"));
        Assert.False(prompt.IsAwaiting("other.json"));
        Assert.False(prompt.IsAwaiting(Guid.NewGuid()));
    }

    [Fact]
    public void NoRowIsAwaitingConfirmationBeforeAnythingIsRequested()
    {
        var prompt = NewPrompt();

        Assert.False(prompt.IsAwaiting("broken.json"));
        Assert.False(prompt.IsAwaiting(Guid.NewGuid()));
    }

    [Fact]
    public void CancellingClearsTheAwaitingRow()
    {
        var prompt = NewPrompt();

        prompt.Request("broken.json");
        prompt.Cancel();

        Assert.False(prompt.IsAwaiting("broken.json"));
    }
}
