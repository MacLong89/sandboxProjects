# Aimbox Gunplay — Agent Context Sheet

Portable reference for the first-person weapon stack in **aimbox**. Use this when porting to other s&box games or when an agent needs full gunplay context without reading the whole repo.

---

## Module layout (port these folders/files)

```
aimbox/Code/Aimbox/
├── Core/
│   ├── AimboxWeaponDefinition.cs   # Static weapon catalog + stats
│   └── AimboxEnums.cs              # AimboxWeaponId, AimboxAttachmentId
├── Gunplay/                        # ★ Modular combat core (start here when porting)
│   ├── AimboxGunplayContext.md     # This file
│   ├── AimboxGunplayTypes.cs       # PelletResult, ShotResult, RecoilSession, ShotRequest
│   ├── AimboxGunplayContracts.cs   # IAimboxCombatAuthority, presentation gate, attachments
│   ├── AimboxWeaponRuntime.cs      # Per-weapon ammo/reload + AimboxWeaponInventory
│   ├── AimboxLocalCombatAuthority.cs # Hitscan, spread, target resolve, melee
│   └── AimboxWeaponCombatComponent.cs # Fire pipeline component on player
└── Gameplay/                       # Presentation + glue (can swap per game)
    ├── AimboxPlayerController.cs   # Input, camera, movement; delegates combat to component
    ├── AimboxViewModelController.cs
    ├── AimboxViewModelFpAnimator.cs
    ├── AimboxWeaponRecoilSolve.cs
    ├── AimboxWeaponRecoilController.cs
    ├── AimboxCombatTracer*.cs
    ├── AimboxCombatMuzzleResolve.cs
    ├── AimboxGameplaySfx.cs
    ├── AimboxWeaponResourceLoad.cs
    └── AimboxHitboxDebug.cs        # AimboxHitboxes capsule + headshot Z test
```

**Minimum port set:** `Core/` weapon defs + entire `Gunplay/` + viewmodel/tracer/recoil presentation files.

---

## Architecture (data flow)

```
Input (PlayerController)
  → AimboxWeaponCombatComponent.TryFire
      1. IAimboxWeaponPresentationGate (deploy/reload/melee block)
      2. AimboxWeaponRuntime.TryConsumeShot (ammo/cooldown)
      3. AimboxWeaponRecoilSolve → kick + fire direction
      4. IAimboxCombatAuthority.ResolveShot (hitscan pellets)
      5. IAimboxCombatAuthority.SpawnTracers (client FX)
      6. IAimboxCombatAuthority.ApplyDamage
      7. Player.RegisterCombatHitFeedback (HUD hitmarker)
  → AimboxViewModelFpAnimator (attack graph pulse after successful fire)
```

**Offline authority:** `AimboxLocalCombatAuthority` — client traces and applies damage immediately.

**Multiplayer path (not implemented yet):** Replace `IAimboxCombatAuthority` with a host-validated implementation:
- Client sends `[Rpc.Host] FireRequest(origin, direction, weaponId, tick, seed)` 
- Host runs same pellet trace logic, applies damage, replicates health
- Clients spawn tracers/SFX on confirmed shots only

---

## Weapon definitions (`AimboxWeaponDefinition`)

Each weapon is a static entry in `AimboxWeapons.All`:

| Field | Purpose |
|-------|---------|
| `Id` / `Name` | Enum + display |
| `MagazineSize` / `ReserveAmmo` | Ammo pool |
| `Damage` | Per-pellet base damage (shotgun = per pellet) |
| `HeadshotMultiplier` | Applied when hit Z ≥ head threshold |
| `FireDelay` | Seconds between shots (cooldown) |
| `ReloadSeconds` | Bulk reload time (or shell time cap for shotgun) |
| `SpreadDegrees` | Hitscan spread half-angle (rifles/pistols) |
| `PelletSpreadDegrees` | Shotgun cone half-angle (8 pellets) |
| `Pellets` | Count per trigger pull (8 = shotgun, 1 = rifle) |
| `Range` | Max ray distance |
| `FalloffStart` / `FalloffEnd` | Linear damage falloff via `DamageAtDistance` |
| `AdsSpreadMultiplier` | Spread multiplier when ADS held |
| `ViewModelPath` / `WorldModelPath` | VMDL paths (see ResourceLoad) |
| `IsMelee` | Uses melee trace (range-limited), no tracers/recoil |
| `AttachmentUnlocks` | Progression only; runtime modifiers below |

**Current weapons (combat):** M4A1, MP5, USP, M700, Spaghelli M4 (shotgun), Bow, M9 Bayonet, Trenchknife, Crowbar. Grenades are in the weapon catalog but not yet equippable in loadouts.

---

## Attachments (runtime modifiers)

Implemented in `AimboxWeaponRuntime` + `AimboxAttachmentModifiers`:

