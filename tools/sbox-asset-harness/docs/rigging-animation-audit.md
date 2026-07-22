# Rigging and animation audit

Audit of the automated creature work completed through scratch-v5. This
separates measured evidence, visual findings, and process lessons so future
builds do not repeat solved failures.

Canonical implementation guide:
[`donor-free-creature-rigging.md`](donor-free-creature-rigging.md).

## Executive conclusion

The successful approach is:

> sanitize geometry → normalize model-specific orientation/scale → fit a small
> skeleton to mesh landmarks → bind with side/anatomy constraints → author
> restrained procedural actions → measure deformation → verify silhouettes in
> previews and ModelDoc → package inspected FBX takes.

The less successful approach was:

> force a generated mesh onto a foreign skeleton → inherit foreign animation
> amplitudes → repeatedly repair weights and orientation after deformation.

Donor retargeting proved that automated skinning and QA were possible, but the
scratch pipeline removed the largest source of instability: a skeleton and
motion library authored for different anatomy.

## Current measured state

All current scratch packages contain five procedural actions, face `-Y`, retain
no donor/source actions or armature, and include a real Jaw.

| Model | Build/profile | Tear score | Worst region | Interpretation |
|---|---|---:|---|---|
| Wolf | scratch-v4 current | 209 | Tail1 / Neck1 / hind chain | Reference authoring build |
| Wolf | v5 Balanced | 193 | Tail1 | Best all-purpose comparison |
| Wolf | v5 Grounded | 166 | Tail1 | Lowest current wolf score |
| Wolf | v5 Athletic | 229 | Tail1 | More reach, expected higher stress |
| Panther | v5 Balanced | 179 | Spine2 | Stable general profile |
| Panther | v5 Grounded | 151 | Spine2 | Lowest current overall score |
| Panther | v5 Athletic | 207 | Spine2 | Expressive but still controlled |

Thresholds are stretch `1.8`, squash `0.45`, sampled at six frames per action.
Scores across action sets or skeleton revisions are directional, not strictly
comparable.

### Historical reference points

| Generation | Method | Result |
|---|---|---|
| Early donor bind | Donor AABB fit + automatic weights | Commonly 500+ tear; usable Idle/Walk ceiling |
| Donor v2.7 | Landmark-fitted donor + gentle limb capsules | Tear about 341; improved legs but still inherited donor/anatomy mismatch |
| Rigify v3 | Minimal Rigify-derived deform rig + retarget | Tear about 499 after correct facing; volatile rear/axial motion |
| Scratch v4 pre-Jaw | Custom landmark rig + Idle/Walk/Gallop | Tear about 114 on the earlier, quieter three-action revision |
| Scratch v4 current | Jaw, attack/death silhouettes, head isolation | Tear 209; visually clearer behaviors with more demanding poses |
| Scratch v5 | Same authored semantics, three motion profiles | Wolf 166–229; panther 151–207 |

The rise from the early v4 score to current v4 does not mean the build became
worse overall. The current set adds a weighted Jaw, stronger lunge/bite,
side-fall Death, and an anatomical head/neck boundary. These create more
measured edge stress while fixing visible behavior the metric does not score.

## Scratch-v6 multi-species update

Scratch-v6 generalized the donor-free workflow to 14 unique animals: alpaca,
bull, cow, deer, donkey, fox, horse, husky, Shiba Inu, stag, elk, moose, plus
new wolf and panther versions. It adds a manifest, source hashes, automatic
orientation, sparse-landmark fallback, palette texturing, rigid antler/head
accessories, family gait profiles, Trot, semantic motion QA, and generated
s&box packaging.

All 14 pass orientation, export/re-import, loop, finite-geometry, locomotion,
Attack-travel, and Death-side-fall checks. Tear scores range from 11 (fox) to
1423 (elk). Deer (380), elk (1423), moose (549), and panther (355) are flagged
for priority ModelDoc review; automated passage does not certify visual skin
quality.

A cervid semantic-zone reweighting experiment was explicitly rejected. It
reduced elk and moose tear counts but created much worse triangular sheets
across front legs and bellies, while deer also regressed numerically. The
original heat/nearest-segment builds were restored. This is direct additional
evidence for the existing rule: prefer localized weight edits, and never accept
a lower deformation score without inspecting rendered motion.

