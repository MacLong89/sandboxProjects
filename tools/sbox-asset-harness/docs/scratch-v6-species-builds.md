# Scratch-v6 multi-species build record

This is the reproducible record for the first donor-free, species-aware batch.
It preserves all v3/v4/v5 assets and installs each v6 creature into a new,
independent s&box package.

## Scope and provenance

- Pipeline: `scratch_v6_realistic`
- Manifest: `blender/species_v6_manifest.json`
- Rig/animation generator: `blender/rig_mesh_from_scratch_v6.py`
- Batch build and QA: `scripts/build_species_v6.py`
- Runtime motion QA: `blender/qa_scratch_v6.py`
- s&box packaging: `scripts/package_species_v6.py`
- Source contribution: geometry, UVs, and material appearance only
- Donor skeletons, weights, constraints, and actions retained: none
- Canonical runtime facing: head at `-Y`, up at `+Z`
- Generated actions: Attack, Death, Gallop, Idle, Trot, Walk
- Sampling rate: 30 FPS

The build script validates each selected source SHA-256 before import. Source
armatures, Armature modifiers, vertex groups, and actions are removed before a
new 23-bone/22-deform landmark rig is created.

## Selected unique animals

- Alpaca (`camelid`): lateral-couplet walk, lateral-sequence run, transverse
  gallop, stomp/threat attack. Tear score 41.
- Bull (`bovine`): heavy four-beat walk, diagonal trot, heavy transverse
  gallop, head charge. Tear score 70.
- Cow (`bovine`): heavy four-beat walk, diagonal trot, heavy transverse gallop,
  head charge. Tear score 96.
- Deer (`cervid_light`): four-beat walk, diagonal trot, elastic bound, head
  charge. Tear score 380; requires priority visual review.
- Donkey (`equid`): four-beat walk, diagonal trot, transverse four-beat gallop,
  hind kick. Tear score 74.
- Fox (`canid`): four-beat walk, diagonal trot, rotary double-suspension
  gallop, pounce-bite. Tear score 11.
- Horse (`equid`): four-beat walk, diagonal trot, transverse four-beat gallop,
  hind kick. Tear score 91.
- Husky (`canid`): four-beat walk, diagonal trot, rotary double-suspension
  gallop, bite-lunge. Tear score 39.
- Shiba Inu (`canid`): four-beat walk, diagonal trot, rotary
  double-suspension gallop, bite-lunge, stronger curled-tail balance. Tear
  score 30.
- Stag (`cervid_heavy`, **herbivore reference**): restrained four-beat walk,
  diagonal trot, **rotary double-suspension gallop** (paired fores/hinds; override
  away from fast trot/lope), neck+head lean headbutt with rigid antlers. See
  [`scratch-v6-reference-quality.md`](scratch-v6-reference-quality.md).
- Elk (`cervid_heavy`): restrained four-beat walk, diagonal trot, fast
  trot/lope, rigid-antler head charge. Tear score 1423; highest-priority visual
  review because the generated decorative topology deforms poorly.
- Moose (`cervid_heavy`): restrained four-beat walk, diagonal trot, fast
  trot/lope, rigid-antler head charge. Tear score 549; priority visual review.
- Tripo wolf (`canid`): four-beat walk, diagonal trot, rotary
  double-suspension gallop, forward bite-lunge.
- Bloom wolf (`bloomwolf`, `canid`): shared_assets bloom wolf mesh; same canid
  motion family as Tripo wolf; material `bloomwolf_basecolor`.
- Panther (`felid`, **carnivore reference**): stealth four-beat walk, diagonal
  trot, rotary double-suspension gallop, whole-leg pounce-bite. See
  [`scratch-v6-reference-quality.md`](scratch-v6-reference-quality.md).

Every package passed:

- source-hash validation;
- mesh and Head-bone `-Y` facing agreement;
- complete skin assignment;
- clean mesh-FBX and animation-FBX re-import;
- six expected action checks;
- loop endpoint continuity;
- finite evaluated vertex checks;
- locomotion limb/head amplitude checks;
- locomotion ground-penetration limits;
- Attack body-travel check;
- Death side-fall rotation check.

Passing these gates does not replace ModelDoc visual review. In particular, the
deformation score flags deer, elk, moose, and panther for closer inspection.

## Explicit exclusions

