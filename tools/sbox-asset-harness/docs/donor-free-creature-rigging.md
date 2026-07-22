# Donor-free creature rigging and procedural animation

Canonical workflow for turning a quadruped mesh into an animated s&box VMDL
without retaining or retargeting any source skeleton or animation.

This document is the default for new generated/Tripo-style quadrupeds. The
older donor-retarget pipeline remains available as a compatibility fallback in
[`mesh-pipeline.md`](mesh-pipeline.md), but it is not the preferred approach
when we control the animation set.

## Why this is now the default

The donor pipeline repeatedly fought differences in:

- rest-pose bone lengths and joint locations;
- limb count (especially paw/toe splits);
- spine and tail proportions;
- animation amplitude;
- source mesh facing and object transforms;
- heat-weight regions near shoulders, flanks, and the rear spine.

A small mesh-fitted skeleton plus restrained procedural motion produced more
stable results on both the wolf and panther. It also made every behavior
explicit and reproducible instead of inheriting unexplained movement from a
donor take.

## Current scripts

| Script | Role |
|---|---|
| `blender/rig_mesh_from_scratch_v4.py` | Reference implementation: landmark rig, skin, Jaw, five procedural actions, exports |
| `blender/rig_mesh_from_scratch_v5.py` | Reusable profile wrapper for wolf/panther geometry and balanced/grounded/athletic motion |
| `blender/rig_mesh_from_scratch_v6.py` | Manifest-driven multi-species sanitation, fitting, palette generation, family motion, and six-action export (**default**) |
| `blender/species_v6_manifest.json` | Source hashes, exclusions, family profiles, material provenance, and per-species overrides |
| `docs/scratch-v6-reference-quality.md` | Panther/stag visual-motion quality bar and skinning/attack checklist |
| `docs/scratch-v6-species-builds.md` | Batch provenance, exclusions, and reproduction commands |
| `blender/measure_deformation.py` | Edge stretch/squash QA across every saved action |
| `blender/qa_scratch_v6.py` | Loop, motion, bounds, finite-geometry, Attack, and Death semantic QA |
| `blender/inspect_fbx.py` | Source meshes, transforms, armatures, materials, and imported actions |
| `scripts/build_species_v6.py` | Parallel build, deformation QA, motion QA, and FBX re-import |
| `scripts/package_species_v6.py` | VMDL/VMAT generation, texture installation, and catalog update |

Generated assets and reports belong under:

```text
tools/sbox-asset-harness/out/<asset_id>/
scene_lab/Assets/models/<asset_id>/
```

Never overwrite a known-good package while experimenting. Every meaningful rig
or motion strategy gets a new asset ID and output folder.

## Pipeline contract

### Inputs

- One quadruped mesh with a readable side profile.
- A color texture or an explicit flat-material fallback.
- No required donor skeleton.
- No required source animations.

A source FBX may contain an old armature or actions, but those are treated as
untrusted baggage and removed before rigging.

### Outputs

- skinned mesh FBX with bind pose only;
- armature-only animation FBX;
- Blender source file;
- JSON build report;
- deformation report;
- rest/Idle/Walk/Trot/Gallop/Attack/Death previews where applicable;
- VMDL with inspected take indices;
- real VMAT path and catalog record.

### Required report facts

Every donor-free build must record:

- `donorUsed: false`;
- source mesh path and selected mesh object;
- applied rotation, scale, and final bounds;
- `orientation.facingOk`;
- rig/deform bone counts;
- weighted and unweighted vertex counts;
- head/neck stabilized and Jaw-weighted vertex counts;
- action names and frame ranges;
- profile name and all motion factors;
- output paths.

## 1. Sanitize the source

Do this even when the file is described as “unrigged.”

1. Select the highest-poly render mesh intentionally.
2. Preserve its world matrix while detaching its parent.
3. Remove Armature modifiers.
4. Clear all vertex groups.
5. Delete imported armature objects.
6. Delete every imported action before authoring new motion.
7. Apply exceptional source object scale only through an explicit,
   model-specific profile.
8. Keep the original source file read-only.

This is what makes `donorUsed: false` meaningful. Reusing geometry from an
animated FBX is acceptable only after the source rig, weights, constraints, and
actions are removed.

### Source provenance

