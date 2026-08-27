using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class KeyFingerprintTests
{
    // Of() hashes whatever bytes it is handed, so a synthetic "key" is enough to exercise the
    // rendering and is far cheaper than generating real key pairs.
    private static byte[] SyntheticKey(int seed) => BitConverter.GetBytes(seed);

    private static string Raw(string fingerprint) => fingerprint.Replace("-", string.Empty);

    // Fails if: the fingerprint is not a function of the key. The DM and the joiner compare it out
    // of band, so it has to be the same string on both screens for the same key.
    [Fact]
    public void TheSameKeyAlwaysGivesTheSameFingerprint()
    {
        using var party = new SessionKeyExchange();

        Assert.Equal(KeyFingerprint.Of(party.PublicKey), KeyFingerprint.Of(party.PublicKey));
    }

    // Fails if: the fingerprint is a constant. A fingerprint that matches for every key would show
    // the DM a reassuring match no matter who was actually on the other end.
    [Fact]
    public void DifferentKeysGiveDifferentFingerprints()
    {
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();

        Assert.NotEqual(KeyFingerprint.Of(first.PublicKey), KeyFingerprint.Of(second.PublicKey));
    }

    // Fails if: the length drifts from R-1.3a's decided eleven. Eleven is not a tidy number chosen
    // for looks — it is ~50.4 bits, and it is only enough because the admission prompt expires.
    [Fact]
    public void TheFingerprintIsElevenCharactersLong()
    {
        using var party = new SessionKeyExchange();

        Assert.Equal(KeyFingerprint.Characters, Raw(KeyFingerprint.Of(party.PublicKey)).Length);
        Assert.Equal(11, KeyFingerprint.Characters);
    }

    // Fails if: the grouping changes to something a person cannot read aloud in one breath.
    // R-1.3a decided three-three-three-two.
    [Fact]
    public void TheFingerprintIsGroupedThreeThreeThreeTwo()
    {
        using var party = new SessionKeyExchange();

        var groups = KeyFingerprint.Of(party.PublicKey).Split('-');

        Assert.Equal(new[] { 3, 3, 3, 2 }, groups.Select(group => group.Length).ToArray());
    }

    // Fails if: the fingerprint goes back to hex, or to any alphabet other than the shared one.
    // Hex would reintroduce A, E, 0, 1 and 5 — every one of them a character R-1.2a excluded for
    // being a vowel or confusable aloud, which is the whole reason a fingerprint is read at all.
    [Fact]
    public void EveryCharacterComesFromTheOneSpeakableAlphabet()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var raw = Raw(KeyFingerprint.Of(SyntheticKey(seed)));

            Assert.All(raw, character => Assert.Contains(character, SpeakableAlphabet.Characters));
        }
    }

    // Fails if: the fingerprint is rendered in an alphabet SessionCode would reject — hex being the
    // case that was actually shipped.
    //
    // Deliberately NOT Assert.Equal(SessionCode.Alphabet, SpeakableAlphabet.Characters). Since
    // SessionCode.Alphabet is now DEFINED as SpeakableAlphabet.Characters, that assertion compares
    // a constant with itself and cannot fail. This runs fingerprint characters through
    // SessionCode's own independent parser instead, so the claim "same alphabet as session codes"
    // is tested by the code that enforces it rather than by re-reading the constant.
    [Fact]
    public void FingerprintCharactersAreAcceptedBySessionCodesOwnParser()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var raw = Raw(KeyFingerprint.Of(SyntheticKey(seed)));

            Assert.True(
                SessionCode.TryParse(raw[..SessionCode.Length], out _),
                $"SessionCode rejected characters taken from a fingerprint: {raw}");
        }
    }

    // Fails if: distinct keys collide at this length in a sample this small, which would mean the
    // rendering is discarding far more of the digest than truncation to ~50.4 bits should.
    [Fact]
    public void DistinctKeysDoNotCollideAcrossALargeSample()
    {
        var seen = new HashSet<string>();

        for (var seed = 0; seed < 20_000; seed++)
        {
            Assert.True(seen.Add(KeyFingerprint.Of(SyntheticKey(seed))));
        }
    }

    // Fails if: the digest is mapped to the alphabet with a modulo, which is the tempting
    // simplification and the one that silently costs entropy.
    //
    // A byte reduced modulo 24 is biased because 256 is not a multiple of 24: sixteen symbols come
    // up eleven times per 256 and the other eight come up ten, so the last eight run ~6.25% light.
    // Eleven biased characters carry measurably less than the 50.4 bits R-1.3a's length decision
    // assumes, and nothing about the rendered string would look wrong.
    //
    // The inputs are a fixed range of seeds, so this is DETERMINISTIC — it does not flake, it is
    // the same 550,000 symbols on every run and it either passes always or fails always. The
    // statistics below say why this fixed sample is a fair one, not what the flake rate is.
    //
    // Expected 22,917 per symbol, sigma ~148. The 3% tolerance is ~4.6 sigma, so an unbiased
    // mapping sits comfortably inside it; the modulo bias this exists to catch runs ~6.25% light on
    // eight of the symbols, or ~9.7 sigma, well outside. Verified by substituting that exact
    // implementation: this test failed and no other test in the suite noticed.
    [Fact]
    public void SymbolsAreDrawnUniformlyRatherThanWithAModuloBias()
    {
        const int Keys = 50_000;
        var counts = new Dictionary<char, int>();

        for (var seed = 0; seed < Keys; seed++)
        {
            foreach (var character in Raw(KeyFingerprint.Of(SyntheticKey(seed))))
            {
                counts[character] = counts.GetValueOrDefault(character) + 1;
            }
        }

        var expected = (double)(Keys * KeyFingerprint.Characters) / SpeakableAlphabet.Length;

        Assert.Equal(SpeakableAlphabet.Length, counts.Count);
        Assert.All(counts, pair => Assert.InRange(pair.Value, expected * 0.97, expected * 1.03));
    }
}
