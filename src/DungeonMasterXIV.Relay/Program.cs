using DungeonMasterXIV.Relay;

// Wiring only, in the spirit of the standards' rule for Plugin.cs: read the options, build the
// relay, run it. Every decision the relay makes lives in a service RelayApp constructs.
var app = RelayApp.Build(RelayOptions.FromEnvironment());
await app.RunAsync().ConfigureAwait(false);
