using System;
using System.IO;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;

namespace DungeonMasterXIV.Windows.Ui;

/// <summary>The type roles: the game's Axis for body and meta, bundled Cinzel and Spectral for the rest.</summary>
internal sealed class UiFonts : IDisposable
{
    public UiFonts(IUiBuilder ui, string fontDirectory, IPluginLog log)
    {
        Body = ui.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis14));
        Meta = ui.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis12));
        Title = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 15f, log);
        Code = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Regular.ttf"), 20f, log);
        Total = Bundled(ui, Path.Combine(fontDirectory, "Cinzel-Bold.ttf"), 24f, log);
        Voice = Bundled(ui, Path.Combine(fontDirectory, "Spectral-Regular.ttf"), 16f, log);
        Icon = ui.IconFontHandle;
    }

    public IFontHandle Body { get; }

    public IFontHandle Meta { get; }

    public IFontHandle Title { get; }

    public IFontHandle Code { get; }

    public IFontHandle Total { get; }

    public IFontHandle Voice { get; }

    /// <summary>Dalamud's FontAwesome handle. Owned by Dalamud, so never disposed here.</summary>
    public IFontHandle Icon { get; }

    public void Dispose()
    {
        Body.Dispose();
        Meta.Dispose();
        Title.Dispose();
        Code.Dispose();
        Total.Dispose();
        Voice.Dispose();
    }

    /// <summary>A bundled font, with the player's language glyphs merged in; Dalamud's default if the file is missing.</summary>
    private static IFontHandle Bundled(IUiBuilder ui, string path, float sizePx, IPluginLog log)
    {
        if (!File.Exists(path))
        {
            log.Warning("Font file {Path} is missing, so that text uses Dalamud's default font.", path);
            return ui.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(sizePx)));
        }

        return ui.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = tk.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx });
            tk.AttachExtraGlyphsForDalamudLanguage(new SafeFontConfig { SizePx = sizePx, MergeFont = font });
        }));
    }
}
