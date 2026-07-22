# Catalog contract

JSON catalogs describe what the agent may load or spawn.

## File locations

- `tools/sbox-asset-harness/catalog/shared.catalog.json` — cross-game drafts / shared IDs
- `tools/sbox-asset-harness/catalog/<game>.catalog.json` — optional per-game overlay (`under_pressure`, `aimbox`, …)

Games may also keep an in-code catalog; when both exist, **update JSON first**, then mirror into C# if that game uses code defs.

## Schema (one entry)

```json
{
  "id": "tree_pine_01",
  "games": ["*"],
  "lane": "kit",
  "kind": "tree",
  "title": "Low poly pine",
  "status": "ready",
  "vmdl": null,
  "vmat": null,
  "kit": {
    "recipe": "tree",
    "targetHeight": 220,
    "colors": {
      "trunk": "#9E6124",
      "leafA": "#61DB14",
      "leafB": "#47C70F"
    }
  },
  "tags": ["outdoor", "vegetation"],
  "notes": ""
}
```

### Fields

| Field | Meaning |
|-------|---------|
| `id` | Stable snake_case id |
| `games` | `["*"]` or list of folder names |
| `lane` | `kit` \| `mesh` \| `place` |
| `kind` | tree, bush, house, furniture, creature, prop, … |
| `status` | `ready` \| `placeholder` \| `blocked_no_blender` \| `needs_import` |
| `vmdl` / `vmat` | Real content paths or `null` |
| `kit.recipe` | Key into kit-recipes.md |
| `kit.targetHeight` | In **that game’s units** when known; else meters with a note |

## Agent rules

- New asset ⇒ new or updated entry in the same PR/change
- Never reference a `vmdl` not listed as non-null here (or verified on disk under the game’s Assets)
- Prefer extending `kind` + recipe over one-off undocumented builders
