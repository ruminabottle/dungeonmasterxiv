using DungeonMasterXIV.Relay;

var app = RelayApp.Build(RelayOptions.FromEnvironment());
await app.RunAsync().ConfigureAwait(false);
