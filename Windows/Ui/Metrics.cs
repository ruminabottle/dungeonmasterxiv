using Dalamud.Interface.Utility;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>The 4px spacing grid and corner radii, scaled by Dalamud's global scale.</summary>
internal static class Metrics
{
    public static float Step => 4f * ImGuiHelpers.GlobalScale;

    public static float CardPadding => 10f * ImGuiHelpers.GlobalScale;

    public static float CardGap => 8f * ImGuiHelpers.GlobalScale;

    public static float ControlRounding => 4f * ImGuiHelpers.GlobalScale;

    public static float WindowRounding => 6f * ImGuiHelpers.GlobalScale;

    public static float EdgeWidth => 3f * ImGuiHelpers.GlobalScale;
}
