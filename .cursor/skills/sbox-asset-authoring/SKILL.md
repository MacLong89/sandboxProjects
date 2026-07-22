---
name: sbox-asset-authoring
description: >-
  Creates and places s&box visual content (low-poly trees, buildings, furniture,
  creatures, landscapes, props, materials, lighting) using a kit-first pipeline,
  optional Blender mesh export, and an asset catalog. Use when the user asks for
  scenery, props, environments, locations, low-poly assets, Blender exports for
  s&box, SceneKit, DecorDef-style kits, or when gameplay code is fine but visuals
  / landscapes / buildings / creatures are hard.
---

# s&box asset authoring

Game-agnostic pipeline for Facepunch s&box visuals. Prefer kits over freeform meshes. Prefer catalog paths over invented ones.

## Harness location

If the workspace contains `tools/sbox-asset-harness/`, treat it as the source of truth for templates, catalogs, Blender scripts, and portable C#. Read those files before inventing new patterns.

Personal skill home: `~/.cursor/skills/sbox-asset-authoring/`  
Repo harness: `tools/sbox-asset-harness/`

## Router (always decide first)

| User intent | Lane | Default action |
|-------------|------|----------------|
| Stylized tree / bush / crate / house / furniture / simple building | **kit** | Compose tinted box/sphere/plane parts in C# |
| Unique organic hero (creature sculpture, detailed trunk, Tripo import) | **mesh** | Blender/GLB → import checklist → catalog entry |
| Place/dress an existing area using known assets | **place** | Catalog + spawn data only; no new meshes |
| Mood / camera / lighting only | **look** | Lighting preset notes; do not invent geometry |

If unclear, choose **kit**. Say which lane you chose in one short sentence, then execute.

## Hard rules

1. **Never invent `.vmdl` / `.vmat` paths.** Only use paths present in the project catalog (`tools/sbox-asset-harness/catalog/*.json` or the active game’s catalog). If missing, create a catalog stub and use kit placeholders.
2. **Never silent-fail assets.** Missing model/material → log warning + obvious magenta/fallback. Prefer project `AssetSafe`-style helpers when present.
3. **Ground contact.** Box/sphere primitives are center-pivoted. Lift so AABB bottom sits on the ground (`LocalPosition.z = height * 0.5` for upright boxes).
4. **Coplanar flicker.** Nudge shared faces by about **±1 unit** (see DepthLayers pattern). Do not invent large floating Z bands.
5. **UV stretch.** Scaling `dev/box` with LocalScale stretches textures. Prefer solid/tint materials for kits, or apply the project’s UV-scale material fix if one exists.
6. **Publish safety.** `models/dev/*` may be editor-only. For shipping games, use project procedural cube helpers when they exist; otherwise note the publish risk.
7. **Do not port whole Three.js/Blender scenes into C#.** Use external tools for reference or single mesh assets; rebuild placement in s&box kits/catalog.
8. **Gameplay stays out of look-dev.** Prefer a thin spawn path / LevelViewer / bootstrap scene when available. Do not drag combat/networking into prop work.

## Lane: kit

**Chat-correcting every first pass is failure.** Read `scene_lab/docs/FIRST_PASS.md`.

1. Prefer `scene_lab`. Axis: `+X` forward, `+Z` up, wheels thin on **Y** via `KitParts.Wheel` only — never freehand-rotate wheels in a Piece.
2. Ratios in `PropSpecs`; assemblies in `KitParts`. Pieces only compose.
3. New part *type* → bake into `KitParts` once, freeze. Do not rediscover wheel/light transforms per asset.
4. Prefer user screenshot over long verbal defect lists.
5. Part budgets: street 5–12, utility 6–14, vehicle 18–35; kits that still fail the 2-second read → mesh lane (faster than polish-chat).
6. Place via `WorkbenchScene.cs` after the piece exists.

