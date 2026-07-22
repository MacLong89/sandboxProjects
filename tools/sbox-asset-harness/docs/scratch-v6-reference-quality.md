# Scratch-v6 reference quality bar (panther + stag)

Panther (`felid`) and stag (`cervid_heavy`) are the visual/motion references for
new scratch-v6 creatures. Prefer matching these behaviors over chasing a lower
tear score alone.

## Reference packages

| Role | Species | Asset ID | Gallop | Attack |
|------|---------|----------|--------|--------|
| Carnivore | panther | `tripo_panther_scratch_v6_realistic` | `rotary_double_suspension` (paired fores/hinds) | whole-leg `pounce_bite` |
| Herbivore | stag | `stag_scratch_v6_realistic` | `rotary_double_suspension` (override; was lope) | neck+head `head_charge` only |

VMDLs live under `scene_lab/Assets/models/<assetId>/`.

## Lessons that must stay in the pipeline

These are implemented in `blender/rig_mesh_from_scratch_v6.py` unless noted.

### Orientation and landmarks

- Canonical facing: head / snout at `-Y`, up `+Z`. Detect per mesh; never assume a fixed Tripo yaw.
- Cervid crowns inflate AABB height. Cap landmark Z (`robust_proxy_bounds` ~0.72 for cervids) and **repair** collapsed hind chains after armature build (`repair_scratch_limb_chains`).
- Lift Spine1–3 onto the dorsal ridge when they sit in the gut; otherwise withers bind to limbs and erupt in motion.

### Skinning (order matters)

1. Heat / nearest-segment bind.
2. `stabilize_head_neck_by_bones` — smooth **only** Head/Neck/Spine groups; never strip Front/Hind from verts nearer limbs than the neck axis.
3. `stabilize_front_limb_weights` — Shoulder→Upper→Lower; skip withers/dorsum and caudal brisket; smooth only Front* groups.
4. `stabilize_hind_limb_weights` — Upper→Lower→Foot; skip dorsal plate.
5. `seal_front_limb_spine_seams` — Upper/Lower vs Spine only (not Shoulder; hard shoulder seals create withers ribbons).
6. `reclaim_torso_from_front_limbs` — chest/brisket off Front* when nearer Spine.
7. `reclaim_dorsum_from_limbs` — withers/back off Front/Hind **and** Neck1 caudal of the neck joint (prevents attack humps).
8. Rigid crown / antler lock to Head; strip neck from crown; restabilize soft neck with crown protected.
9. **Final** reseal + torso reclaim + dorsum reclaim after neck restabilize (restabilize used to undo limb seals).

### Locomotion

- Detect `frontReach` (axis+sign) from paw mesh toward `-Y`; detect `hindReachSign` separately on HindUpper X. Do not drive hind with a weird front axis.
- Drive limbs from shoulder/hip; keep FrontLower/HindLower counters small (wrist/hock snaps).
- Carnivore / paired run: `rotary_double_suspension` or `elastic_bound` (both fores together, both hinds together, opposite half-cycle). Avoid `fast_trot_lope` when the user wants a bound.
- Cervid walk/gallop spine flex and root bounce stay mild so residual dorsum weights cannot spike the back.

### Attacks

- **Panther / felid pounce:** whole-leg swing from FrontShoulder; bold peak is OK once chest reclaim is solid. Note: `reference whole-leg pounce-bite`.
- **Stag / cervid headbutt:** Neck1 + Head lean only; **zero Spine2/3 arch**. Spine flex on a cervid withers plate reads as a giant back hump. Crowns rigid on Head. Note: `reference lean headbutt`.

### QA mindset

- Motion QA + tear score must pass, but visual review of Attack / Gallop / Walk is the gate.
- Lower tear after a broad positional reweight can still look worse (rejected cervid experiment — see `scratch-v6-species-builds.md`).

## Checklist for a new species

1. Add/update `blender/species_v6_manifest.json` (source SHA-256, family, height, materials, motionOverrides).
2. Build: `python tools/sbox-asset-harness/scripts/build_species_v6.py --species <id> --jobs 1 --deformation-frames 8`
3. Package: `python tools/sbox-asset-harness/scripts/package_species_v6.py`
4. ModelDoc: rest, Idle, Walk, Trot, Gallop, Attack, Death — compare against panther (carnivore) or stag (herbivore).
5. If legs dislocate: check limb-chain lengths + FrontLower/HindFoot neighbors (no Spine/Tail).
6. If back humps on attack: Neck1/Front* on dorsum; confirm final `dorsumReclaim` and attack spine = 0 for cervids.
7. If chest flaps on pounce: torso reclaim after seal; do not hard-seal FrontShoulder to Spine.

## Related docs

- Batch record: [`scratch-v6-species-builds.md`](scratch-v6-species-builds.md)
- Full donor-free contract: [`donor-free-creature-rigging.md`](donor-free-creature-rigging.md)
