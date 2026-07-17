# Quickstart (no setup required beyond opening this repo in Cursor)

## You can say

- “Make a low poly tree”
- “Block out a simple house”
- “Add a wood crate kit prop”
- “Export a mesh tree in Blender if available”

The `sbox-asset-authoring` skill routes the request.

## What happens

1. Agent picks **kit** (default) / **mesh** / **place** / **look**
2. Updates `catalog/*.catalog.json`
3. Reuses the active game’s helpers, or copies `portable/SceneKit/`
4. Validates the catalog with `scripts/validate_catalog.py`

## You only need to touch s&box when

- Importing a GLB to `.vmdl` (mesh lane)
- Pressing Play to preview (until a look-dev project exists)

## Fresh project?

**No.** Not for this harness. Optional later for a look-dev-only boot scene.
