using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using ZagreusValley.Generated;

namespace ZagreusValley.HadesArt;

/// <summary>sys_hades_locate + sys_art_extract. Finds the player's Hades install, cuts the art.json frames out of its
/// packages, fits them to layouts.json and caches PNGs under the local data folder. Nothing from Hades ships with
/// the mod. A row that can't be produced returns null, and the game keeps its vanilla art for it.</summary>
public sealed class ArtCache
{
    const int CacheVersion = 1;
    readonly IModHelper helper;
    readonly IMonitor log;
    readonly string cacheDir;
    readonly Dictionary<string, Texture2D?> loaded = new();
    Task<Dictionary<string, (int W, int H, byte[] Rgba)>>? pending;

    public string? HadesDir { get; }

    public ArtCache(IModHelper helper, IMonitor log)
    {
        this.helper = helper; this.log = log;
        cacheDir = Environment.GetEnvironmentVariable("ZAGREUSVALLEY_CACHE") is { Length: > 0 } c
            ? c : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZagreusValley");
        cacheDir = Path.Combine(cacheDir, $"art-v{CacheVersion}");
        HadesDir = Locate();
        log.Log(HadesDir is null ? "Hades install not found; Hades art stays off." : $"Hades install: {HadesDir}", LogLevel.Info);
    }

    static string? Locate()
    {
        var candidates = new List<string?> { Environment.GetEnvironmentVariable("ZAGREUSVALLEY_HADES_DIR") };
        foreach (var lib in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam", @"D:\SteamLibrary", @"E:\SteamLibrary" })
            candidates.Add(Path.Combine(lib, "steamapps", "common", "Hades"));
        candidates.Add(@"C:\Program Files\Epic Games\Hades");
        return candidates.FirstOrDefault(d => d is { Length: > 0 } && Directory.Exists(Path.Combine(d, "Content", "Win", "Packages")));
    }

    public void EnsureExtracted(bool force = false)
    {
        if (HadesDir is null) return;
        if (force && Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        loaded.Clear();
        var todo = ArtSheet.Rows.Where(r => r.HadesPackage != "?" && !File.Exists(CachePath(r.Id))).ToList();
        if (todo.Count == 0) return;
        log.Log($"Extracting {todo.Count} images from Hades (first launch only)...", LogLevel.Info);
        pending = Task.Run(() => Extract(todo));
        helper.Events.GameLoop.UpdateTicked += FinishWhenReady;
    }

    void FinishWhenReady(object? s, StardewModdingAPI.Events.UpdateTickedEventArgs e)
    {
        if (pending is not { IsCompleted: true }) return;
        helper.Events.GameLoop.UpdateTicked -= FinishWhenReady;
        if (pending.IsFaulted) { log.Log($"Hades art extraction failed: {pending.Exception}", LogLevel.Error); return; }
        Directory.CreateDirectory(cacheDir);
        foreach (var (id, img) in pending.Result)          // Texture2D must be made on the game thread
        {
            var tex = new Texture2D(StardewValley.Game1.graphics.GraphicsDevice, img.W, img.H);
            tex.SetData(img.Rgba);
            using (var f = File.Create(CachePath(id))) tex.SaveAsPng(f, img.W, img.H);
            loaded[id] = tex;
        }
        log.Log($"Hades art ready ({pending.Result.Count} images).", LogLevel.Info);
        helper.GameContent.InvalidateCache(a => a.Name.StartsWith("Characters") || a.Name.StartsWith("Portraits"));
    }

    Dictionary<string, (int, int, byte[])> Extract(List<ArtRow> rows)
    {
        var outp = new Dictionary<string, (int, int, byte[])>();
        foreach (var byPkg in rows.GroupBy(r => r.HadesPackage))
        {
            var pkg = PkgReader.Read(Path.Combine(HadesDir!, "Content", "Win", "Packages", byPkg.Key + ".pkg"));
            var decoded = new Dictionary<string, (int W, int H, byte[] Rgba)>();
            var subs = pkg.Atlases.SelectMany(a => a.Subs.Select(s => (a.TextureName, s)))
                                  .GroupBy(x => x.s.Name).ToDictionary(g => g.Key, g => g.First());
            foreach (var row in byPkg)
            {
                var layout = LayoutsSheet.Rows.First(l => l.Id == row.TargetLayout);
                var frames = new List<(int, int, byte[])>();
                foreach (var name in row.HadesEntries.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!subs.TryGetValue(name, out var hit)) { log.Log($"{row.Id}: '{name}' not in {byPkg.Key}", LogLevel.Warn); continue; }
                    if (!decoded.TryGetValue(hit.TextureName, out var sheet))
                        decoded[hit.TextureName] = sheet = XnbTexture.Decode(pkg.Textures[hit.TextureName]);
                    frames.Add(Fit.Crop(sheet, hit.s.X, hit.s.Y, hit.s.W, hit.s.H));
                }
                if (frames.Count > 0) outp[row.Id] = Fit.ToGrid(frames, layout);
            }
        }
        return outp;
    }

    string CachePath(string id) => Path.Combine(cacheDir, id + ".png");

    /// <summary>The converted texture for an art.json row, or null if it isn't available.</summary>
    public Texture2D? Get(string artId)
    {
        if (loaded.TryGetValue(artId, out var t)) return t;
        var p = CachePath(artId);
        t = File.Exists(p) ? Texture2D.FromFile(StardewValley.Game1.graphics.GraphicsDevice, p) : null;
        return loaded[artId] = t;
    }
}
