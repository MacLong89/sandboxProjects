# s&box asset harness

Game-agnostic tooling for kit props, mesh exports, and catalogs used with the Cursor skill `sbox-asset-authoring`.

## Layout

| Path | Purpose |
|------|---------|
| `catalog/` | Shared + per-game asset catalogs (JSON) |
| `blender/` | Headless low-poly exporters |
| `portable/SceneKit/` | Drop-in C# stubs for games that lack kit helpers |
| `scripts/` | Catalog validation |
| `templates/` | Copy-paste starters |
| `out/` | Generated GLB / exports (gitignored contents) |

## Cursor skill

Personal skill (all projects): `~/.cursor/skills/sbox-asset-authoring/`  
Workspace pointer: `.cursor/skills/sbox-asset-authoring/`

Ask in chat: “make a low poly tree” — the skill routes to kit vs mesh vs place.

## Look-dev game: `scene_lab/`

Standalone s&box project for iterative scenery. Open `scene_lab/scene_lab.sbproj`, play `scenes/workbench.scene`.

- Composition: `scene_lab/Code/Scene/WorkbenchScene.cs`
- Modular pieces: `scene_lab/Code/Pieces/`
- Export: copy piece files into other games when ready

## Validate

```bash
python tools/sbox-asset-harness/scripts/validate_catalog.py
```

## Blender export (optional)

```bash
blender --background --python tools/sbox-asset-harness/blender/export_lowpoly_prop.py -- --id tree_pine_01 --kind tree --height 4.0
```