Do not claim a mesh is Tripo-generated unless provenance is embedded or known.
For example, the local panther looks generated and has a baked atlas, but its
FBX does not prove Tripo provenance. Document “likely generated/Tripo-derived”
instead of asserting it.

## 2. Normalize orientation, scale, and ground

### Orientation

The runtime convention is:

- Blender/s&box up: `+Z`;
- creature head/front: `-Y`;
- left side: `+X`;
- right side: `-X`.

Never hardcode one rotation for every Tripo FBX. The wolf source required
`+90° Z`; the panther source required `0°` because its object transform already
carried the effective turn.

`facingOk` must check both:

1. the generated Head bone is on the `-Y` side of the root; and
2. the mesh snout is also on the `-Y` side.

A bones-only check produced a false positive in the Rigify-v3 experiment:
head bones faced `-Y` while the mesh snout faced `+Y`.

### Scale

- Apply a uniform scale only.
- Preserve the source’s width/length/height proportions.
- Use a named target height (wolf `1.15`, panther `1.10` in current tests).
- Move the final AABB bottom to `Z=0`.
- Record canonical bounds.

Avoid anisotropic donor-AABB fitting. It can make the mesh fit a foreign
skeleton numerically while distorting the body and exaggerating spine/limb
deformation.

## 3. Generate landmarks

Use normalized AABB fractions, not absolute coordinates. At minimum detect:

- hips/root;
- mid-spine;
- chest;
- withers;
- neck;
- head;
- lower jaw;
- tail root and tip;
- front shoulder, elbow, wrist, paw on each side;
- hind hip, knee, hock, paw on each side.

Mirror-side rules:

- `+X` vertices feed `.L`;
- `-X` vertices feed `.R`;
- enforce a minimum lateral offset so a noisy/asymmetric mesh does not place
  limb bones on the centerline.

Model-specific landmark code must be narrowly scoped. Scratch-v6 uses
species-family bounds plus a nearest-sample fallback for sparse topology; every
fallback count remains in the build report.

### Skeleton topology

The current scratch quadruped is intentionally small:

```text
Root (motion, non-deform)
└─ Spine1 ─ Spine2 ─ Spine3 ─ Neck1 ─ Head
                                     └─ Jaw
   ├─ Tail1 ─ Tail2 ─ Tail3 ─ Tail4
   ├─ FrontShoulder.L ─ FrontUpper.L ─ FrontLower.L
   ├─ FrontShoulder.R ─ FrontUpper.R ─ FrontLower.R
   ├─ HindUpper.L ─ HindLower.L ─ HindFoot.L
   └─ HindUpper.R ─ HindLower.R ─ HindFoot.R
```

Keep the runtime deform rig small. Full Rigify generated hundreds of
control/mechanism bones and constraints that added retarget/export complexity
without improving the 2k-vertex mesh.

## 4. Skin the mesh

### Preferred sequence

1. Attempt Blender automatic heat weighting.
2. Normalize once.
3. Limit to four influences because s&box truncates five or more.
4. Strip contralateral limb influences.
5. If heat weighting leaves vertices unweighted, use a deterministic nearest
   bone-segment fallback with side gating.
6. Run the anatomical head/neck stabilization pass.
7. Normalize and report counts.

Some generated topologies cannot be solved reliably by Blender heat weighting.
The documented fallback assigns the nearest four deform bone segments with
inverse-power weights. This is acceptable because it is deterministic,
side-aware, and validated by deformation/visual QA.

### Head and neck isolation

Shoulder weights pulling the mane, neck, or muzzle made head motion appear tied
to front-leg motion even when Neck/Head animation amplitudes were tiny.

The stabilization pass:

- targets the upper front quarter of the mesh;
- excludes the low front-leg volume;
- removes all Front/Hind group influence;
- assigns lower muzzle vertices to `Jaw + Head`;
- assigns skull/snout to `Head`;
- blends neck into `Neck1`;
- blends the rear mane/withers into `Spine3`;
- uses at most three groups and reports the affected counts.

This is a visual-quality change, not merely an animation-curve change. It may
raise the edge tear score at the new hard anatomical boundary, so inspect both
the metric and the animation.

### Common weight failures

