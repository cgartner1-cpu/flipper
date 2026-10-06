using System;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Locations;
using StardewValley.Monsters;
using ZagreusValley.Generated;

namespace ZagreusValley.Systems;

/// <summary>sys_monster_tune: renames and rescales reskinned monsters as they appear (Mines band from enemies.json, or roaming spawns).</summary>
public sealed class MonsterTuner
{
    public const string TunedKey = "ZagreusValley/Tuned", RoamingKey = "ZagreusValley/Roaming";
    readonly IModHelper helper;
    public MonsterTuner(IModHelper helper) => this.helper = helper;
    public void Register() => helper.Events.World.NpcListChanged += (_, e) =>
    {
        foreach (var m in e.Added.OfType<Monster>()) Tune(m, e.Location is MineShaft ms ? ms.mineLevel : -1);
    };

    public static void Tune(Monster m, int mineLevel)
    {
        if (m.modData.ContainsKey(TunedKey)) return;
        var row = EnemiesSheet.Rows.FirstOrDefault(r => r.StardewMonster == m.GetType().Name);
        if (row is null) return;
        bool roaming = m.modData.ContainsKey(RoamingKey);
        if (!roaming && !InBand(row.MinesFloors, mineLevel)) return;
        m.displayName = row.HadesEnemy;
        m.MaxHealth = Math.Max(1, (int)(m.MaxHealth * row.HealthMult)); m.Health = m.MaxHealth;
        m.DamageToFarmer = Math.Max(1, (int)(m.DamageToFarmer * row.DamageMult));
        m.speed += row.SpeedDelta;
        m.modData[TunedKey] = row.Id;
    }

    static bool InBand(string band, int level)
    {
        var p = band.Split('-'); return level >= int.Parse(p[0]) && level <= int.Parse(p[1]);
    }
}
