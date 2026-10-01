namespace DungeonMasterXIV.Relay.Transport;

public interface IRelayConnection
{
    string Id { get; }

    ValueTask SendAsync(byte[] bytes, CancellationToken cancellationToken);

    ValueTask CloseAsync(CancellationToken cancellationToken);

}
