using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class KeyFingerprintTests
{
    // A fixed second party, so the tests inherited from C8 keep measuring what they measured — the
    // rendering and the distribution — now that a fingerprint is a function of two keys.
    private static readonly byte[] Counterparty = SyntheticKey(0xC0FFEE);

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

        Assert.Equal(KeyFingerprint.Of(party.PublicKey, Counterparty), KeyFingerprint.Of(party.PublicKey, Counterparty));
    }

    // Fails if: the fingerprint is a constant. A fingerprint that matches for every key would show
    // the DM a reassuring match no matter who was actually on the other end.
    [Fact]
    public void DifferentKeysGiveDifferentFingerprints()
    {
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();

        Assert.NotEqual(KeyFingerprint.Of(first.PublicKey, Counterparty), KeyFingerprint.Of(second.PublicKey, Counterparty));
    }

    // Fails if: the length drifts from R-1.3a's decided eleven. Eleven is not a tidy number chosen
    // for looks — it is ~50.4 bits, and it is only enough because the admission prompt expires.
    [Fact]
    public void TheFingerprintIsElevenCharactersLong()
    {
        using var party = new SessionKeyExchange();

        Assert.Equal(KeyFingerprint.Characters, Raw(KeyFingerprint.Of(party.PublicKey, Counterparty)).Length);
        Assert.Equal(11, KeyFingerprint.Characters);
    }

    // Fails if: the grouping changes to something a person cannot read aloud in one breath.
    // R-1.3a decided three-three-three-two.
    [Fact]
    public void TheFingerprintIsGroupedThreeThreeThreeTwo()
    {
        using var party = new SessionKeyExchange();

        var groups = KeyFingerprint.Of(party.PublicKey, Counterparty).Split('-');

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
            var raw = Raw(KeyFingerprint.Of(SyntheticKey(seed), Counterparty));

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
            var raw = Raw(KeyFingerprint.Of(SyntheticKey(seed), Counterparty));

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
            Assert.True(seen.Add(KeyFingerprint.Of(SyntheticKey(seed), Counterparty)));
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
            foreach (var character in Raw(KeyFingerprint.Of(SyntheticKey(seed), Counterparty)))
            {
                counts[character] = counts.GetValueOrDefault(character) + 1;
            }
        }

        var expected = (double)(Keys * KeyFingerprint.Characters) / SpeakableAlphabet.Length;

        Assert.Equal(SpeakableAlphabet.Length, counts.Count);
        Assert.All(counts, pair => Assert.InRange(pair.Value, expected * 0.97, expected * 1.03));
    }
}

/// <summary>
/// A-1.3f: one fingerprint from both keys, identical on both screens.
/// </summary>
public class CombinedKeyFingerprintTests
{
    // A-1.3f's core property. Fails if: ordering becomes role-based, or anything else makes the
    // answer depend on which side is asking.
    //
    // Note what this asserts. Building both orderings is not the test — COMPARING them is. A version
    // of this that constructed both and then asserted only the format would read as covering
    // symmetry while covering nothing, which is the shape of the sharpest defect found on this
    // project: a test that constructs the counterexample and does not look at it.
    [Fact]
    public void BothPartiesComputeTheSameFingerprintFromTheSameTwoKeys()
    {
        using var host = new SessionKeyExchange();
        using var joiner = new SessionKeyExchange();

        var asTheHostComputesIt = KeyFingerprint.Of(host.PublicKey, joiner.PublicKey);
        var asTheJoinerComputesIt = KeyFingerprint.Of(joiner.PublicKey, host.PublicKey);

        Assert.Equal(asTheHostComputesIt, asTheJoinerComputesIt);
    }

    // A-1.3f's second half, and it needs BOTH directions separately. A single test showing "it
    // changes when a key changes" is satisfied by a function that ignores one argument entirely —
    // which is precisely the one-directional defence this chunk exists to remove.
    [Fact]
    public void SubstitutingTheHostsKeyChangesTheFingerprint()
    {
        using var host = new SessionKeyExchange();
        using var joiner = new SessionKeyExchange();
        using var impostor = new SessionKeyExchange();

        Assert.NotEqual(
            KeyFingerprint.Of(host.PublicKey, joiner.PublicKey),
            KeyFingerprint.Of(impostor.PublicKey, joiner.PublicKey));
    }

