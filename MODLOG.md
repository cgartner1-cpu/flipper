# MODLOG: Zagreus Valley

## Decisions (agreed with the user, 2026-10-06)
- Home game: Stardew Valley. Hades is required too (art source).
- Art: extracted at first launch from the player's own Hades install; nothing of Supergiant's ships.
- Pickups: god boon orbs, a boon lasts until the day ends.
- Solo for v0.1 (co-op possibly later).
- First playable: Zagreus player sprite, all 9 boons, Mines enemies + roaming spawns, 8 recast villagers.

## Route
- Stardew 1.6 = .NET 6 / MonoGame. Code via SMAPI (C#) + Harmony. Melty installs no loader for Stardew, so SMAPI
  (LGPL-3.0) is bundled in the release and launched as `{game}/StardewModdingAPI.exe`.
- Melty passes the Hades folder as env `ZAGREUSVALLEY_HADES_DIR={game:hades}`; cache in `{localappdata}/ZagreusValley`.
- Melty one_click_check on the draft recipe (no file entries): "One click: yes". With `entries` the
  validate/one_click tools returned "Something went wrong on Melty's side"; retry with the real build.

## Hades package format (from deppth, MIT)
- `Content/Win/Packages/<name>.pkg`: header [compressor 0x20=lz4][2 bytes][version 7], 32 MiB chunks, each
  [1 flag byte][int32 BE compressed length][LZ4 block]. Entries: 0xAD texture (name, int size, XNB bytes),
  0xBE end of chunk, 0xFF end of file. deppth's `stream.read(1) != 0` is always true, so every chunk is read as compressed.
- `<name>.pkg_manifest` (uncompressed, compressor 0x00): 0xDE atlas entries = named sub-rects into a texture.
- Textures are XNB v5/6 uncompressed: format 0 BGRA, 6 = BC3, 28 = BC7.
- Verified: a C# port round-trips a package + manifest written by deppth (BGRA path). BC3/BC7 still need real Hades files.

## Open (blocks a build/test)
1. art.json: 38 rows × (hades_package, hades_entries) are '?': need a Hades install to list atlas names.
2. No Stardew/Hades install in the cloud container: can't compile the SMAPI parts (needs game DLLs) or test.
3. Unverified API guesses to confirm in game: FarmerRenderer.draw parameter names and draw origin; monster
   constructors; vanilla monster frame sizes (enemies.vanilla_frame); BuffEffects field names.
