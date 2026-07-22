# Mesh pipeline (Blender → s&box)

Use when kits cannot carry the asset (organic trunks, creatures, hero props).

## Defaults

- **Poly budget (low-poly):** props ≤ 500 tris, creatures ≤ 2k unless asked
- **Origin:** at ground contact, object upright, +Z up in Blender export (apply rotation/scale)
- **Scale:** model roughly 1.0 = 1 meter in Blender; note target in-game height in catalog `targetHeight`
- **Materials:** few slots (bark/leaf, body/accent); avoid complex node graphs for first pass
- **Export:** GLB to `tools/sbox-asset-harness/out/<prop_id>/model.glb`

## Blender script (static props)

From repo root (Blender on PATH):

```bash
blender --background --python tools/sbox-asset-harness/blender/export_lowpoly_prop.py -- --id tree_pine_01 --kind tree --height 4.0
```

If Blender is missing, still:

1. Add catalog entry with `lane: "mesh"`, `status: "blocked_no_blender"`
2. Provide kit placeholder so gameplay/look-dev can proceed
3. Tell user the exact Blender command to run when available

## After GLB exists (user or agent)

s&box import is editor-side (ModelDoc / asset browser). Agent checklist:

1. Record intended path: `models/<game_or_shared>/props/<prop_id>.vmdl`
2. Catalog: set `vmdl` when the file exists in the game’s Assets; else leave null
3. In code: load via safe helper; fallback to kit until path resolves
4. Verify upright axis and ground pivot in look-dev

## Textures for ModelDoc materials

s&box’s resource compiler does **not** reliably ingest JPEG for `.vmat` color/normal maps.

1. Extract textures from Tripo/GLB/FBX (or keep source maps beside the mesh)
2. Convert every `*.jpeg` / `*.jpg` map → `*.png` (Pillow is fine)
3. Author `.vmat` with `shaders/complex.shader` pointing at the PNGs
4. Delete leftover JPEGs so nothing re-references them
5. Packed metal/rough maps need channel splits before wiring; leave `default_rough.tga` until then

## Creature pipelines

Use the donor-free pipeline by default for generated quadrupeds when we control
the action set:

- [Donor-free creature rigging and procedural animation](donor-free-creature-rigging.md)
- mesh-fitted 23-bone runtime rig;
- no retained source/donor skeleton, weights, constraints, or actions;
- procedural Idle, Walk, Gallop, Attack, and Death;
- validated on both wolf and panther geometry.

Use donor retargeting only when an existing animation library is a hard
requirement and its skeleton/rest pose is close enough to the target mesh.

## Legacy creature donor-rig pipeline (unrigged mesh → donor animations)

Use when you have an **unrigged** mesh (e.g. Tripo) and a **donor FBX** from `tools/FBX/` that already has `AnimalArmature` + animation takes (Wolf, Fox, Husky, Deer, Horse, …).

### Hard rules learned the hard way

1. **Facing beats tear score.** Runtime quadrupeds face **-Y**, but the required
   source rotation is model-specific. The old donor test mesh used `-90°`; the
   later scratch wolf used `+90°`; the panther used `0°`. Verify both the mesh
   snout and Head bone instead of inferring facing from a preset or tear score.
2. **Mesh FBX ≠ anim FBX.** Export skinned mesh + bind pose only (`bake_anim=False`). Point VMDL `AnimFile` entries at the **original donor** FBX (copy into Assets, e.g. `wolf_donor_anims.fbx`). Blender-rebaked takes often list in ModelDoc but do not drive the mesh.
3. **Import filter must match the exported mesh name.** Use a unique name like `tripo_wolf` (not `Wolf` — Blender may emit `Wolf.001`). VMDL `exception_list` must match exactly.
4. **Heat weights skip spine on dissimilar meshes.** Mid-body often binds to `BackUpperLeg` → V-pinch. Heuristic spine rewrites can raise tear score; treat them as experimental. Next real fix: fit skeleton into mesh (not mesh into skeleton AABB only).
5. **Never invent `.vmdl` / `.vmat` paths.** Catalog first.

