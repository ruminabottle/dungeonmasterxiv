using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3e's alias half: the name defaults to the character name and may be changed to an alias.
/// </summary>
/// <remarks>
/// The rule lives on <see cref="PluginSettings"/> rather than at the plugin's composition root so
/// it can be tested without Dalamud, and so the settings window's "You will join as" preview and
/// the join itself call the same method. Two expressions meant to agree drift; one cannot disagree
/// with itself.
/// </remarks>
public class DisplayNameAliasTests
{
    private static readonly DisplayName CharacterName = DisplayName.OrNone("Rum Bottle");

    // The default the requirement names. Fails if: a fresh install sends nothing, which would show
    // every DM "a player who gave no name" until each user found the setting.
    [Fact]
    public void WithNoAliasThePlayerJoinsUnderTheirCharacterName()
    {
        var settings = new PluginSettings();

        Assert.Equal(CharacterName, settings.DisplayNameOr(CharacterName));
    }

    // The change half. Fails if: the alias is stored and then not used, which is the inert-setting
    // shape -- a control that changes nothing.
    [Fact]
    public void AUsableAliasIsWhatGetsSent()
    {
        var settings = new PluginSettings();
        settings.RecordDisplayNameAlias("The Cartographer");

        var sent = settings.DisplayNameOr(CharacterName);

        Assert.True(sent.WasStated);
        Assert.Equal("The Cartographer", sent.Value);
    }