| Symptom | Likely cause | Preferred response |
|---|---|---|
| Legs cross the centerline | Contralateral limb weights | Strip wrong-side limb groups, then normalize |
| Flank spikes ride with legs | Limb weights on side torso | Reclaim upper flank to spine/body groups |
| Neck follows front stride | Shoulder weights on neck/mane | Anatomical head/neck stabilization |
| Lower jaw does not bite | No Jaw bone or no Jaw-weighted vertices | Generate Jaw landmark/bone and report Jaw vertex count |
| Heat bind leaves holes | Bone heat cannot solve topology | Deterministic nearest-segment fallback |
| Hard seam/tearing after cleanup | Full region replacement too abrupt | Blend boundaries; avoid repeated global smoothing |
| Rubber legs | Excess global smoothing or torso bleed | Reduce smooth passes; attenuate axial groups near limb capsules |

## 5. Author procedural actions

All actions are sampled at 30 FPS and saved with fake users so Blender keeps
them after the active action is cleared for mesh export.

### Idle

- nearly static root and limbs;
- subtle chest breathing;
- restrained head/neck drift;
- slight tail motion;
- seamless first/last pose.

Idle is the first skinning gate. It should have zero or near-zero torn samples.

### Walk

- diagonal pairs (`front-left + hind-right`, then opposite);
- restrained spine;
- small root vertical motion;
- head/neck nearly static and on a phase not identical to either leg pair;
- seamless loop.

Current base amplitudes after the head isolation audit:

- Neck: about `0.003 rad`;
- Head: about `0.002 rad`.

### Trot

- diagonal two-beat timing for most families;
- camelids use a lateral-sequence run;
- restrained root bounce and head motion;
- seamless first/last pose.

### Gallop

- front and hind gather/extend phases;
- slightly offset left/right timing;
- spine motion remains smaller than limb motion;
- restrained head/neck:
  - Neck about `0.005 rad`;
  - Head about `0.003 rad`.

### Attack

The attack must read as an attack in silhouette:

1. anticipation/windup;
2. Root lunge in `-Y`;
3. both front chains reach;
4. neck/head thrust;
5. Jaw opens during approach;
6. Jaw snaps near closed at impact;
7. optional small rebound;
8. recovery to rest.

Current reference values:

- 42 frames;
- strike Root translation about `-0.25 Y`;
- Jaw opening peak about `0.48 rad`;
- both front legs extend, with slight asymmetry allowed.

A head thrust without a Jaw bone reads as nodding, not biting.

### Death

The death must become side-lying rather than spinning or “jumping.”

- brief stagger;
- mostly rigid Root-driven fall;
- Root rotates around local `Z` (approximately the creature’s longitudinal
  axis for this generated root), not the root bone’s local `Y` axis;
- limbs fold;
- neck/head settle;
- Jaw remains slightly slack;
- side-lying pose is reached by 60–70% and held.

Reference final Root rotation is about `1.42 rad`.

Verify the evaluated final mesh, not just bone values:

- final Z extent materially lower than rest;
- final lateral X extent substantially larger than rest;
- lateral center offset consistent with the chosen side.

The validated wolf/panther side falls reduce Z extent to roughly 72–73% of
rest and increase X extent to about 2.9–3.0×.

## 6. Motion profiles

Profiles are semantic multipliers over the same authored actions. Keep the base
rig and action intent identical so the comparison answers one question:
“How much motion is appropriate?”

| Profile | Axial | Limbs | Head | Tail | Root XY | Root Z | Intent |
|---|---:|---:|---:|---:|---:|---:|---|
| Balanced | 0.85 | 1.00 | 0.75 | 0.90 | 0.95 | 0.70 | Reference-plus |
| Grounded | 0.50 | 0.90 | 0.50 | 0.60 | 0.65 | 0.20 | Stable, low bounce |
| Athletic | 0.80 | 1.18 | 0.70 | 1.15 | 1.15 | 0.85 | Longer, stronger stride |

Exceptions that override the table above in the current v5 rebake:

- Attack Head/Neck/Jaw keep at least `0.90` of authored motion even when the
  profile head factor is lower, so Grounded still reads as a bite/lunge;
- Death preserves Root rotation and translation regardless of profile root
  factors, so the side-fall silhouette stays intact;
- include `Jaw` in the head multiplier group.

Every output report and catalog note must store the profile name, factors, and
intent. Also write a `VARIATIONS.md` alongside grouped experiments.

