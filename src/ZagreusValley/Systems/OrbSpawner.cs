using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using ZagreusValley.Generated;
using ZagreusValley.HadesArt;

namespace ZagreusValley.Systems;

/// <summary>sys_orb_spawn: places boon orbs per spawns.json, draws them, and grants the boon when Zagreus walks over one.</summary>
public sealed class OrbSpawner
{
    sealed record Orb(string Location, Vector2 Tile, BoonRow Boon);

    readonly IModHelper helper;
    readonly BoonSystem boons;
    readonly ArtCache art;
    readonly List<Orb> orbs = new();
    readonly Dictionary<string, int> placedToday = new();
    readonly Random rng = new();

    public OrbSpawner(IModHelper helper, BoonSystem boons, ArtCache art) { this.helper = helper; this.boons = boons; this.art = art; }

    public void Register()
    {
        helper.Events.GameLoop.DayStarted += (_, _) => { orbs.Clear(); placedToday.Clear(); RollFor(Game1.currentLocation); };
        helper.Events.Player.Warped += (_, e) => { if (e.IsLocalPlayer) RollFor(e.NewLocation); };
        helper.Events.GameLoop.UpdateTicked += (_, _) => CheckPickup();
        helper.Events.Display.RenderedWorld += (_, e) => Draw(e.SpriteBatch);
    }

    static string Key(GameLocation l) => l is MineShaft ? "MineShaft" : l.Name;

    void RollFor(GameLocation loc)
    {
        if (loc is null) return;
        foreach (var row in SpawnsSheet.Rows.Where(r => r.Kind == "orb" && r.Location == Key(loc)))
        {
            placedToday.TryGetValue(row.Id, out int n);
            if (n >= row.MaxPerDay || !InWindow(row.TimeWindow) || rng.NextDouble() > row.Chance) continue;
            if (orbs.Any(o => o.Location == loc.NameOrUniqueName)) continue;   // one orb per map at a time
            if (FindTile(loc) is not { } tile) continue;
            orbs.Add(new Orb(loc.NameOrUniqueName, tile, Pick(row.What)));
            placedToday[row.Id] = n + 1;
        }
    }

    public void PlaceNear(Farmer who, BoonRow boon)
    {
        var tile = who.Tile + new Vector2(who.FacingDirection == 1 ? 2 : who.FacingDirection == 3 ? -2 : 0, who.FacingDirection == 2 ? 2 : who.FacingDirection == 0 ? -2 : 0);
        orbs.Add(new Orb(who.currentLocation.NameOrUniqueName, tile, boon));
    }

    BoonRow Pick(string what)
    {
        if (what != "any") return BoonsSheet.Rows.First(b => b.Id == what);
        int total = BoonsSheet.Rows.Sum(b => b.SpawnWeight), roll = rng.Next(total);
        foreach (var b in BoonsSheet.Rows) { roll -= b.SpawnWeight; if (roll < 0) return b; }
        return BoonsSheet.Rows[0];
    }

    Vector2? FindTile(GameLocation loc)
    {
        for (int i = 0; i < 60; i++)
        {
            var t = loc.getRandomTile();
            if (loc.CanItemBePlacedHere(t) && loc.isTilePassable(t)) return t;
        }
        return null;
    }

    static bool InWindow(string window)
    {
        var p = window.Split('-');
        int t = Game1.timeOfDay;
        return t >= int.Parse(p[0], CultureInfo.InvariantCulture) && t <= int.Parse(p[1], CultureInfo.InvariantCulture);
    }

    void CheckPickup()
    {
        if (!Context.IsPlayerFree) return;
        var box = Game1.player.GetBoundingBox();
        var hit = orbs.FirstOrDefault(o => o.Location == Game1.currentLocation.NameOrUniqueName
            && box.Intersects(new Rectangle((int)o.Tile.X * 64 + 8, (int)o.Tile.Y * 64 + 8, 48, 48)));
        if (hit is null) return;
        orbs.Remove(hit);
        boons.Grant(hit.Boon);
    }

    void Draw(SpriteBatch b)
    {
        if (Game1.currentLocation is null) return;
        var tex = art.Get("art_boon_orb");
        float pulse = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalSeconds * 4) * 4f;
        foreach (var o in orbs.Where(o => o.Location == Game1.currentLocation.NameOrUniqueName))
        {
            var color = Hex(o.Boon.OrbColor);
            var pos = Game1.GlobalToLocal(Game1.viewport, o.Tile * 64 + new Vector2(32, 24 + pulse));
            if (tex is not null)
            {
                int frame = (int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 150) % 4;
                b.Draw(tex, pos, new Rectangle(frame * 16, 0, 16, 16), color, 0f, new Vector2(8, 8), 4f, SpriteEffects.None, 1f);
            }
            else   // placeholder until Hades art is extracted: a glowing square in the god's colour
            {
                b.Draw(Game1.staminaRect, new Rectangle((int)pos.X - 20, (int)pos.Y - 20, 40, 40), color * 0.45f);
                b.Draw(Game1.staminaRect, new Rectangle((int)pos.X - 12, (int)pos.Y - 12, 24, 24), color);
            }
        }
    }

    static Color Hex(string hex)
    {
        uint v = uint.Parse(hex.TrimStart('#'), NumberStyles.HexNumber);
        return new Color((int)(v >> 16) & 0xFF, (int)(v >> 8) & 0xFF, (int)v & 0xFF);
    }
}
