namespace DungeonMasterXIV.Relay.Transport;

/// <summary>A connection the relay can send bytes to and close, identified by id.</summary>
public interface IRelayConnection
{
    string Id { get; }

    ValueTask SendAsync(byte[] bytes, CancellationToken cancellationToken);

    ValueTask CloseAsync(CancellationToken cancellationToken);

}