Full inventory, exclusions, scores, commands, and per-family movement choices:
[`scratch-v6-species-builds.md`](scratch-v6-species-builds.md).

## What worked well

### 1. Fitting the skeleton to the mesh

Landmark-derived hips, chest, neck, head, limbs, and tail eliminated much of the
rest-pose mismatch that donor fitting could not solve. Normalized AABB fractions
made the method portable between the wolf and panther while allowing narrow
profile-specific fallbacks.

Why it worked:

- joint pivots start inside the target anatomy;
- bone lengths reflect the target silhouette;
- procedural rotations operate around useful pivots;
- no retarget basis conversion is required.

### 2. A small runtime rig

The 23-bone/22-deform scratch armature is sufficient for these ~2k-vertex
creatures. It exports cleanly, is easy to inspect, and maps directly to authored
semantics.

Why it worked:

- fewer ambiguous weight groups;
- no control/mechanism bones;
- no runtime constraints;
- clear ownership of every animated channel;
- straightforward s&box import.

### 3. Procedural motion authored for the generated rig

Idle, Walk, Gallop, Attack, and Death are parameterized instead of copied from
foreign bone proportions.

The strongest improvements were:

- diagonal Walk timing;
- restrained axial movement;
- substantially quieter head/neck locomotion;
- Attack with Root lunge, bilateral reach, and a real Jaw snap;
- Death driven into a side-lying pose and held.

These changes came directly from visual feedback and made action intent readable
in silhouette.

### 4. Separating motion profiles from rig generation

Balanced, Grounded, and Athletic rebake the same action semantics with named
factors. This made experimentation controlled and reproducible.

Observed pattern:

- Grounded consistently produced the lowest tear score;
- Balanced retained more character with moderate stress;
- Athletic increased stress predictably rather than producing random failure.

This indicates the profile controls are changing the intended variable: motion
amplitude.

### 5. Source sanitation

The panther test demonstrated that a mesh can be reused from an animated FBX
without using its donor data. Detaching the mesh, deleting Armature modifiers,
clearing vertex groups, deleting imported armatures, and purging actions made
the provenance of the new rig unambiguous.

### 6. Deterministic skinning fallback

Blender heat weighting failed on the panther, but the side-aware nearest
bone-segment fallback weighted every vertex. A deterministic fallback is much
better for automation than retrying a non-deterministic operator and hoping it
converges.

### 7. Anatomical weight cleanup

Two targeted strategies were effective:

- gentle limb capsules attenuated torso bleed without replacing all weights;
- head/neck stabilization removed limb influence from mane/skull/muzzle and
  created a real Jaw region.

Targeted cleanup consistently beat broad global rewrites.

### 8. Layered QA

The combination of:

- JSON build reports;
- deformation reports;
- facing checks;
- clean FBX re-import;
- action-specific previews;
- user inspection in ModelDoc;
- versioned output folders;

made failures diagnosable and prevented a single misleading signal from
deciding quality.

## What worked poorly

### 1. Treating donor fit as a geometry-only scaling problem

AABB scaling aligned overall size but did not align shoulder, hip, spine, or
tail pivots. Heat binding then assigned side torso vertices to nearby leg bones,
creating flank pinches and inward/rubbery legs.

Permanent lesson: fit joints to anatomy first; do not expect weighting to repair
a bad rest rig.

### 2. Full weight-region replacement

Aggressively replacing torso/limb groups improved one pose but created hard
boundaries and large tear regressions. Repeated smoothing then reintroduced the
bleed it was intended to fix.

Permanent lesson: use localized attenuation/blending, side gating, and explicit
anatomy zones. Normalize after one deliberate cleanup sequence.

### 3. Full Rigify for this runtime target

Rigify’s generated control rig is valuable for manual Blender animation, but it
is excessive for automated s&box export. Hundreds of controls, mechanism bones,
constraints, and basis conversions add failure modes while the final runtime
deform rig remains small.

The v3 compromise still inherited donor animation and showed volatile rear
spine/hindquarter movement. Damping reduced symptoms but did not remove the
anatomical mismatch.

