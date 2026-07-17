---
name: sbox-asset-authoring
description: >-
  Workspace pointer for s&box visual/asset authoring. Creates kit props, mesh
  export stubs, and catalog entries for landscapes, buildings, furniture, and
  creatures. Use when the user asks for scenery, low-poly assets, props, or
  environments in any s&box game in this repo.
---

# s&box asset authoring (workspace)

This repo’s harness lives at `tools/sbox-asset-harness/`.

**Prefer the full personal skill** at `~/.cursor/skills/sbox-asset-authoring/` (same name). If that is unavailable, follow this summary and read the harness README.

## Router

| Intent | Lane |
|--------|------|
| Stylized tree/house/furniture | **kit** — tinted primitives, catalog recipe |
| Organic hero mesh | **mesh** — Blender script → GLB → catalog `needs_import` |
| Dress a location with known assets | **place** — catalog only |
| Lighting/mood | **look** — presets only |

## Always

1. No invented `.vmdl` / `.vmat` paths
2. Update `tools/sbox-asset-harness/catalog/*.catalog.json`
3. Reuse game helpers or copy `tools/sbox-asset-harness/portable/SceneKit/`
4. Ground contact + ±1 depth nudges
5. Validate: `powershell -File tools/sbox-asset-harness/scripts/validate_catalog.ps1` (or the `.py` script if Python exists)

## Default look-dev project

Use **`scene_lab/`** for scenery iteration (`WorkbenchScene.cs` + `Code/Pieces/`). Do not put look-dev into full games unless exporting.