## 7. Deformation QA

`measure_deformation.py` compares each mesh edge against bind-pose length and
flags:

- stretch ratio `>= 1.8`;
- squash ratio `<= 0.45`.

It reports:

- `tearScore`: number of unique edges that crossed either threshold in any
  sampled action/frame;
- `perAction.*.tornEdgeSamples`: repeated edge-frame violations (not the same
  aggregation as `tearScore`, and not a sum that equals it);
- `maxStretch`;
- dominant weight group for bad edges.

Prefer also reporting a normalized rate (`tearScore / edgeCount`) when comparing
meshes of different density. Six-frame sampling is for iteration; release builds
should sample denser, especially on Attack/Walk/Gallop.

### Important limitations

- A lower score does not prove correct facing.
- A score of zero with `perAction: {}` means no actions were measured, not
  perfect deformation. Idle can also score zero when it is nearly static; that
  only proves the bind is stable under tiny motion.
- Scores across different action sets, topologies, or thresholds are not
  strictly apples-to-apples.
- More expressive, semantically correct actions can score worse than restrained
  but visually inert motion.
- The metric does not detect foot sliding, weak silhouettes, wrong gait timing,
  backwards facing, blank previews, or a death that yaws instead of falling.
- Bones-only `facingOk` on older donor packages is not trustworthy; require
  mesh-snout and Head-bone agreement.

### Required acceptance gates

Automated:

- `donorUsed == false`;
- `facingOk == true` using mesh and bones;
- action count exactly six for scratch-v6;
- action names/ranges expected;
- zero unweighted vertices;
- no opposite-side limb weights after cleanup;
- Jaw exists and has nonzero weighted vertices;
- head/neck stabilization count is nonzero;
- Idle torn samples near zero;
- animation FBX clean re-import contains only the scratch armature and actions;
- final Death AABB passes side-lie checks.

Visual:

- rest: bones sit inside anatomy;
- Idle: no shoulder/neck drag;
- Walk: diagonal sequence, no crossing, no rubber collapse;
- Gallop: readable gather/extend without volatile rear spine;
- Attack: forward lunge + both front legs + bite;
- Death: settles visibly on a side and holds;
- material and facing correct in ModelDoc.

Do not accept a build from the tear score alone.

## 8. Blender and FBX pitfalls

- Blender 5.2 actions need `use_fake_user = true` when they are not kept in NLA
  tracks. Otherwise the saved blend can silently drop them.
- `bake_anim_use_all_actions=true` exports actions in FBX/import order, which
  can be alphabetical rather than creation order. Re-import the animation FBX
  and inspect the actual take order before writing VMDL indices.
- Remove source actions before export or they can leak into the animation FBX.
- Export mesh FBX with `bake_anim=false`.
- Export animation FBX armature-only.
- Use `add_leaf_bones=false`.
- Use `use_armature_deform_only=true`.
- s&box limits effective blend weights to four; limit in Blender first.
- Passing the target itself as the preview helper’s “hidden donor” produces
  blank previews. Hide the rig or a separate dummy instead.
- Some FBXs carry unapplied object scale or importer-specific light properties;
  isolate any compatibility patch and document it in the model profile.

## 9. s&box packaging

### VMDL

For each asset:

- `RenderMeshFile.filename` points to its skinned mesh FBX;
- `import_filter.exception_list` exactly matches the exported mesh object name;
- `AnimationList` points to the armature-only animation FBX;
- take indices come from clean FBX re-import;
- Attack/Death are non-looping;
- Gallop/Idle/Trot/Walk are looping;
- material path exists and is cataloged.

Current canonical take order:

```text
0 Attack
1 Death
2 Gallop
3 Idle
4 Trot
5 Walk
```

Never assume it. Verify it after every export change.

### Materials

- Copy real source textures into the active game.
- Convert JPEG to PNG.
- Use `shaders/complex.shader` in source VMAT.
- Point only to paths that exist.
- Shared profile variants should reuse one common material.

### Compilation

The s&box file watcher is not reliable for folders created after the editor
started. A valid source VMDL may still lack `.vmdl_c`.

Preferred:

1. compile through the live editor asset API when available;
2. otherwise open each VMDL once in Asset Browser/ModelDoc;
3. verify `.vmdl_c` exists and is up to date;
4. report missing compiled outputs honestly.

