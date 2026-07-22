# Shared Assets (`maclgames.shared_assets`)

Single pull-from folder for reusable s&box content across games in this repo.

**Existing games are untouched.** Their `Assets/` folders still own what they ship. This library is the consolidation point for new games and look-dev.

## Layout

| Path | Purpose |
|------|---------|
| `Assets/` | Deduped union of models, materials, textures, sounds, UI, shaders, etc. |
| `source/fbx/` | Shared donor / animal FBX from `tools/FBX/` |
| `source/tripo/` | Tripo staging exports from `tools/tripo_output/` |
| `inventory.json` | Every file’s chosen source game + other games that had the same path |
| `shared_assets.sbproj` | s&box **library** package (`Org`: maclgames, `Ident`: shared_assets) |

Skipped on purpose (game-specific): `scenes/`, `map/`.

When the same relative path existed in multiple games, the winner follows priority: `terraingen` → `thorns` → `fauna2` → … (see inventory).

## Pull into a new game

### Option A — copy (simplest)

Copy only what you need:

```powershell
# Example: foliage + kit materials into a new project
Copy-Item shared_assets\Assets\models\foliage2 new_game\Assets\models\foliage2 -Recurse
Copy-Item shared_assets\Assets\materials\kit_* new_game\Assets\materials\ -ErrorAction SilentlyContinue
```

Or mirror the whole tree:

```powershell
Copy-Item shared_assets\Assets\* new_game\Assets\ -Recurse -Force
```

### Option B — local library reference

From the new game folder:

```powershell
New-Item -ItemType Directory -Force -Path Libraries | Out-Null
cmd /c mklink /J Libraries\maclgames.shared_assets ..\shared_assets
```

Then open the game in s&box so the library mounts. Prefer Option A until you are ready to treat this as a real package.

### Option C — package reference

After publishing `maclgames.shared_assets` to asset.party, add to the game `.sbproj`:

```json
"PackageReferences": [
  "maclgames.shared_assets"
]
```

## Refresh after adding assets in a game

```powershell
powershell -File tools/sbox-asset-harness/scripts/consolidate_shared_assets.ps1
```

Safe to re-run; it overwrites `shared_assets/Assets` / `source/` and regenerates `inventory.json`.

`Assets/` and `source/` are **generated** (gitignored) so the repo does not store a second copy of ~1.5 GB of binaries. After clone, run the script once to rebuild the library from each game’s `Assets/`.

## Notes

- Paths inside `Assets/` match the original game-relative paths (`models/wolf/wolf.vmdl`, etc.).
- Compiled `*_c` companions are included so copies work without an immediate editor recompile.
- Third-party content under `*/Libraries/` is not included.
- Catalog IDs for kits still live in `tools/sbox-asset-harness/catalog/`; point `vmdl`/`vmat` at paths under this library once you import them into a game.
