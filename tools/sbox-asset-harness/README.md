# s&box asset harness

Game-agnostic tooling for kit props, mesh exports, and catalogs used with the Cursor skill `sbox-asset-authoring`.

## Layout

| Path | Purpose |
|------|---------|
| `catalog/` | Shared + per-game asset catalogs (JSON) |
| `blender/` | Headless low-poly exporters |
| `portable/SceneKit/` | Drop-in C# stubs for games that lack kit helpers |
| `scripts/` | Catalog validation + shared asset consolidation |
| `templates/` | Copy-paste starters |
| `out/` | Generated GLB / exports (gitignored contents) |

## Shared asset library

Reusable models / materials / sounds / textures from every game are consolidated into repo-root **`shared_assets/`** (see that folder’s README).

Refresh after adding content in a game:

```powershell
powershell -File tools/sbox-asset-harness/scripts/consolidate_shared_assets.ps1
```

`Assets/` and `source/` under `shared_assets/` are generated (gitignored). Re-run the script on a fresh clone.

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

## Creature rigs and animations

For new generated quadrupeds, use the donor-free landmark rig and procedural
animation pipeline:

- `blender/rig_mesh_from_scratch_v4.py` — reference rig/action authoring;
- `blender/rig_mesh_from_scratch_v5.py` — reusable motion profiles and
  wolf/panther landmark profiles;
- `blender/rig_mesh_from_scratch_v6.py` — manifest-driven, species-aware
  donor-free rigging and six procedural actions;
- `blender/species_v6_manifest.json` — unique-source inventory, exclusions,
  source hashes, animal-family movement profiles, and build settings;
- `blender/qa_scratch_v6.py` — runtime motion and evaluated-geometry QA;
- `scripts/build_species_v6.py` — reproducible parallel build/QA runner;
- `scripts/package_species_v6.py` — s&box VMDL/VMAT/catalog installer;
- `blender/measure_deformation.py` — automated edge deformation QA.

The canonical workflow, acceptance gates, failure modes, and packaging
checklist are in
[`docs/donor-free-creature-rigging.md`](docs/donor-free-creature-rigging.md).
The historical results and design conclusions are in
[`docs/rigging-animation-audit.md`](docs/rigging-animation-audit.md).
The 14-animal scratch-v6 inventory, results, exclusions, rejected experiments,
and review order are in
[`docs/scratch-v6-species-builds.md`](docs/scratch-v6-species-builds.md).

### Legacy donor retargeting

Donor animals live in repo-root `tools/FBX/` (shared `AnimalArmature` + takes).

| Script | Purpose |
|--------|---------|
| `blender/inspect_fbx.py` | Inspect meshes / bones / actions |
| `blender/rig_mesh_from_donor.py` | Fit unrigged mesh to donor, skin, export |
| `blender/measure_deformation.py` | Headless tear score for iteration |

Full compatibility pipeline and VMDL rules:
[`docs/mesh-pipeline.md`](docs/mesh-pipeline.md) (legacy donor-rig section).

Quick wolf re-rig (Blender 5.2):

```powershell
$b = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
& $b --background --factory-startup --python tools\sbox-asset-harness\blender\rig_mesh_from_donor.py -- `
  --donor tools\FBX\Wolf.fbx `
  --target scene_lab\Assets\models\tripo_wolf_test\tripo_wolf_test.fbx `
  --weight-method automatic --target-z-rotation -90 `
  --out-fbx scene_lab\Assets\models\tripo_wolf_test\tripo_wolf_test_rigged.fbx `
  --out-blend tools\sbox-asset-harness\out\tripo_wolf_rigged\tripo_wolf_rigged.blend `
  --preview-dir tools\sbox-asset-harness\out\tripo_wolf_rigged\previews
```

**Facing:** runtime head is `-Y`, but source rotation is model-specific. Verify
both the mesh snout and Head bone; never choose a rotation from tear score alone.