Permanent lesson: use Rigify only as an animator-facing authoring rig if a human
will use its controls. Export a deliberately defined deform skeleton.

### 4. One-to-one donor motion amplitude

Retargeted axial and root channels were too strong for the fitted target. Rear
spine, pelvis, and hindquarters moved far more than needed.

Permanent lesson: motion authored on another rest rig needs per-region damping,
but once most channels need damping, direct procedural authoring is simpler and
more controllable.

### 5. Hardcoded facing assumptions

The donor documentation originally prescribed `-90°` for “Tripo.” The Rigify
wolf later needed `+90°`, and the panther needed `0°`. A bones-only orientation
check reported success even when mesh and bones faced opposite directions.
Diagnostics also show the installed donor v27 package can report
`facingOk: true` while mesh snout and head bone disagree. Treat that package
as a legacy artifact, not the orientation authority.

Permanent lesson: source generator does not define orientation. Inspect the
actual mesh and verify mesh plus skeleton.

### 6. Using tear score as a complete quality score

Three misleading cases occurred:

- rotating the mesh backward could lower tear;
- missing/discarded actions produced a perfect-looking zero;
- visually better attack/death/head isolation could increase tear.

Permanent lesson: tear score is a deformation regression metric, not an
animation-quality objective.

### 7. Blender action persistence and export assumptions

New actions without users were dropped from saved blends. FBX take order also
did not reliably match authoring order.

Permanent lesson:

- set `action.use_fake_user = true`;
- clean-reimport exported animation FBXs;
- derive VMDL take indices from observed order.

### 8. Preview helper misuse

Passing the target mesh as the object to hide caused blank previews. Preview
generation can succeed technically while producing no useful evidence.

Permanent lesson: validate preview content and use a dedicated hidden donor/dummy
argument.

### 9. Source-file baggage

The panther FBX carried baked scale, a source rig, duplicated action sets, and an
importer compatibility issue. Assuming it was “just geometry” would have
contaminated both output and reports.

Permanent lesson: inspect and sanitize every source regardless of label.

### 10. Relying on the s&box watcher

New VMDL folders did not always compile until opened manually. Source packaging
was correct, but runtime resources were not immediately available.

Permanent lesson: source existence and compiled-resource existence are separate
gates. Report each one.

## What remains weak

The scratch pipeline is good, but not fully general:

1. Landmark bands are tuned for wolf/panther-like quadrupeds.
2. The mesh front detector relies partly on silhouette/height heuristics.
3. The fallback binder is distance-based, not volume/geodesic-aware.
4. Feet do not use IK or ground-contact constraints; sliding is judged visually.
5. The tear metric does not score contact, silhouette, self-intersection, or
   action semantics.
6. Build/report/VMDL packaging is still spread across scripts and manual steps.
7. s&box compile verification is partly editor-dependent.
8. There are no automated golden-report regression tests yet.

These are future-hardening items, not reasons to return to donor retargeting.

## Recommended next engineering work

Priority order:

1. Extract v4/v5 shared code into a named `scratch_quadruped` module.
2. Store landmark bands, source sanitation, target height, and rotation in
   per-model JSON profiles.
3. Add a preflight command that emits source inspection plus a rendered
   orientation sheet before rigging.
4. Add action-semantic QA:
   - Attack strike displacement/Jaw delta;
   - Death final AABB side-lie ratio;
   - Walk/Gallop loop endpoint delta;
   - foot-contact velocity during stance.
5. Add a clean-reimport validator for armature/action/take names.
6. Generate VMDL and catalog fragments from the inspected export manifest.
7. Keep the current wolf and panther reports as regression fixtures.
8. Add a third topology (long-bodied or short-legged quadruped) before calling
   landmark fitting generally portable.

## Decision rule for future creatures

Use donor-free scratch when:

- five to ten purpose-built actions are enough;
- the creature is approximately quadrupedal;
- stable automated output matters more than matching a large legacy library.

Use donor retargeting when:

- a large existing action library is mandatory;
- target and donor rest anatomy are close;
- manual weight paint/retarget cleanup is acceptable.

Use a human animator/control rig when:

- nuanced acting or contact-rich motion is required;
- the creature is outside current landmark assumptions;
- action quality matters more than unattended reproducibility.

