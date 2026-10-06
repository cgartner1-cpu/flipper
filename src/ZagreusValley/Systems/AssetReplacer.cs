using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Characters;
using ZagreusValley.Generated;
using ZagreusValley.HadesArt;

namespace ZagreusValley.Systems;

/// <summary>sys_asset_replace + sys_npc_names: serves the cached Hades art for villagers and monsters, and renames villagers.</summary>
public sealed class AssetReplacer
{
    readonly IModHelper helper;
    readonly ArtCache art;
    public AssetReplacer(IModHelper helper, ArtCache art) { this.helper = helper; this.art = art; }

    public void Register() => helper.Events.Content.AssetRequested += OnAssetRequested;

    void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Characters"))
        {
            e.Edit(a =>
            {
                var data = a.AsDictionary<string, CharacterData>().Data;
                foreach (var v in VillagersSheet.Rows)
                    if (data.TryGetValue(v.StardewNpc, out var c)) c.DisplayName = v.DisplayName;
            }, AssetEditPriority.Late);
            return;
        }
        foreach (var v in VillagersSheet.Rows)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(v.SpriteAsset)) { PatchTopLeft(e, v.HadesSpriteArt); return; }
            if (e.NameWithoutLocale.IsEquivalentTo(v.PortraitAsset)) { PatchTopLeft(e, v.HadesPortraitArt); return; }
        }
        foreach (var en in EnemiesSheet.Rows)
            if (e.NameWithoutLocale.IsEquivalentTo(en.TextureAsset)) { PatchEveryFrame(e, en); return; }
    }

    /// <summary>Overwrite the top-left of the vanilla sheet (walk rows / first portraits); vanilla frames beyond it stay.</summary>
    void PatchTopLeft(AssetRequestedEventArgs e, string artId)
    {
        if (art.Get(artId) is not { } tex) return;
        e.Edit(a =>
        {
            var img = a.AsImage();
            var area = new Rectangle(0, 0, Math.Min(tex.Width, img.Data.Width), Math.Min(tex.Height, img.Data.Height));
            img.PatchImage(tex, new Rectangle(0, 0, area.Width, area.Height), area, PatchMode.Replace);
        }, AssetEditPriority.Late);
    }

    /// <summary>Fill every frame cell of the vanilla monster sheet with the staged Hades frames, resampled to the vanilla frame size.</summary>
    void PatchEveryFrame(AssetRequestedEventArgs e, EnemyRow en)
    {
        if (art.Get(en.HadesArt) is not { } staged) return;
        var parts = en.VanillaFrame.Split('x'); int fw = int.Parse(parts[0]), fh = int.Parse(parts[1]);
        e.Edit(a =>
        {
            var img = a.AsImage();
            int cols = img.Data.Width / fw, rows = img.Data.Height / fh;
            int scols = 4, sframe = staged.Width / scols, srows = staged.Height / sframe;
            var src = new Color[staged.Width * staged.Height]; staged.GetData(src);
            var outp = new Color[img.Data.Width * img.Data.Height];
            for (int i = 0; i < cols * rows; i++)
            {
                int si = i % (scols * srows), sx = (si % scols) * sframe, sy = (si / scols) * sframe;
                var frame = new byte[sframe * sframe * 4];
                for (int y = 0; y < sframe; y++) for (int x = 0; x < sframe; x++)
                {
                    var c = src[(sy + y) * staged.Width + sx + x]; int o = (y * sframe + x) * 4;
                    frame[o] = c.R; frame[o + 1] = c.G; frame[o + 2] = c.B; frame[o + 3] = c.A;
                }
                var px = Fit.Resample(frame, sframe, sframe, fw, fh);
                int gx = (i % cols) * fw, gy = (i / cols) * fh;
                for (int y = 0; y < fh; y++) for (int x = 0; x < fw; x++)
                {
                    int o = (y * fw + x) * 4;
                    outp[(gy + y) * img.Data.Width + gx + x] = new Color(px[o], px[o + 1], px[o + 2], px[o + 3]);
                }
            }
            var tex = new Texture2D(StardewValley.Game1.graphics.GraphicsDevice, img.Data.Width, img.Data.Height);
            tex.SetData(outp);
            img.PatchImage(tex, patchMode: PatchMode.Replace);
        }, AssetEditPriority.Late);
    }
}
