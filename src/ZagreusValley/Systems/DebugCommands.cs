using System.Linq;
using StardewModdingAPI;
using StardewValley;
using ZagreusValley.Generated;
using ZagreusValley.HadesArt;

namespace ZagreusValley.Systems;

/// <summary>sys_debug_cmds: SMAPI console commands for repeatable in-game tests.</summary>
public static class DebugCommands
{
    public static void Register(IModHelper helper, OrbSpawner orbs, ArtCache art)
    {
        helper.ConsoleCommands.Add("zag_orb", "Place a boon orb in front of you. Usage: zag_orb <god>, e.g. zag_orb zeus", (_, a) =>
        {
            if (!Context.IsWorldReady || a.Length == 0) return;
            var b = BoonsSheet.Rows.FirstOrDefault(r => r.God.Equals(a[0], System.StringComparison.OrdinalIgnoreCase));
            if (b is null) { ModEntry.Log.Log("gods: " + string.Join(", ", BoonsSheet.Rows.Select(r => r.God)), LogLevel.Info); return; }
            orbs.PlaceNear(Game1.player, b);
        });
        helper.ConsoleCommands.Add("zag_spawn", "Spawn a Hades enemy near you. Usage: zag_spawn <enemy id>, e.g. zag_spawn e_thug", (_, a) =>
        {
            if (!Context.IsWorldReady || a.Length == 0) return;
            var e = EnemiesSheet.Rows.FirstOrDefault(r => r.Id == a[0]);
            if (e is null) { ModEntry.Log.Log("enemies: " + string.Join(", ", EnemiesSheet.Rows.Select(r => r.Id)), LogLevel.Info); return; }
            RoamingSpawner.Spawn(Game1.currentLocation, e, Game1.player.Tile, 3);
        });
        helper.ConsoleCommands.Add("zag_reextract", "Delete the Hades art cache and extract it again.", (_, _) => art.EnsureExtracted(force: true));
    }
}
