# Zagreus Valley

A Stardew Valley × Hades mashup: play Stardew Valley as Zagreus.

- **You are Zagreus.** Your farmer is drawn as Zagreus.
- **The valley is recast.** Lewis→Hades, Robin→Nyx, Clint→Dusa, Pierre→Charon, Abigail→Megaera, Sebastian→Thanatos, Emily→Persephone, Gus→Hypnos (looks and names; dialogue stays Stardew's in v0.1).
- **God boons.** Glowing boon orbs appear around the valley and in the Mines. Walk over one for a boon until the day ends: Hermes (speed), Ares (damage), Athena (defense), Artemis (crits), Poseidon (knockback), Zeus (chain lightning), Dionysus (damage over time), Aphrodite (weakened enemies), Demeter (faster crops).
- **Hades enemies.** The Mines fill with Numbskulls, Wretches, Bloodless, Gorgons, Exalted and more, and some wander into the valley at night. They use Stardew monster behavior with Hades looks and tuned stats.
- **Single player.**

**Requires** Stardew Valley 1.6.14+ and Hades (Steam or Epic). The mod reads all Hades art from *your own* Hades install on first launch; no Supergiant art is included in the download.

## Status

Source written, not yet built or tested in the game. See `MODLOG.md`.

## How it is built

- `sheets/*.json` is the design and the source of truth: one row per thing, one column per property.
- `python3 tools/preflight.py` lists every unfilled cell, unresolved cross-sheet reference and unverified row. Build only when it's clean.
- `python3 tools/gen.py` turns each row into a C# record in `src/ZagreusValley/Generated/Sheets.g.cs`.
- `src/ZagreusValley` is the SMAPI mod. Each class names the `systems.json` row(s) it implements.

Test commands in the SMAPI console: `zag_orb <god>`, `zag_spawn <enemy id>`, `zag_reextract`.
