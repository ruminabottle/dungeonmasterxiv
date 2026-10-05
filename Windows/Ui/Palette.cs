using System.Globalization;
using System.Numerics;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>The Chronicle colour roles. Components use these names, never raw values.</summary>
internal static class Palette
{
    public static readonly Vector4 Surface = Hex("17140F");
    public static readonly Vector4 SurfaceRaised = Hex("221D15");
    public static readonly Vector4 SurfaceHover = Hex("2A2318");
    public static readonly Vector4 SurfaceSunk = Hex("120F0B");
    public static readonly Vector4 Rule = Hex("6B5631");
    public static readonly Vector4 RuleSoft = Hex("3A3020");
    public static readonly Vector4 RuleStrong = Hex("8A7040");
    public static readonly Vector4 Text = Hex("E9DFC9");
    public static readonly Vector4 TextMuted = Hex("B5A888");
    public static readonly Vector4 Gold = Hex("C9A65A");
    public static readonly Vector4 GoldBright = Hex("F2D891");
    public static readonly Vector4 GoldLabel = Hex("D8BF86");
    public static readonly Vector4 PrivateSurface = Hex("1F1A22");
    public static readonly Vector4 PrivateRule = Hex("6A5478");
    public static readonly Vector4 PrivateText = Hex("CFC2DC");
    public static readonly Vector4 Warning = Hex("D9824A");
    public static readonly Vector4 Danger = Hex("D9665A");

    private static Vector4 Hex(string rgb) => new(
        int.Parse(rgb[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f,
        int.Parse(rgb[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f,
        int.Parse(rgb[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f,
        1f);
}