    [Fact]
    public void SubstitutingTheJoinersKeyChangesTheFingerprint()
    {
        using var host = new SessionKeyExchange();
        using var joiner = new SessionKeyExchange();
        using var impostor = new SessionKeyExchange();

        Assert.NotEqual(
            KeyFingerprint.Of(host.PublicKey, joiner.PublicKey),
            KeyFingerprint.Of(host.PublicKey, impostor.PublicKey));
    }

    // These two exist because the pair above is WEAKER THAN IT LOOKS, which a probe found and
    // reading did not. Substituting a random key usually changes which of the two sorts lower, so a
    // function that hashed only the canonically-lower key — one argument effectively discarded,
    // which is the one-directional defence returning — still produced a different string and passed
    // both substitution tests. It was caught only by inherited collision tests, by luck.
    //
    // These use controlled keys so the ordering is FIXED and only one side varies. Fails if: the
    // fingerprint depends on only one of the two keys.
    [Fact]
    public void ChangingOnlyTheHigherKeyChangesTheFingerprint()
    {
        var low = new byte[] { 0x01, 0x00 };

        Assert.NotEqual(
            KeyFingerprint.Of(low, new byte[] { 0x02, 0x00 }),
            KeyFingerprint.Of(low, new byte[] { 0x03, 0x00 }));
    }

    [Fact]
    public void ChangingOnlyTheLowerKeyChangesTheFingerprint()
    {
        var high = new byte[] { 0xFF, 0x00 };

        Assert.NotEqual(
            KeyFingerprint.Of(new byte[] { 0x01, 0x00 }, high),
            KeyFingerprint.Of(new byte[] { 0x02, 0x00 }, high));
    }

    // Fails if: the length prefixes are dropped. Both pairs concatenate to the same five bytes, so
    // without prefixing they hash identically and two different exchanges share a fingerprint. This
    // is the assertion that holds up the comment's claim, rather than the claim resting on P-256
    // SPKI happening to be a fixed length.
    [Fact]
    public void TwoExchangesThatConcatenateAlikeStillDiffer()
    {
        var first = KeyFingerprint.Of(new byte[] { 1, 2, 3 }, new byte[] { 4, 5 });
        var second = KeyFingerprint.Of(new byte[] { 1, 2 }, new byte[] { 3, 4, 5 });

        Assert.NotEqual(first, second);
    }

    // Fails if: the rendering drifts from R-1.3a — eleven characters of the speakable alphabet,
    // grouped three-three-three-two.
    [Fact]
    public void TheRenderedFormMatchesRule13a()
    {
        using var host = new SessionKeyExchange();
        using var joiner = new SessionKeyExchange();

        var groups = KeyFingerprint.Of(host.PublicKey, joiner.PublicKey).Split('-');

        Assert.Equal(new[] { 3, 3, 3, 2 }, groups.Select(g => g.Length).ToArray());
        Assert.All(string.Concat(groups), c => Assert.Contains(c, SpeakableAlphabet.Characters));
    }

    // THE CROSS-GUARD, and the return half of the pair PR #10 could only write one side of.
    // C6's side is AdmissionDeadline.Window's remark, which names KeyFingerprint.cs; this names
    // tests/DungeonMasterXIV.Tests/AdmissionDeadlineTests.cs back.
    //
    // Fails if: the admission prompt's expiry is removed or its window is changed without the
    // fingerprint length moving with it. R-1.3a decided eleven characters ONLY because the prompt
    // expires — against a bounded window a ten-month second-preimage search is hopeless rather than
    // merely expensive. Remove the expiry and eleven must become fourteen.
    //
    // A comment does not discharge this. A decision recorded rather than applied is what produced
    // C8, which is the chunk this one amends.
    [Fact]
    public void ElevenCharactersHoldsOnlyBecauseTheAdmissionPromptExpires()
    {
        Assert.Equal(11, KeyFingerprint.Characters);
        Assert.Equal(TimeSpan.FromMinutes(15), AdmissionDeadline.Window);
    }
}
