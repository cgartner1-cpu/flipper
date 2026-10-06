using HarmonyLib;
using StardewModdingAPI;
using ZagreusValley.HadesArt;
using ZagreusValley.Systems;

namespace ZagreusValley;

/// <summary>Wires every systems.json row to its SMAPI event or Harmony patch.</summary>
public sealed class ModEntry : Mod
{
    internal static IMonitor Log = null!;
    internal static ArtCache Art = null!;

    public override void Entry(IModHelper helper)
    {
        Log = Monitor;
        Art = new ArtCache(helper, Monitor);                       // sys_hades_locate + sys_art_extract
        helper.Events.GameLoop.GameLaunched += (_, _) => Art.EnsureExtracted();

        new AssetReplacer(helper, Art).Register();                 // sys_asset_replace, sys_npc_names
        new MonsterTuner(helper).Register();                       // sys_monster_tune
        var boons = new BoonSystem(helper);                        // sys_boon_apply, sys_boon_clear, hook boons
        boons.Register();
        var orbs = new OrbSpawner(helper, boons, Art);             // sys_orb_spawn
        orbs.Register();
        new RoamingSpawner(helper).Register();                     // sys_roaming_spawn
        DebugCommands.Register(helper, orbs, Art);                 // sys_debug_cmds

        var harmony = new Harmony(ModManifest.UniqueID);
        FarmerRenderPatch.Apply(harmony, Art);                     // sys_farmer_render
        BoonSystem.ApplyPatches(harmony);                          // sys_zeus_chain, sys_dionysus_hangover, sys_aphrodite_weak
    }
}
