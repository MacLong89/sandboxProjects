# Heights Hotel — Art Pipeline

## Generate sprites

```bash
cd tools/generate_sprites
pip install -r requirements.txt
python generate.py
```

Outputs land in `Assets/ui/sprites/` as RGBA PNGs plus `sprite_catalog.json`.

## Rules
- Generator is source of truth; re-run clears stale generated PNGs and writes the complete set deterministically.
- `tools/generate_sprites/source_art/` contains the generated city and logo source images used by the pipeline. The generator crops/resizes the city and removes the logo's source matte before export.
- Non-backdrop assets must have true transparent pixels (`A=0`). No checkerboards or color-key filler.
- The generator exits non-zero if a silhouette asset lacks an alpha channel or has no fully transparent pixels.
- Animated sheets are also audited frame-by-frame; declared frames must all be visually unique.
- Naming: `{entity}_{anim}.png` for sheets (e.g. `guest_a_walk.png`), `{entity}.png` for static.
- Catalog fields: `path`, `frameW`, `frameH`, `frames`, `fps`, `loop`, `pivot`.
- The set includes room interiors, level overlays, rooftop/elevator structure pieces, state bubbles, ambient effects, transaction feedback, character sheets, and HUD icons.

## Using sheets in UI
Animate with CSS `background-position` stepped by `frameW`, or a small animator that advances frames at `fps`. Always use nearest-neighbor scaling (`image-rendering: pixelated`).