- `tools/FBX/Horse_White.fbx`: exact Horse geometry/topology; color-only
  variant.
- `tools/FBX/Deer.fbx`: duplicate deer species; the selected deer is the
  higher-detail canonical geometry.
- `tools/FBX/Wolf.fbx`: duplicate species and historical donor/reference file.
- `thorns/Assets/models/wolf/wolf.fbx`: duplicate wolf species; v6 continues
  the established Tripo wolf line.
- `shared_assets/Assets/models/panther-wolf/panther-wolf.fbx`: derivative
  hybrid, not a real-world species.
- `shared_assets/Assets/models/bloomseed/bloomseed.fbx`: plant/fantasy
  creature, outside the real-animal request.
- `tools/tripo_output/wolf_model.fbx` and
  `shared_assets/source/tripo/wolf_model.fbx`: byte-identical copies of the
  selected Tripo wolf source.

## Process changes that worked

1. A manifest now owns source identity, family, target height, materials,
   exclusions, and motion overrides.
2. Orientation is detected from geometry instead of assigned from a
   generator-wide assumption.
3. Sparse low-poly landmark searches fall back to nearest vertices around the
   requested anatomical region instead of aborting.
4. Multi-mesh species are joined after accessory vertices are marked.
5. Antlers, horns, and herbivore head crowns are rigidly assigned to Head.
6. Head-charge pitch sign is tested against evaluated geometry, avoiding the
   wrong local-axis assumption.
7. Low-poly material slots are converted to a generated palette texture,
   preserving appearance in one s&box material.
8. Family profiles separate canid, felid, light/heavy cervid, equid, bovine,
   and camelid timing and amplitude.
9. Runtime QA checks action semantics and evaluated geometry in addition to
   the older edge-deformation metric.
10. Packaging is generated from inspected outputs, including all six take
    indices and local VMAT/texture paths.
11. Cervid landmark Z is capped and limb/spine chains are repaired after build
    so antlers cannot collapse HindLower or sink the spine into the gut.
12. Front/hind limb stabilize, spine-seam seal, torso reclaim, and dorsum
    reclaim (including Neck1-on-back) run in a fixed order; final reclaim
    happens after neck restabilize so seals are not undone.
13. Vertex-group smooth is active-group only (never ALL) around limb/neck
    passes — ALL-mode smooth recreates paw ribbons and chest flaps.
14. Hind reach sign is detected separately from front reach axis/sign.
15. Stag attack uses neck+head only (no Spine2/3 arch). Panther attack uses
    whole-leg shoulder swing once chest weights are reclaimed.
16. Bound-style gallop (`rotary_double_suspension`) is preferred when the
    animal should run with both fores / both hinds together (panther, stag).

## Rejected experiment

A broad semantic cervid reweighting pass reassigned torso and limb zones by
normalized position. It reduced elk's tear score from about 1423 to 1007 and
moose from 549 to 496, but rendered severe triangular mesh sheets across the
front legs and belly. Deer also regressed from 380 to 630.

The experiment was rejected and the three packages were rebuilt with their
previous heat/nearest-segment binding. This reinforces the earlier finding:
lower tear score is not sufficient evidence, and broad positional weight
replacement is unsafe on decorative/disconnected generated topology.

## Reproduction

Build and QA every manifest entry:

```powershell
python tools/sbox-asset-harness/scripts/build_species_v6.py `
  --species all --jobs 3 --deformation-frames 8
```

Install all successful outputs into unique `scene_lab/Assets/models/<assetId>`
folders and update the catalog:

```powershell
python tools/sbox-asset-harness/scripts/package_species_v6.py
python tools/sbox-asset-harness/scripts/validate_catalog.py
```

Per-species reports, deformation results, previews, and Blender sources are
under `tools/sbox-asset-harness/out/<assetId>/`. The aggregate installed-package
record is `out/scratch_v6_package_summary.json`.

## s&box review order

Open each new `.vmdl` in ModelDoc. Review rest, Idle, Walk, Trot, Gallop,
Attack, and Death. Treat **panther** and **stag** as the quality bar (see
[`scratch-v6-reference-quality.md`](scratch-v6-reference-quality.md)), then
compare other carnivores to panther and herbivores to stag. Source VMDLs and
materials are installed; compiled `.vmdl_c` availability remains
editor/file-watcher dependent and must be confirmed in s&box.
