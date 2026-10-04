using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A roll: speaker and time, the label, the expression, every die, and the total.</summary>
internal static class RollCard
{
    public const string OnlyYou = "Only you saw this.";

    public static void Draw(UiFonts fonts, SpeakerName speaker, long atUtcTicks, SharedRoll roll, bool local = false)
    {
        using var card = Card.Begin(Palette.SurfaceRaised, Palette.Rule);
        Speaker.DrawWithTime(fonts, speaker, atUtcTicks);

        if (!string.IsNullOrWhiteSpace(roll.Label))
        {
            using var title = fonts.Title.Push();
            ImGui.TextColored(Palette.GoldLabel, roll.Label);
        }

        Bar(fonts.Body, roll.Expression, Palette.Text);
        DrawDice(fonts, roll);
        Bar(fonts.Total, roll.Total.ToString(CultureInfo.InvariantCulture), Palette.GoldBright);

        if (roll.Notice is { } notice)
        {
            ImGui.TextColored(Palette.Warning, notice);
        }

        if (local)
        {
            using var meta = fonts.Meta.Push();
            ImGui.TextColored(Palette.TextMuted, OnlyYou);
        }
    }

    /// <summary>Kept dice plain; set-aside dice muted and struck through, so the audit trail stays visible.</summary>
    private static void DrawDice(UiFonts fonts, SharedRoll roll)
    {
        if (roll.Dice.Count == 0)
        {
            return;
        }

        using var meta = fonts.Meta.Push();
        var right = ImGui.GetCursorScreenPos().X + Card.InnerWidth();
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        for (var index = 0; index < roll.Dice.Count; index++)
        {
            var die = roll.Dice[index];
            var text = die.Value.ToString(CultureInfo.InvariantCulture);
            var width = ImGui.CalcTextSize(text).X;

            if (index > 0)
            {
                ImGui.SameLine();
                if (ImGui.GetCursorScreenPos().X + width > right)
                {
                    ImGui.NewLine();
                }
            }

            var start = ImGui.GetCursorScreenPos();
            ImGui.TextColored(die.Kept ? Palette.Text : Palette.TextMuted, text);

            if (!die.Kept)
            {
                var middle = start.Y + (ImGui.GetTextLineHeight() / 2f);
                ImGui.GetWindowDrawList().AddLine(
                    new Vector2(start.X - 1f, middle),
                    new Vector2(start.X + width + 1f, middle),
                    ImGui.GetColorU32(Palette.TextMuted));
            }
        }

        ImGui.Dummy(new Vector2(0f, spacing / 2f));
    }

    private static void Bar(IFontHandle font, string text, Vector4 colour)
    {
        using var pushed = font.Push();
        var start = ImGui.GetCursorScreenPos();
        var width = Card.InnerWidth();
        var height = ImGui.GetTextLineHeight() + (2f * Metrics.Step);
        var end = start + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(start, end, ImGui.GetColorU32(Palette.SurfaceSunk), Metrics.ControlRounding);
        drawList.AddRect(start, end, ImGui.GetColorU32(Palette.RuleSoft), Metrics.ControlRounding);

        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorScreenPos(new Vector2(start.X + ((width - textWidth) / 2f), start.Y + Metrics.Step));
        ImGui.TextColored(colour, text);
        ImGui.SetCursorScreenPos(new Vector2(start.X, end.Y + Metrics.Step));
        ImGui.Dummy(Vector2.Zero);
    }
}
