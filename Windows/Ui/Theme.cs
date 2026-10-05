using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>Applies the Chronicle colours and shapes to ImGui until the returned scope is disposed.</summary>
internal static class Theme
{
    public static IDisposable Push()
    {
        var colours = ImRaii.PushColor(ImGuiCol.WindowBg, Palette.Surface)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, Palette.SurfaceRaised)
            .Push(ImGuiCol.Border, Palette.RuleStrong)
            .Push(ImGuiCol.Text, Palette.Text)
            .Push(ImGuiCol.TextDisabled, Palette.TextMuted)
            .Push(ImGuiCol.TitleBg, Palette.SurfaceRaised)
            .Push(ImGuiCol.TitleBgActive, Palette.SurfaceRaised)
            .Push(ImGuiCol.TitleBgCollapsed, Palette.Surface)
            .Push(ImGuiCol.FrameBg, Palette.SurfaceSunk)
            .Push(ImGuiCol.FrameBgHovered, Palette.SurfaceRaised)
            .Push(ImGuiCol.FrameBgActive, Palette.SurfaceHover)
            .Push(ImGuiCol.Button, Palette.SurfaceRaised)
            .Push(ImGuiCol.ButtonHovered, Palette.SurfaceHover)
            .Push(ImGuiCol.ButtonActive, Palette.RuleSoft)
            .Push(ImGuiCol.Header, Palette.SurfaceRaised)
            .Push(ImGuiCol.HeaderHovered, Palette.SurfaceHover)
            .Push(ImGuiCol.HeaderActive, Palette.SurfaceHover)
            .Push(ImGuiCol.Separator, Palette.RuleSoft)
            .Push(ImGuiCol.NavHighlight, Palette.Gold)
            .Push(ImGuiCol.CheckMark, Palette.Gold)
            .Push(ImGuiCol.SliderGrab, Palette.Gold)
            .Push(ImGuiCol.SliderGrabActive, Palette.GoldBright)
            .Push(ImGuiCol.ScrollbarBg, Palette.Surface)
            .Push(ImGuiCol.ScrollbarGrab, Palette.Rule)
            .Push(ImGuiCol.ScrollbarGrabHovered, Palette.RuleStrong)
            .Push(ImGuiCol.ScrollbarGrabActive, Palette.Gold)
            .Push(ImGuiCol.ResizeGrip, Palette.Rule)
            .Push(ImGuiCol.ResizeGripHovered, Palette.RuleStrong)
            .Push(ImGuiCol.ResizeGripActive, Palette.Gold);

        var scale = ImGuiHelpers.GlobalScale;
        var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, Metrics.WindowRounding)
            .Push(ImGuiStyleVar.ChildRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.FrameRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.PopupRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.GrabRounding, Metrics.ControlRounding)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.FrameBorderSize, 1f)
            .Push(ImGuiStyleVar.DisabledAlpha, 0.5f)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(12f, 12f) * scale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(8f, 4f) * scale)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 6f) * scale);

        return new Both(styles, colours);
    }

    private sealed class Both(IDisposable first, IDisposable second) : IDisposable
    {
        public void Dispose()
        {
            first.Dispose();
            second.Dispose();
        }
    }
}