    // THE ONE THAT MATTERS MOST. An alias DisplayName refuses -- control characters, bidi
    // overrides, overlong -- must fall back to the character name and NOT to None. Fails if: a typo
    // silently becomes anonymity, which would show the DM "a player who gave no name" and look
    // deliberate rather than broken.
    [Theory]
    [InlineData("with\na newline")]
    [InlineData("with‮a bidi override")]
    [InlineData("with​a zero width space")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void AnUnusableAliasFallsBackToTheCharacterNameRatherThanToNothing(string unusable)
    {
        var settings = new PluginSettings();
        settings.RecordDisplayNameAlias(unusable);

        var sent = settings.DisplayNameOr(CharacterName);

        Assert.Equal(CharacterName, sent);
        Assert.NotEqual(DisplayName.None, sent);
    }

    // The control for the theory above: those inputs must be rejected BY DisplayName, not merely
    // absent from what was stored. Without this, a bug that dropped the alias on the floor would
    // make the fallback test pass for the wrong reason.
    [Theory]
    [InlineData("with\na newline")]
    [InlineData("with‮a bidi override")]
    [InlineData("with​a zero width space")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ThoseInputsAreGenuinelyStoredAndGenuinelyRefused(string unusable)
    {
        var settings = new PluginSettings();
        settings.RecordDisplayNameAlias(unusable);

        Assert.NotEmpty(settings.DisplayNameAlias);
        Assert.False(DisplayName.TryParse(settings.DisplayNameAlias, out _));
    }

    // Fails if: clearing the box leaves the old alias in force, so a user cannot get back to their
    // character name without uninstalling.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ClearingTheAliasReturnsToTheCharacterName(string? cleared)
    {
        var settings = new PluginSettings();
        settings.RecordDisplayNameAlias("The Cartographer");

        settings.RecordDisplayNameAlias(cleared);

        Assert.Equal(string.Empty, settings.DisplayNameAlias);
        Assert.Equal(CharacterName, settings.DisplayNameOr(CharacterName));
    }

    // The house pattern for every Record* on this type: report whether anything changed, so the
    // caller does not rewrite an identical file. Draw runs every frame, so this one is called a lot.
    [Fact]
    public void RecordingTheSameAliasTwiceReportsNoChange()
    {
        var settings = new PluginSettings();

        Assert.True(settings.RecordDisplayNameAlias("The Cartographer"));
        Assert.False(settings.RecordDisplayNameAlias("The Cartographer"));
    }

    // Surrounding whitespace is not a different name, and a name that is only whitespace is no name
    // at all. Fails if: " Bob " and "Bob" are stored as two different aliases and each save rewrites.
    [Fact]
    public void SurroundingWhitespaceIsNotADifferentName()
    {
        var settings = new PluginSettings();
        settings.RecordDisplayNameAlias("The Cartographer");

        Assert.False(settings.RecordDisplayNameAlias("  The Cartographer  "));
        Assert.Equal("The Cartographer", settings.DisplayNameAlias);
    }

    // The alias is persisted (this is the Tier 1 half the ticket carries), so it must survive the
    // round trip like every other field. Fails if: it is added to the type and forgotten by the
    // serializer, which the existing round-trip test would not have noticed.
    [Fact]
    public void TheAliasSurvivesBeingSavedAndLoaded()
    {
        var saved = new PluginSettings();
        saved.RecordDisplayNameAlias("The Cartographer");

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(saved);
        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<PluginSettings>(json)!;

        Assert.Equal("The Cartographer", loaded.DisplayNameAlias);
        Assert.Equal("The Cartographer", loaded.DisplayNameOr(CharacterName).Value);
    }

    // R-1.3e "PRE-FILLED with their character name". The Spec Owner ruled this a citation that
    // changes what is built: an empty box fails it. Fails if: a fresh install shows a blank field,
    // so the user has to already know what would be sent in order to see it.
    [Fact]
    public void TheBoxStartsPreFilledWithTheCharacterName()
    {
        var settings = new PluginSettings();

        Assert.Equal(CharacterName.Value, settings.NameToEdit(CharacterName));
    }

    // Fails if: the box shows the EFFECTIVE name rather than what was typed. With an unusable alias
    // stored the effective name is the character name -- showing that would replace the user's input
    // with something they did not type, while the warning beside it tells them to fix a value the
    // box no longer contains.
    [Fact]
    public void AnUnusableAliasStaysVisibleSoItCanBeCorrected()
    {
        var settings = new PluginSettings();
        settings.RecordDisplayNameAlias("with\na newline");

        Assert.Equal("with\na newline", settings.NameToEdit(CharacterName));
        Assert.Equal(CharacterName, settings.DisplayNameOr(CharacterName));
    }

    // The box is pre-filled, so the commonest edit is no edit. Fails if: leaving it alone stores the
    // character name as an alias -- which pins today's name, so a renamed player keeps sending the
    // old one with nothing on screen explaining why.
    [Fact]
    public void LeavingThePreFilledNameAloneStoresNoAlias()
    {
        var settings = new PluginSettings();

        var changed = settings.RecordChosenName(settings.NameToEdit(CharacterName), CharacterName);

        Assert.False(changed);
        Assert.Equal(string.Empty, settings.DisplayNameAlias);
    }

    // The same rule after an alias was set: typing your character name back means "go back to
    // tracking it", not "freeze this string".
    [Fact]
    public void TypingTheCharacterNameBackClearsTheAlias()
    {
        var settings = new PluginSettings();
        settings.RecordChosenName("The Cartographer", CharacterName);

        settings.RecordChosenName(CharacterName.Value, CharacterName);

        Assert.Equal(string.Empty, settings.DisplayNameAlias);
        Assert.Equal(CharacterName, settings.DisplayNameOr(CharacterName));
    }

    // Fails if: a stored alias that equals the character name survives a rename. This is the harm
    // the clearing rule exists to prevent, asserted rather than described.
    [Fact]
    public void AfterARenameTheDefaultFollowsTheNewCharacterName()
    {
        var settings = new PluginSettings();
        settings.RecordChosenName(CharacterName.Value, CharacterName);

        var renamed = DisplayName.OrNone("Rum Bottle II");

        Assert.Equal(renamed, settings.DisplayNameOr(renamed));
    }

    // Settings written before this field existed must load, and must behave as "no alias" rather
    // than as an empty name. Fails if: the field is made required, or null from an older file
    // reaches DisplayName and is treated as a stated-but-empty name.
    [Fact]
    public void SettingsWrittenBeforeThisFieldExistedStillLoad()
    {
        var older = Newtonsoft.Json.JsonConvert.DeserializeObject<PluginSettings>(
            "{\"MainWindowOpen\":true,\"RestoreWindowState\":true}")!;

        Assert.Equal(CharacterName, older.DisplayNameOr(CharacterName));
    }

    // A-1.2g, and it is the criterion that says how to test it: "assert on what LEAVES THE CLIENT,
    // not on what a settings screen shows". A settings field whose value never reaches the wire
    // passes every obvious test -- the field exists, it saves, the UI looks right -- while the wire
    // still carries the character name. So this drives a join and reads the envelope.
    //
    // It composes the name the way Plugin.cs does rather than re-deciding it. What it cannot reach
    // is the ImGui call itself; that residual is stated in the PR rather than papered over.
    [Fact]
    public void WhatLeavesTheClientIsWhatTheUserSet()
    {
        var settings = new PluginSettings();
        settings.RecordChosenName("The Cartographer", CharacterName);

        var sent = JoinAndReadTheWire(settings);

        Assert.Equal("The Cartographer", sent);
    }

    // The other half of A-1.2g, and the default case: no alias means the character name reaches the
    // wire. Fails if: the fallback stops at the preview and the join sends nothing.
    [Fact]
    public void WithNoAliasTheCharacterNameLeavesTheClient()
    {
        var sent = JoinAndReadTheWire(new PluginSettings());

        Assert.Equal(CharacterName.Value, sent);
    }

    // An unusable alias must not reach the wire as anonymity. Same reasoning as the fallback test
    // above, asserted where A-1.2g asks for it -- on what left.
    [Fact]
    public void AnUnusableAliasSendsTheCharacterNameRatherThanNothing()
    {
        var settings = new PluginSettings();
        settings.RecordDisplayNameAlias("with\na newline");

        var sent = JoinAndReadTheWire(settings);

        Assert.Equal(CharacterName.Value, sent);
    }

    // Composes the supplier exactly as Plugin.cs does, then joins and reads the join request off the
    // transport.
    private static string? JoinAndReadTheWire(PluginSettings settings)
    {
        var transport = new RecordingTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default);

        coordinator.RequestJoin(
            SessionCode.FromValid("BCDFGH"),
            settings.DisplayNameOr(CharacterName));
        transport.OpenTheSocket = true;
        coordinator.Tick(TimeSpan.Zero, new DateTimeOffset(2026, 8, 28, 4, 0, 0, TimeSpan.Zero));

        return transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null && e.Type == WireMessageType.JoinRequest)
            .Select(e => e!.DisplayName)
            .FirstOrDefault();
    }

    private sealed class RecordingTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        // Explicit accessors rather than a field-like event, because nothing here delivers an
        // inbound frame: A-1.2g is about what LEAVES the client, so this double is send-only. A
        // field-like event nothing raises is CS0067, and the alternatives were both worse — a
        // Deliver method no test calls is dead code, and inventing an inbound test to justify the
        // event would be writing a test to satisfy a compiler rather than a requirement.
        public event Action<byte[]>? Received
        {
            add { }
            remove { }
        }

        public List<byte[]> Sent { get; } = new();

        public bool OpenTheSocket { get; set; }

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected && OpenTheSocket;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        // Mirrors the real transport: a frame sent before the socket opens is discarded (BUG-36).
        public void Send(byte[] envelope)
        {
            if (IsReadyToSend)
            {
                Sent.Add(envelope);
            }
        }

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
