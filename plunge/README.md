# PLUNGE

Standalone s&box project for a cozy incremental diving game with detailed
Stardew/Terraria-style art. This is the consolidated survivor of the
`deep` → `deep_dive` → `plunge` line; the retired projects' art lives on
under `ArtSource/`.

## Open

Project path: `C:\Users\Macra\Projects\sandboxProjects\plunge`

Open `plunge.sbproj` in the s&box editor, then play `Assets/scenes/plunge.scene`.

## Core loop

1. Dive with basic gear.
2. Touch fish, artifacts, crates, and chests to fill cargo.
3. Fight jellyfish and sharks with the knife or harpoon.
4. Surface with `R`; the expedition is logged and cargo is sold automatically.
5. Buy gear, reach level 5, unlock a submarine, and descend into deeper biomes.

The early economy is tuned so the first useful tank/flipper purchase follows the
first successful dive. The first submarine target is reachable in roughly four
short expeditions.

## Controls

- `WASD` / left stick — swim
- `Shift` / gamepad A — boost
- Mouse 1 / right trigger — use selected weapon
- `1`, `2`, `3` — harpoon, knife, light
- `F` — toggle light
- `C` — photograph nearby rare creatures
- `R` — surface and settle the dive

## Art pipeline

`tools/import_real_art.py` builds every runtime PNG in `Assets/` from the
source art in `ArtSource/`:

- `ArtSource/deep_textures/` — high-detail standalone sprites salvaged from
  the retired `deep` project
- `ArtSource/generated/` — magenta-keyed atlases from the retired `deep_dive`
  project (sliced + keyed by the script)
- `ArtSource/generated_agent/` — generated fills for sprites the old projects
  lacked (shark, drone, crystal, crate, some icons, reef/abyss backgrounds)

Animated actors (`diver`, `fish_*`, `jelly`, `shark`, `sub`) get a synthesized
4-frame bob loop from a single pose. Rerun the script after changing anything
in `ArtSource/`.