Study: `FIRST_PASS.md`, `docs/ref_sedan_goal.md`, `KitParts`, `PropSpecs`, `CarSedanPiece`.
When a user provides a goal image, match that silhouette feature-for-feature before declaring done.

## Lane: mesh

1. Run or adapt `tools/sbox-asset-harness/blender/export_lowpoly_prop.py` when Blender is available.
2. Export GLB to `tools/sbox-asset-harness/out/<prop_id>/` (create dirs as needed).
3. Fill an import checklist entry (origin at ground, meters→s&box scale note, upright axis, poly budget).
4. Add a **catalog** record with empty `vmdl` until the user imports in ModelDoc / asset browser; use kit placeholder in-game until `vmdl` is set.
5. Do not claim the mesh is in-game until a real content path exists.
6. **Textures:** JPEG → PNG before `.vmat` (see mesh-pipeline).
7. **Generated quadrupeds:** default to donor-free **scratch v6** —
   `tools/sbox-asset-harness/docs/donor-free-creature-rigging.md`,
   `docs/scratch-v6-reference-quality.md` (panther/stag quality bar),
   `docs/scratch-v6-species-builds.md`, `blender/species_v6_manifest.json`,
   `blender/rig_mesh_from_scratch_v6.py`, `scripts/build_species_v6.py`,
   `scripts/package_species_v6.py`. Sanitize the source, fit the landmark
   skeleton, verify snout and Head face `-Y`, apply limb/dorsum skinning
   passes from the reference doc, and author/inspect the six scratch actions.
   Source rotation is model-specific; never use a fixed “Tripo rotation” or
   optimize facing from tear score. Visual bar: carnivores ≈ panther, herbivores
   ≈ stag.
8. **Legacy donor animation libraries:** use the compatibility donor-rig section
   in [mesh-pipeline.md](mesh-pipeline.md) only when retaining an existing
   action library is a hard requirement and target/donor anatomy is close.

Read [mesh-pipeline.md](mesh-pipeline.md).

## Lane: place

1. Load catalog for the active game (or shared catalog).
2. Author placement as data (JSON or C# defs): position, yaw, scale/kind, palette.
3. Apply ground + depth rules.
4. Avoid new geometry unless the catalog lacks a required kit kind — then go to **kit**.

## Lane: look

1. Prefer existing lighting controllers in the game.
2. Otherwise document a small preset: sun color/angles, ambient/exposure notes, fog if used.
3. Do not add decorative meshes under “lighting” requests unless asked.

## Catalog contract

Catalog files live at:

- Shared drafts: `tools/sbox-asset-harness/catalog/shared.catalog.json`
- Per-game overlays (optional): `tools/sbox-asset-harness/catalog/<game>.catalog.json`

Schema and examples: [catalog.md](catalog.md).

When creating an asset, **always** update the appropriate catalog JSON in the same change.

## Validation

After writing files, run from repo root (prefer PowerShell on Windows if Python is missing):

```powershell
powershell -File tools/sbox-asset-harness/scripts/validate_catalog.ps1
```

```bash
python tools/sbox-asset-harness/scripts/validate_catalog.py
```

Fix reported errors before declaring done.

## Default look-dev project: `scene_lab`

When the workspace contains `scene_lab/`, **do all scenery iteration there** unless the user names another game.

| Edit | Path |
|------|------|
| Composition (road + props) | `scene_lab/Code/Scene/WorkbenchScene.cs` |
| Modular reusable pieces | `scene_lab/Code/Pieces/*Piece.cs` |
| Kit helpers | `scene_lab/Code/Kit/` |

Do **not** dump look-dev into under_pressure / full games. Export = copy a Piece file (+ Kit deps) into the target game later.

User preview: open `scene_lab/scene_lab.sbproj` → play `scenes/workbench.scene` → **R** rebuilds.

## Done criteria

- Lane chosen and followed
- Catalog updated
- Helpers reused or portable stubs added
- No invented asset paths
- Ground + depth rules applied
- User told exactly what to open/spawn to see it (scene/object name)
