using System;
using System.Globalization;
using System.Text;

namespace DungeonMasterXIV.Net;

public readonly struct DisplayName : IEquatable<DisplayName>
{
    public const int MaxLength = 32;

    internal static int PerceivedLength(string value) => new StringInfo(value).LengthInTextElements;

    public const int MaxUtf8Bytes = (MaxLength * 8) + 1;

    public const string Unstated = "a player who gave no name";

    private readonly string? _value;

    private DisplayName(string value) => _value = value;

    public string Value => _value ?? Unstated;

    public bool WasStated => _value is not null;

    public static DisplayName None => default;

    public static bool TryParse(string? candidate, out DisplayName name)
    {
        name = default;

        if (candidate is null)
        {
            return false;
        }

        var trimmed = candidate.Trim();
        if (trimmed.Length == 0 || PerceivedLength(trimmed) > MaxLength)
        {
            return false;
        }

        if (IsReservedToTheHost(trimmed))
        {
            return false;
        }

        var rendersSomething = false;

        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (!IsPermitted(rune))
            {
                return false;
            }

            rendersSomething |= HasAGlyphOfItsOwn(rune);
        }

        if (!rendersSomething)
        {
            return false;
        }

        name = new DisplayName(trimmed);
        return true;
    }

    public static DisplayName OrNone(string? candidate) =>
        TryParse(candidate, out var name) ? name : None;

    private static readonly string[] ReservedToTheHost = ["DM", "GM", "Dungeon Master", "Game Master"];

    private static bool IsReservedToTheHost(string trimmed)
    {
        var collapsed = new StringBuilder(trimmed.Length);
        var lastWasSpace = false;

        foreach (var character in trimmed)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!lastWasSpace)
                {
                    collapsed.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            collapsed.Append(character);
            lastWasSpace = false;
        }

        var normalised = collapsed.ToString();

        foreach (var reserved in ReservedToTheHost)
        {
            if (string.Equals(normalised, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPermitted(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.DecimalDigitNumber => !IsInvisibleDespiteItsCategory(rune),

        _ => rune.Value is Space or Apostrophe or TypographicApostrophe or Hyphen or FullStop,
    };

    private static bool IsInvisibleDespiteItsCategory(Rune rune) =>
        rune.Value is 0x115F or 0x1160 or 0x3164 or 0xFFA0;

    private static bool HasAGlyphOfItsOwn(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber => !IsInvisibleDespiteItsCategory(rune),
        _ => rune.Value is Apostrophe or TypographicApostrophe or Hyphen or FullStop,
    };

    private const int Space = 0x0020;
    private const int Apostrophe = 0x0027;
    private const int Hyphen = 0x002D;
    private const int FullStop = 0x002E;
    private const int TypographicApostrophe = 0x2019;

    public bool Equals(DisplayName other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DisplayName other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public override string ToString() => Value;

    public static bool operator ==(DisplayName left, DisplayName right) => left.Equals(right);

    public static bool operator !=(DisplayName left, DisplayName right) => !left.Equals(right);
}