### Scripts

| Script | Role |
|--------|------|
| `blender/inspect_fbx.py` | Meshes, bones, actions, bounds |
| `blender/rig_mesh_from_donor.py` | Fit + auto/transfer weights + export skinned FBX + previews |
| `blender/measure_deformation.py` | Headless tear score across all actions (iterate without screenshots) |

Blender path on this machine: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`

### One-shot commands (wolf example)

```powershell
$blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
$rig = "tools\sbox-asset-harness\blender\rig_mesh_from_donor.py"
$measure = "tools\sbox-asset-harness\blender\measure_deformation.py"

# 1) Copy donor anims into the game Assets folder (once)
copy /Y tools\FBX\Wolf.fbx scene_lab\Assets\models\tripo_wolf_test\wolf_donor_anims.fbx

# 2) Rig Tripo mesh onto donor skeleton (facing -90)
& $blender --background --factory-startup --python $rig -- `
  --donor tools\FBX\Wolf.fbx `
  --target scene_lab\Assets\models\tripo_wolf_test\tripo_wolf_test.fbx `
  --weight-method automatic `
  --target-z-rotation -90 `
  --out-fbx scene_lab\Assets\models\tripo_wolf_test\tripo_wolf_test_rigged.fbx `
  --out-blend tools\sbox-asset-harness\out\tripo_wolf_rigged\tripo_wolf_rigged.blend `
  --preview-dir tools\sbox-asset-harness\out\tripo_wolf_rigged\previews

# 3) Tear metric (target: lower is better; donor reference ~95; Tripo first pass ~500+)
& $blender --background tools\sbox-asset-harness\out\tripo_wolf_rigged\tripo_wolf_rigged.blend `
  --python $measure -- `
  --out tools\sbox-asset-harness\out\tripo_wolf_rigged\deformation_report.json --frames 6
```

Confirm the JSON report includes `"orientation": { "facingOk": true }`. For
new work this must be a mesh-and-bones check; a bones-only report can pass while
the skinned mesh faces backward.

### VMDL shape (matches thorns animal models)

- `RenderMeshFile` → `..._rigged.fbx` with `exclude_by_default = true`, `exception_list = [ "tripo_wolf" ]`
- `AnimationList` / `AnimFile` → `source_filename` = donor anim FBX, `take` indices matching donor action order (Attack=0, Death=1, … Walk=11 for Wolf)
- `DefaultMaterialGroup` → PNG-based `.vmat`

### Iteration loop (no screenshot spam)

1. Change rig script / rotation / weights
2. Re-export FBX
3. Run `measure_deformation.py` → read `TEAR_SCORE` + `TORN_BY_BONE`
4. Only ask user to open ModelDoc when score improves **and** `facingOk` is true
5. User validates Idle/Walk first; extreme takes (Gallop_Jump) tear more on dissimilar meshes

### Known ceiling

Automatic bind of a Tripo creature onto a stylized donor quadruped reaches ~“usable Idle/Walk” quality. Clean extreme poses usually need either:

- **Fit skeleton to mesh** (move hip/spine/tail rest bones into the Tripo silhouette, keep bone names), or
- A short manual weight-paint pass on `out/<id>/*.blend`

The completed experiments showed a stronger option when donor actions are not
required: build the runtime skeleton from mesh landmarks and author restrained
procedural motion. See the donor-free guide above.

### Catalog

Mark entry `lane: "mesh"`, set `vmdl` / `vmat`, note donor FBX + tear score in `notes`.

## Do not

- Spend tokens porting multi-object Blender scenes into GameObject trees by hand
- Claim `.vmdl` exists when only GLB is on disk
- Add networking/combat dependencies to mesh preview
- Optimize tear score by flipping the animal front-to-back
- Rebake donor animations through Blender and expect ModelDoc to drive the mesh