| Attachment | Effect |
|------------|--------|
| `ExtendedMag` | +50% mag size |
| `ForegripStraight` | ×0.82 visual recoil kick |
| `ForegripAngled` | ×0.88 visual recoil kick |
| `HoloSight` / `RaisedRedDot` | ×0.86 spread (red-dot style) |
| `RangedSight` | ×0.75 spread + scoped PiP ADS |
| `Suppressor` | ×0.25 gunfire noise (stealthier on noise bus) |
| `Flashlight` | Visual mount only (no gameplay modifier yet) |

---

## Ammo & reload (`AimboxWeaponRuntime`)

- **Bulk reload:** One timer fills mag from reserve (rifles/pistols/SMG).
- **Per-shell reload:** `SpaghelliM4` only — one shell per cycle (~0.52s), `UsesPerShellReload`.
- **Auto-reload:** Starts when last shot empties mag or manual Reload key.
- **Fire gate:** `IsReloading` or `_cooldown > 0` blocks `TryConsumeShot`.
- **Deploy gate:** Separate from reload — see viewmodel section.

---

## Hitscan & pellets (`AimboxLocalCombatAuthority`)

1. Build `AimboxCombatShotRequest` (attacker, weapon, aim forward, stance flags).
2. For each pellet (1 or 8):
   - `AimboxHitscanSpread.ApplyPelletSpread` — deterministic disk spread (hash-based, network-friendly).
   - Ray from `attacker.EyePosition` along direction, ignore attacker hierarchy.
   - Resolve target via `AimboxCombatTargetResolve` (`FindMode.EverythingInSelfAndParent`).
   - Headshot: `AimboxHitboxes.IsHeadshot(hitPos, targetRootPos)` — Z threshold at +56 units.
3. Returns `AimboxHitscanShotResult` with per-pellet damage only on hits.
4. **Melee:** Single ray out to `Definition.Range`, ×1.35 damage if heavy (Attack2).

**Performance notes:**
- Reuses scratch buffer `_pelletScratch[12]` — no alloc per shot for ≤12 pellets.
- Tracer end comes from same trace (`TracerEnd` on `AimboxPelletResult`) — avoids second world trace when length ≥ 1 unit from muzzle.

---

## Recoil (`AimboxWeaponRecoilSolve` + `AimboxWeaponRecoilController`)

Two layers:
1. **Fire direction** — spray pattern + bloom on authoritative aim vector.
2. **Visual kick** — spring-smoothed pitch/yaw on camera + viewmodel sway.

Tuning constants:
- `GlobalVisualKickMul = 0.25` (75% reduction from raw)
- ADS ×0.5 kick, crouch ×0.5 kick (stacking)
- Per-weapon profiles in `GetProfile()` inside RecoilSolve
- **Important:** Use explicit profile factories (not record struct defaults) — s&box compiler zeroed defaults previously.

Recoil state lives in `AimboxRecoilSessionState` on `AimboxWeaponCombatComponent`. Reset on weapon swap.

Debug: `AimboxRecoilDebug.Enabled` — default **false**. Set true for shot/solve logs.

---

## Viewmodels (`AimboxViewModelController`)

- Spawned as child of **player camera** GameObject (`NetworkMode.Never`).
- Stock FP weapons use **10× local scale** (`FpWeaponMeshRootScaleMul`).
- `SkinnedModelRenderer` with anim graph when path is in `UsesStockFpAnimatorSequences`.
- Optional FP arms via bone-merge (`FirstPersonArmsHumanPath`).
- ADS: anim graph `ironsights` param OR direct playback `Ironsights_Pose_Normal`.
- **View kick:** local rotation offset on viewmodel root, decays ~16/s.

### Model paths (`AimboxWeaponResourceLoad`)

| Weapon | FP viewmodel | World model |
|--------|--------------|-------------|
| M4A1 | `models/weapons/sbox_rifle_m4a1/v_m4a1.vmdl` | `w_m4a1.vmdl` |
| MP5 | `.../v_mp5.vmdl` | `w_mp5.vmdl` |
| USP | `.../v_usp.vmdl` | `w_usp.vmdl` |
| M700 | sniper FP path | sniper W path |
| Shotgun | `.../v_spaghellim4.vmdl` | `w_spaghellim4.vmdl` |
| Melee | per-weapon FP | bayonet W / empty for some |

Missing models fall back to cyan-tinted dev box.

---

## Animations (`AimboxViewModelFpAnimator`)

**Deploy (equip):**
- Sequence default `Deploy_Slide` (per-weapon overrides in ViewModelController config).
- `_equipPlaybackDone = false` until deploy finishes.
- `PresentationAllowsCombatFire` requires `_equipPlaybackDone && !reload && !melee`.

**Fire:**
- Graph param `b_attack = true` via `OwnerNotifyServerConfirmedFire()` after successful `TryFire`.
- `firing_mode` enum: 3 = full auto (M4/MP5), 1 = semi/burst default.

**Reload:**
- Bulk: `Reload_Empty` direct playback or graph.
- Shotgun: per-shell graph queue (`Reload_FirstShell`, `Reload_Shell`), `b_reloading` stance held across session.