Standalone `resourcecompiler.exe` may fail without the editor’s mod/game context.

### Catalog

Every package gets a ready catalog entry only after source VMDL/FBXs/material
exist. Notes include:

- donor-free status;
- geometry source;
- profile intent;
- rotation and height;
- facing;
- latest tear score;
- Jaw/head-stabilization behavior;
- compile status when relevant.

Run:

```powershell
python tools/sbox-asset-harness/scripts/validate_catalog.py
```

## 10. Reproducible build example

Wolf Grounded:

```powershell
$b = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
$id = "tripo_wolf_scratch_v5_grounded"
$out = "tools\sbox-asset-harness\out\$id"

& $b --background --factory-startup `
  --python tools\sbox-asset-harness\blender\rig_mesh_from_scratch_v5.py -- `
  --target scene_lab\Assets\models\tripo_wolf_test\tripo_wolf_test.fbx `
  --profile grounded `
  --asset-id $id `
  --landmark-profile wolf `
  --rig-name ScratchWolfV5Armature `
  --target-z-rotation 90 `
  --target-height 1.15 `
  --out-fbx "$out\$id.fbx" `
  --out-anims "$out\${id}_anims.fbx" `
  --out-blend "$out\$id.blend" `
  --preview-dir "$out\previews"

& $b --background "$out\$id.blend" `
  --python tools\sbox-asset-harness\blender\measure_deformation.py -- `
  --out "$out\deformation_report.json" --frames 6
```

Panther uses:

```text
--target thorns/Assets/models/panther/panther.fbx
--landmark-profile panther
--rig-name ScratchPantherV5Armature
--target-z-rotation 0
--target-height 1.10
```

## 11. Debugging decision tree

1. **Model faces backward**
   - inspect raw mesh axis/object rotation;
   - test mesh snout and Head bone ends;
   - change model profile rotation;
   - never optimize facing via tear score.
2. **No actions measured**
   - inspect saved blend `bpy.data.actions`;
   - set fake users;
   - confirm metric `perAction` is nonempty.
3. **Unweighted vertices**
   - inspect bone-heat warnings;
   - use deterministic nearest-segment fallback;
   - report fallback explicitly.
4. **Neck/head follows front legs**
   - inspect weights before curves;
   - run anatomical stabilization;
   - then reduce Neck/Head amplitudes.
5. **Rubber/crossing legs**
   - remove opposite-side weights;
   - reduce repeated global smoothing;
   - attenuate torso groups near limb capsules;
   - shorten profile limb factor before rewriting topology.
6. **Volatile rear**
   - lower Root/Spine profile factors;
   - inspect hip/flank weights;
   - choose Grounded as baseline.
7. **Attack does not read**
   - verify Root advances `-Y`;
   - inspect both front chains;
   - require Jaw open/close, not head nod only.
8. **Death does not read**
   - test Root local axes in evaluated mesh space;
   - measure final AABB;
   - hold the final side pose.
9. **VMDL lists wrong actions**
   - clean-import animation FBX;
   - rewrite take indices from observed order.
10. **VMDL source exists but cannot be selected**
    - check `.vmdl_c`;
    - open once in ModelDoc or use live editor compile;
    - inspect compiler warnings.

## 12. Definition of done

- [ ] Source sanitation recorded.
- [ ] Uniform scale and ground contact applied.
- [ ] Mesh and bones face `-Y`.
- [ ] Landmark counts nonzero.
- [ ] Zero unweighted vertices.
- [ ] Four or fewer influences.
- [ ] Opposite-side weights removed.
- [ ] Head/neck stabilization and Jaw counts nonzero.
- [ ] Six expected scratch-v6 actions saved and fake-user protected.
- [ ] Animation FBX clean-reimported; take order recorded.
- [ ] Idle/Walk/Trot/Gallop/Attack/Death visual checks passed.
- [ ] Death final AABB proves side lying.
- [ ] Deformation report generated and interpreted with caveats.
- [ ] Mesh/anims FBXs copied into a unique Assets folder.
- [ ] VMDL paths/filter/takes/loop flags correct.
- [ ] VMAT and texture paths exist.
- [ ] Catalog entry and `VARIATIONS.md` updated.
- [ ] Catalog validator passes.
- [ ] `.vmdl_c` compile status reported.

