# PLUNGE

Standalone s&box project for a cozy, pixel-art incremental diving game.

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

`tools/generate_pixel_art.py` generates every game sprite and animation frame.
All actor, prop, icon, and particle PNGs are RGBA and normalize fully transparent
pixels to `(0, 0, 0, 0)`. Backgrounds are intentionally opaque.
