using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Campaigns;

/// <summary>Holds the campaign chosen for hosting and the one in use, creating one if none was chosen or found.</summary>
public sealed class HostingCampaign
{
    private readonly CampaignStore _store;

    public HostingCampaign(CampaignStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    public Guid? Chosen { get; set; }

    public Campaign? Current { get; private set; }

    public IReadOnlyList<Campaign> Resumable => _store.Campaigns;

    public Campaign StartFor()
    {
        Current = (Chosen is { } chosen ? _store.Find(chosen) : null) ?? _store.Create(null);
        return Current;
    }

    public void Ended() => Current = null;

    public bool LetsReturningPlayersIn => Current?.LetReturningPlayersIn == true;

    public void SetReturningPlayers(bool letIn)
    {
        if (Current is not { } campaign || campaign.LetReturningPlayersIn == letIn)
        {
            return;
        }

        campaign.LetReturningPlayersIn = letIn;
        _store.Save(campaign);
    }
}
