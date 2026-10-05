using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DungeonMasterXIV.Windows.Ui.Components;

/// <summary>A padded, full-width panel drawn behind whatever is drawn inside its scope. Cards do not nest.</summary>
internal readonly struct Card : IDisposable
{
    private readonly Vector2 _start;
    private readonly float _width;
    private readonly Vector4 _surface;
    private readonly Vector4 _edge;
    private readonly Vector4? _leftEdge;

    private Card(Vector4 surface, Vector4 edge, Vector4? leftEdge)
    {
        _surface = surface;
        _edge = edge;
        _leftEdge = leftEdge;
        _start = ImGui.GetCursorScreenPos();
        _width = ImGui.GetContentRegionAvail().X;

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        var padding = Metrics.CardPadding;
        ImGui.SetCursorScreenPos(_start + new Vector2(padding + (leftEdge is null ? 0f : Metrics.EdgeWidth), padding));
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + _width - (2f * padding) - (leftEdge is null ? 0f : Metrics.EdgeWidth));
    }

    public static Card Begin(Vector4 surface, Vector4 edge) => new(surface, edge, null);

    /// <summary>The width left on the current line inside a card, stopping at its right padding.</summary>
    public static float InnerWidth() => ImGui.GetContentRegionAvail().X - Metrics.CardPadding;

    /// <summary>A card with a thick coloured left edge, used for banners.</summary>
    public static Card WithLeftEdge(Vector4 surface, Vector4 edge, Vector4 leftEdge) => new(surface, edge, leftEdge);

    public void Dispose()
    {
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();

        var end = new Vector2(_start.X + _width, ImGui.GetItemRectMax().Y + Metrics.CardPadding);
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(_start, end, ImGui.GetColorU32(_surface), Metrics.ControlRounding);
        drawList.AddRect(_start, end, ImGui.GetColorU32(_edge), Metrics.ControlRounding);
        if (_leftEdge is { } left)
        {
            drawList.AddRectFilled(
                _start, new Vector2(_start.X + Metrics.EdgeWidth, end.Y), ImGui.GetColorU32(left), Metrics.ControlRounding);
        }

        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(_start.X, end.Y));
        ImGui.Dummy(new Vector2(_width, Metrics.CardGap));
    }
}