**ADS:**
- Graph blend on `ironsights` or direct playback fallback when graph lacks param.

**Cancel on swap:** `CancelActivePresentation()` before destroying viewmodel — prevents async races.

---

## ADS & camera (PlayerController)

- Hip FOV 80 → ADS FOV 20, lerped at `AdsFovLerpSpeed` (× attachment AdsSpeed mul).
- `WantsAds = Attack2 && !IsMelee`.
- Eye pitch stored in `_pitch`, yaw in `WorldRotation`.
- `EyeRotation` synced each frame for aim/traces.

---

## Tracers (`AimboxCombatTracerFx`)

- Client-only `SceneLineObject`, ~0.11s lifetime.
- Width: start 1.4, end 0.175 (50% of original).
- Muzzle from `AimboxCombatMuzzleResolve` (attachment bones + per-weapon offsets).
- End point from pellet trace when possible; fallback second trace if muzzle→end < 1 unit.
- `AimboxCombatTracerSource.OtherPlayer` reserved for future MP replication.

---

## SFX (`AimboxGameplaySfx`)

- Fire/reload/equip at player eye position.
- Dedicated server / headless guarded.
- Shotgun unique fire/reload sounds; most rifles share M4 assets.

---

## Player controller integration

`AimboxPlayerController` keeps:
- Networked: `Health`, `IsAlive`, `ActiveWeapon`, `Team`, `AccountId`
- Local-only: ammo, reload, recoil, viewmodel, camera

Components created at runtime:
- `AimboxWeaponCombatComponent` — combat pipeline
- `AimboxViewModelController` on camera object

**Edge cases handled:**
- Dead players cannot shoot (weapon input skipped when dead).
- Proxies skip all local input/update.
- Deploy animation must finish before fire.
- Weapon swap resets recoil + view kick + cancels animator tasks.

---

## Hitboxes

Citizen capsule: radius 18, height 72, configured on player + dummy root.
Headshot band: hit world Z ≥ target origin Z + 56.

---

## Extending / porting checklist

1. Copy `Gunplay/` + weapon `Core/` definitions.
2. Implement your player pawn with `AimboxWeaponCombatComponent`.
3. Wire `AimboxViewModelController` to your camera.
4. Register `AimboxCombatTracerService.EnsureForScene` in game bootstrap.
5. Replace `AimboxLocalCombatAuthority` for networked games.
6. Map your input: `Attack1` fire, `Attack2` ADS/heavy melee, `Reload`, slot keys.
7. Add weapons to `AimboxWeapons.All` + FP/W paths in ResourceLoad.
8. Configure animator sequences in `ViewModelController.ConfigureAnimatorForWeapon`.

---

## Known limitations (intentional or TODO)

> AUDIT NOTES 2026-07-13: partial host-authoritative MP now exists (`AimboxNetworkCombat` +
> `AimboxGame` fire/kill/end RPCs). Remaining gaps below are still real.

- Host-authoritative player HP + fire resolve exist for listen-server; joiner inventory is mirrored
  on the host per fire RPC (base weapon stats only — attachments not Synced yet).
- Ammo still not Synced — host and client each ConsumeShot; can desync over a long mag fight.
- Match scores / lobby votes are not fully Synced (EndMatch + winners RPC exist; mid-match HUD may lag).
- Grenades catalog stubs may still lack full definition damage entries.
- TP weapon model on proxies — static world model, no fire anim.
- Recoil bloom uses `Random.Shared` for micro-spread (consider seeded RNG for replay).
- `AimboxViewModelTuner` editor component exists but is not wired to runtime controller.

---

## Quick debug toggles

| Toggle | Location | Default |
|--------|----------|---------|
| Recoil logs | `AimboxRecoilDebug.Enabled` | false |
| Hitbox overlay | `AimboxHitboxDebug` component in scene | off |

---

## File → responsibility cheat sheet

| Need to change… | Edit… |
|-----------------|-------|
| Damage / fire rate / spread | `AimboxWeaponDefinition.cs` |
| Pellet count / shotgun spread | `Pellets`, `PelletSpreadDegrees` on shotgun def |
| Recoil feel | `AimboxWeaponRecoilSolve.cs` profiles + `AimboxWeaponRecoilController` spring |
| Tracer look | `AimboxCombatTracerFx.cs` widths/duration/colors |
| Muzzle position | `AimboxCombatMuzzleResolve.cs` |
| Deploy / reload anims | `AimboxViewModelFpAnimator.cs` + `ConfigureAnimatorForWeapon` |
| When firing allowed | `PresentationAllowsCombatFire` in animator |
| Hit detection rules | `AimboxLocalCombatAuthority.TracePellet` |
| MP authority | Implement `IAimboxCombatAuthority` |

---

*Last updated: gunplay modularization pass — combat extracted to `Gunplay/`, melee fixed, tracers optimized, attachments wired, dead-fire blocked.*
