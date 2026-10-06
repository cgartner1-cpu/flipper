using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Monsters;
using StardewValley.TerrainFeatures;
using ZagreusValley.Generated;

namespace ZagreusValley.Systems;

/// <summary>sys_boon_apply, sys_boon_clear and the hook boons (sys_zeus_chain, sys_dionysus_hangover,
/// sys_aphrodite_weak, sys_demeter_growth). A boon is a Stardew buff that lasts until the day ends.</summary>
public sealed class BoonSystem
{
    const string BuffPrefix = "ZagreusValley.Boon.";
    static readonly Dictionary<Monster, int> Hangover = new();       // monster -> seconds left (Dionysus)
    static readonly Dictionary<Monster, int> Weakened = new();       // monster -> seconds left (Aphrodite)
    static bool inBoonDamage;                                        // stops boon damage from re-triggering boons

    readonly IModHelper helper;
    public BoonSystem(IModHelper helper) => this.helper = helper;

    public void Register()
    {
        helper.Events.GameLoop.DayEnding += (_, _) => { DemeterGrowth(); ClearAll(); };
        helper.Events.GameLoop.OneSecondUpdateTicked += (_, _) => { if (Context.IsWorldReady) TickStatuses(); };
        helper.Events.Player.Warped += (_, _) => { Hangover.Clear(); Weakened.Clear(); };
    }

    static string BuffId(BoonRow b) => BuffPrefix + b.God;
    public static bool Has(string boonId) => BoonsSheet.Rows.FirstOrDefault(b => b.Id == boonId) is { } b && Game1.player.hasBuff(BuffId(b));
    static BoonRow Row(string id) => BoonsSheet.Rows.First(b => b.Id == id);

    public void Grant(BoonRow b)
    {
        var effects = new BuffEffects();
        if (b.EffectKind == "stat")
        {
            var field = typeof(BuffEffects).GetField(b.StatField, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"BuffEffects has no field {b.StatField} ({b.Id})");
            ((Netcode.NetFloat)field.GetValue(effects)!).Value = b.Amount;
        }
        var icon = ModEntry.Art.Get("art_icon_" + b.God.ToLowerInvariant());
        Game1.player.applyBuff(new Buff(BuffId(b), source: "Zagreus Valley", displaySource: b.God,
            duration: Buff.ENDLESS, iconTexture: icon ?? Game1.buffsIcons, iconSheetIndex: icon is null ? 0 : 0,
            effects: effects, displayName: $"{b.BoonName} ({b.God})", description: b.Description));
        Game1.addHUDMessage(new HUDMessage($"{b.God}: {b.BoonName}. {b.Description}", HUDMessage.newQuest_type));
        Game1.playSound("yoba");
    }

    void ClearAll()
    {
        foreach (var b in BoonsSheet.Rows) Game1.player.buffs.Remove(BuffId(b));
        Hangover.Clear(); Weakened.Clear();
    }

    void TickStatuses()
    {
        var loc = Game1.currentLocation;
        var dion = Row("b_dionysus");
        foreach (var m in Hangover.Keys.ToList())
        {
            if (m.Health <= 0 || !loc.characters.Contains(m) || --Hangover[m] < 0) { Hangover.Remove(m); continue; }
            BoonDamage(loc, m, (int)dion.Amount);
        }
        foreach (var m in Weakened.Keys.ToList())
            if (--Weakened[m] < 0 || m.Health <= 0) Weakened.Remove(m);
    }

    void DemeterGrowth()
    {
        if (!Has("b_demeter")) return;
        int extra = (int)Row("b_demeter").Amount;
        foreach (var loc in new[] { Game1.getFarm() })
            foreach (var dirt in loc.terrainFeatures.Values.OfType<HoeDirt>())
                if (dirt.crop is { } crop && dirt.state.Value == HoeDirt.watered)
                    for (int i = 0; i < extra; i++) crop.newDay(dirt.state.Value);
    }

    static void BoonDamage(GameLocation loc, Monster m, int amount)
    {
        inBoonDamage = true;
        try { loc.damageMonster(m.GetBoundingBox(), amount, amount, false, Game1.player); }
        finally { inBoonDamage = false; }
    }

    // ---- Harmony: detect which monsters a player hit, by comparing health before/after GameLocation.damageMonster ----
    public static void ApplyPatches(Harmony h)
    {
        foreach (var m in typeof(GameLocation).GetMethods().Where(m => m.Name == nameof(GameLocation.damageMonster) && m.ReturnType == typeof(bool)))
            h.Patch(m, prefix: new HarmonyMethod(typeof(BoonSystem), nameof(DamagePrefix)), postfix: new HarmonyMethod(typeof(BoonSystem), nameof(DamagePostfix)));
        h.Patch(AccessTools.Method(typeof(Farmer), nameof(Farmer.takeDamage)), prefix: new HarmonyMethod(typeof(BoonSystem), nameof(TakeDamagePrefix)));
    }

    static void DamagePrefix(GameLocation __instance, out Dictionary<Monster, int>? __state)
    {
        __state = inBoonDamage ? null : __instance.characters.OfType<Monster>().ToDictionary(m => m, m => m.Health);
    }

    static void DamagePostfix(GameLocation __instance, Dictionary<Monster, int>? __state)
    {
        if (__state is null) return;
        var hit = __state.Where(kv => kv.Key.Health < kv.Value).Select(kv => kv.Key).ToList();
        if (hit.Count == 0) return;
        foreach (var m in hit)
        {
            if (Has("b_dionysus")) Hangover[m] = 5;
            if (Has("b_aphrodite")) Weakened[m] = 10;
        }
        if (Has("b_zeus"))
        {
            int dmg = (int)Row("b_zeus").Amount;
            var origin = hit[0].GetBoundingBox().Center;
            var targets = __instance.characters.OfType<Monster>()
                .Where(m => !hit.Contains(m) && m.Health > 0 && Vector2.Distance(origin.ToVector2(), m.GetBoundingBox().Center.ToVector2()) <= 3 * 64)
                .Take(3).ToList();
            if (targets.Count > 0) __instance.playSound("thunder_small");
            foreach (var t in targets) BoonDamage(__instance, t, dmg);
        }
    }

    static void TakeDamagePrefix(ref int damage, Monster damager)
    {
        if (damager is not null && Weakened.ContainsKey(damager))
            damage = Math.Max(1, (int)(damage * Row("b_aphrodite").Amount));
    }
}
