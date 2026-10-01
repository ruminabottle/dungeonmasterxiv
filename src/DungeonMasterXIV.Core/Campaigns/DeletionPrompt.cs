using System;

namespace DungeonMasterXIV.Campaigns;

public sealed class DeletionPrompt
{
    private readonly record struct Target(Guid? CampaignId, string? FileName);

    private readonly Action<Guid> _deleteCampaign;
    private readonly Action<string> _deleteFile;

    private Target? _pending;

    public DeletionPrompt(Action<Guid> deleteCampaign, Action<string> deleteFile)
    {
        ArgumentNullException.ThrowIfNull(deleteCampaign);
        ArgumentNullException.ThrowIfNull(deleteFile);

        _deleteCampaign = deleteCampaign;
        _deleteFile = deleteFile;
    }

    public bool IsAwaiting(Guid campaignId) => _pending?.CampaignId == campaignId;

    public bool IsAwaiting(string fileName) => _pending?.FileName == fileName;

    public void Request(Guid campaignId) => _pending = new Target(campaignId, null);

    public void Request(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _pending = new Target(null, fileName);
    }

    public void Cancel() => _pending = null;

    public void Confirm()
    {
        if (_pending is not { } target)
        {
            return;
        }

        _pending = null;

        if (target.CampaignId is { } campaignId)
        {
            _deleteCampaign(campaignId);
        }
        else if (target.FileName is { } fileName)
        {
            _deleteFile(fileName);
        }
    }
}
