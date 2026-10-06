using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using ZagreusValley.HadesArt;

namespace ZagreusValley.Systems;

/// <summary>sys_farmer_render: draws Zagreus (art_zagreus_sprite, layout stardew_farmer_16x32_4dir) in place of the
/// layered farmer. Falls back to the normal farmer when the art isn't extracted.</summary>
public static class FarmerRenderPatch
{
    static ArtCache art = null!;
    // layout rows: down, right, up, left. Stardew facing: 0 up, 1 right, 2 down, 3 left.
    static readonly int[] RowForFacing = { 2, 1, 0, 3 };

    public static void Apply(Harmony h, ArtCache cache)
    {
        art = cache;
        var targets = typeof(FarmerRenderer).GetMethods()
            .Where(m => m.Name == nameof(FarmerRenderer.draw) && m.GetParameters().Any(p => p.ParameterType == typeof(Farmer))
                        && m.GetParameters().Any(p => p.Name == "position") && m.GetParameters().Any(p => p.Name == "facingDirection"));
        foreach (var m in targets)
        {
            try { h.Patch(m, prefix: new HarmonyMethod(typeof(FarmerRenderPatch), nameof(Prefix))); }
            catch (Exception ex) { ModEntry.Log.Log($"Couldn't patch {m}: {ex.Message}", LogLevel.Warn); }
        }
    }

    static bool Prefix(SpriteBatch b, Vector2 position, Vector2 origin, float layerDepth, int facingDirection, float scale, Farmer who)
    {
        if (!who.IsLocalPlayer || art.Get("art_zagreus_sprite") is not { } tex) return true;
        int row = RowForFacing[Math.Clamp(facingDirection, 0, 3)];
        int col = who.isMoving() ? (int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 150) % 4 : 0;
        b.Draw(tex, position + origin, new Rectangle(col * 16, row * 32, 16, 32), Color.White, 0f, origin / 4f, 4f * scale,
            SpriteEffects.None, layerDepth);
        return false;
    }
}
