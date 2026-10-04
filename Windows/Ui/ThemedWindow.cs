using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>A window drawn in the Chronicle theme: Cinzel title bar, Axis body, undone after every draw.</summary>
internal abstract class ThemedWindow : Window
{
    private IDisposable? _theme;
    private IDisposable? _titleFont;
    private bool _failureLogged;

    protected ThemedWindow(string name, UiFonts fonts, IPluginLog log, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        : base(name, flags)
    {
        Fonts = fonts;
        Log = log;
    }

    protected UiFonts Fonts { get; }

    protected IPluginLog Log { get; }

    public override void PreDraw()
    {
        _theme = Theme.Push();
        _titleFont = Fonts.Title.Push();
    }

    public sealed override void Draw()
    {
        using var body = Fonts.Body.Push();
        try
        {
            DrawContent();
        }
        catch (Exception exception)
        {
            if (!_failureLogged)
            {
                Log.Error(exception, "The {Window} window failed while drawing.", WindowName);
                _failureLogged = true;
            }

            ImGui.TextColored(Palette.Danger, "This window hit a problem and stopped drawing. Details are in /xllog.");
        }
    }

    public override void PostDraw()
    {
        _titleFont?.Dispose();
        _titleFont = null;
        _theme?.Dispose();
        _theme = null;
    }

    protected abstract void DrawContent();
}
