using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using ZagreusValley.Generated;

namespace ZagreusValley.Systems;

/// <summary>sys_roaming_spawn: spawns Hades enemies outside the Mines per spawns.json, near the player, and removes them at day end.</summary>
public sealed class RoamingSpawner
{
    readonly IModHelper helper;
    readonly Dictionary<string, int> today = new();
    readonly Random rng = new();
    public RoamingSpawner(IModHelper helper) => this.helper = helper;

    public void Register()
    {
        helper.Events.GameLoop.DayStarted += (_, _) => today.Clear();
        helper.Events.GameLoop.TimeChanged += (_, _) => Roll();
        helper.Events.GameLoop.DayEnding += (_, _) =>
        {
            foreach (var loc in Game1.locations)
                loc.characters.RemoveWhere(c => c is Monster m && m.modData.ContainsKey(MonsterTuner.RoamingKey));
        };
    }

    void Roll()
    {
        var loc = Game1.currentLocation;
        if (!Context.IsWorldReady || loc is null) return;
        foreach (var row in SpawnsSheet.Rows.Where(r => r.Kind == "enemy" && r.Location == loc.Name))
        {
            today.TryGetValue(row.Id, out int n);
            var w = row.TimeWindow.Split('-');
            if (n >= row.MaxPerDay || Game1.timeOfDay < int.Parse(w[0]) || Game1.timeOfDay > int.Parse(w[1]) || rng.NextDouble() > row.Chance) continue;
            if (Spawn(loc, EnemiesSheet.Rows.First(e => e.Id == row.What), Game1.player.Tile, 6)) today[row.Id] = n + 1;
        }
    }

    /// <summary>Spawn one enemy row on an open tile about <paramref name="distance"/> tiles from <paramref name="near"/>.</summary>
    public static bool Spawn(GameLocation loc, EnemyRow row, Vector2 near, int distance)
    {
        var rng = Game1.random;
        for (int i = 0; i < 30; i++)
        {
            double ang = rng.NextDouble() * Math.PI * 2;
            var tile = new Vector2((int)(near.X + Math.Cos(ang) * distance), (int)(near.Y + Math.Sin(ang) * distance));
            if (!loc.isTileOnMap(tile) || !loc.CanItemBePlacedHere(tile) || !loc.isTilePassable(tile)) continue;
            var m = Create(row.StardewMonster, tile * 64);
            m.modData[MonsterTuner.RoamingKey] = "1";
            loc.characters.Add(m);       // MonsterTuner applies the row's stats via NpcListChanged
            return true;
        }
        return false;
    }

    static Monster Create(string cls, Vector2 pos) => cls switch
    {
        "GreenSlime" => new GreenSlime(pos, 0),
        "Bug" => new Bug(pos, 0),
        "RockCrab" => new RockCrab(pos),
        "Bat" => new Bat(pos, 0),
        "Duggy" => new Duggy(pos),
        "Skeleton" => new Skeleton(pos, false),
        "Ghost" => new Ghost(pos),
        "DustSpirit" => new DustSpirit(pos),
        "ShadowBrute" => new ShadowBrute(pos),
        "ShadowShaman" => new ShadowShaman(pos),
        "SquidKid" => new SquidKid(pos),
        _ => throw new ArgumentException($"no constructor mapped for {cls}")
    };
}
