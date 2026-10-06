#!/usr/bin/env python3
"""Preflight: overlay every sheet, list unfilled/unverified cells and cross-sheet references that don't resolve.
Exit 0 only when clean. Usage: python3 tools/preflight.py [--allow-unverified]"""
import json, sys, pathlib

SHEETS = pathlib.Path(__file__).resolve().parent.parent / "sheets"
sheets = {p.stem: json.loads(p.read_text()) for p in sorted(SHEETS.glob("*.json"))}
ids = {s: {r["id"] for r in d["rows"]} for s, d in sheets.items()}
all_ids = set().union(*ids.values()) | {"player", "any", "-"}

# column -> sheet(s) its values must reference
REFS = {
    ("villagers", "hades_sprite_art"): "art", ("villagers", "hades_portrait_art"): "art",
    ("enemies", "hades_art"): "art", ("boons", "hook"): "systems", ("art", "target_layout"): "layouts",
}
problems, unverified = [], []
for s, d in sheets.items():
    cols = d["columns"]
    for r in d["rows"]:
        for c in cols:
            v = r.get(c)
            if v is None or v == "" or v == "?":
                problems.append(f"{s}.{r['id']}.{c}: unfilled")
        for c in r:
            if c not in cols:
                problems.append(f"{s}.{r['id']}.{c}: column not declared")
        if r.get("verified") is not True:
            unverified.append(f"{s}.{r['id']}")
        for (rs, rc), target in REFS.items():
            if rs == s and r.get(rc) not in ("-", None) and r[rc] not in ids[target]:
                problems.append(f"{s}.{r['id']}.{rc}: '{r[rc]}' not in {target}")
        if s == "art":
            for ref in str(r["used_by"]).split(","):
                if ref not in all_ids:
                    problems.append(f"art.{r['id']}.used_by: '{ref}' does not resolve")
        if s == "spawns" and r["what"] != "any" and r["what"] not in ids["boons"] | ids["enemies"]:
            problems.append(f"spawns.{r['id']}.what: '{r['what']}' does not resolve")
        if s == "systems":
            for ref in r["implements"].split(","):
                if ref not in all_ids and ref not in sheets:
                    problems.append(f"systems.{r['id']}.implements: '{ref}' does not resolve")
        if s == "boons" and (r["effect_kind"] == "hook") == (r["hook"] == "-"):
            problems.append(f"boons.{r['id']}: effect_kind/hook mismatch")

# every art row must be used; every boon hook must exist
used = {r[c] for d in (sheets["villagers"], sheets["enemies"]) for r in d["rows"] for c in r if c.startswith("hades_") and c.endswith("art")}
for r in sheets["art"]["rows"]:
    if r["used_by"].startswith(("v_", "e_")) and r["id"] not in used:
        problems.append(f"art.{r['id']}: not referenced by its used_by row")

print(f"{sum(len(d['rows']) for d in sheets.values())} rows in {len(sheets)} sheets")
print(f"\n{len(problems)} blocking problems:"); [print("  " + p) for p in problems]
print(f"\n{len(unverified)} rows not yet verified in the running game")
sys.exit(1 if problems or (unverified and "--allow-unverified" not in sys.argv) else 0)
