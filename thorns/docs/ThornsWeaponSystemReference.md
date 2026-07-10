# Thorns weapon system — implementation reference

This document describes how the **Thorns** s&box project implements guns end-to-end: authority, stats, recoil, networking, first-person (FP) viewmodels, third-person (TP) world models, reload, and HUD. It is meant to be portable to another game/engine with analogous concepts (authoritative server, client presentation, hitscan, inventory).

**Primary code locations**

| Area | Main types / files |
|------|-------------------|
| Host fire + hitscan + reload | `Code/Weapons/ThornsWeapon.cs` |
| Combat tuning (record) | `Code/Combat/ThornsWeaponDefinitions.cs` |
| Recoil + bloom (host) | `Code/Combat/ThornsWeaponRecoilSolve.cs` |
| Item ↔ asset paths | `Code/Inventory/ThornsItemRegistry.cs` |
| Hotbar + equip sync | `Code/Inventory/ThornsHotbarEquipment.cs` |
| Eye + aim checks | `Code/Combat/ThornsCombatAuthority.cs` |
| Visual camera kick (owner) | `Code/Multiplayer/ThornsPawnMovement.cs`, `ThornsPawnCamera.cs` |
| FP viewmodel | `Code/Weapons/ThornsViewModelController.cs`, `ThornsViewModelFpAnimator.cs` |
| TP world gun | `Code/Weapons/ThornsWeaponWorldVisual.cs` |
| Citizen body | `Code/Multiplayer/ThornsCitizenRig.cs`, `ThornsCitizenBodyDriver.cs` |
| Pawn hierarchy spawn | `Code/Multiplayer/ThornsGameManager.cs` (`BuildDefaultPlayerHierarchy`, `EnsureDefaultWeaponWorld`) |

---

## 1. Architecture overview

- **Single networked pawn root** (`ThornsPawn` on the same `GameObject` as movement, weapon, inventory, hotbar). Flatten prefabs so RPC ownership and transforms stay consistent.
- **Server authority**: ammo consumption, fire rate, durability, hit validation, and reload completion run on the **host** only.
- **Client intent only**: the owning client sends **fire direction** (and attack variant) via RPC; the server does **not** trust the client for the final hit ray — it rebuilds fire direction after recoil/bloom.
- **Two visual layers**:
  - **First person**: local-only, parented to **`View`** (same object as `ThornsPawnCamera` / `CameraComponent`). Not replicated as gameplay state.
  - **Third person**: child `GameObject` named `WeaponWorld` with `ThornsWeaponWorldVisual` — visible to **other** players; transform must be correct on every peer (including the owner’s hidden copy) for replication.

---

## 2. Data model: weapon stats vs item assets

### 2.1 `ThornsWeaponDefinitions.WeaponDefinition` (C# `record`)

Authoritative **gameplay** tuning: damage, fire interval, clip, reload time, max range, ammo type, durability, headshot multiplier, pellet count / spread, and **recoil/bloom** fields. See `ThornsWeaponDefinitions.cs` for the full parameter list.

Notable fields:

- **`FireIntervalSeconds`**: minimum time between accepted shots (host).
- **`FireMode`**: string label (`semi`, `auto`, `melee`, …) — used for UI/anim graph enum (`FiringModeGraphValue`) and input (auto fire loop on client).
- **`PelletCount` / `PelletSpreadHalfAngleDegrees`**: shotgun-style multi-ray; `1` = single hitscan ray.
- **Recoil / bloom** (server applies to **fire direction**):
  - `RecoilPatternScaleDegrees`, `RecoilResetDelaySeconds`, `RecoilPatternClampEnd`
  - `BloomHalfAngleDegreesBase`, `BloomHalfAngleDegreesPerSprayShot`
  - `AdsRecoilMul`, `AdsBloomMul`, `MovingRecoilMul`, `MovingBloomMul`, `CrouchRecoilMul`, `CrouchBloomMul`
  - `ClientVisualKickScale`: scales **client-only** camera kick relative to the same pattern step used on the server.
- **Reload**:
  - Mag-style: one delay (`ReloadTimeSeconds`) then fill from reserve up to clip.
  - Shell-style: `ReloadIsPerShellCycle`, `ReloadShellCountPerRpc`, repeated gates per shell; `ShellReloadGameplayGateSeconds` for known shotguns with stale large reload times in defs.

### 2.2 `ThornsItemRegistry.ThornsItemDefinition`

Per **item id** (e.g. `m4`, `mp5`): ties **inventory item** to:

- `CombatWeaponDefinitionId` — key into `ThornsWeaponDefinitions.Get`
- `ViewModelAsset` — FP model path (`v_*.vmdl`)
- `WorldModelAsset` — TP model path (`w_*.vmdl`)

Stock Facepunch paths are centralized as constants on `ThornsViewModelController` (e.g. `M4FirstPersonViewmodelPath`, `M4WorldModelPath`). FP vs TP: **`v_`** = first person, **`w_`** = third person (see [sboxweapons](https://sbox.game/facepunch/sboxweapons)).

---

## 3. Host combat pipeline (fire)

Implemented in `ThornsWeapon.RequestFire` (`[Rpc.Host]`).

**Guards (reject → `RpcFireOutcome` with ammunition expended false)** include:

- Caller owns pawn (`ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot`)
- Alive, normalized direction, **`ThornsCombatAuthority.TryGetAuthoritativeEye`** + **`IsDirectionWithinAimTolerance`** vs `AimDotMin` (~`0.55f`), eye origin sanity (`IsOriginPlausible`), inventory/hotbar rows, weapon item resolve, melee vs ranged rules, fire-rate cooldown, ammo > 0, not broken, reload/pump gates.

**On acceptance**:

1. Decrement ammo (host inventory slot), apply durability loss; update fire-rate gate.
2. **Non-melee**: call `ThornsWeaponRecoilSolve.SolveAuthoritativeFireDirection` with host-held recoil state (`_hostRecoilLastShotTime`, `_hostRecoilPatternIndex`, `_hostRecoilSprayOrdinal`) and stance flags (ADS, moving planar speed threshold ~55, crouch from `ThornsVitals`).
3. **Hitscan**: trace from eye position along solved direction; optional penetration past non-damage geometry; headshot rules in `ThornsCombatAuthority.TryHeadshotFromTrace`; damage via `ThornsHealth`.
4. **`RpcFireOutcome`** to **owner** only: ammo consumed flag, damage summary, FP presentation kind, **kick as integer millidegrees** (pitch/yaw) — ints used because trailing floats were unreliable on some Rpc paths.

**Pellets**: multiple traces with `HostSamplePelletDirection` around `fireDir` for pellet weapons.

**Melee**: separate branches / FP presentation kinds (`b_attack` vs heavy).

---

## 4. Recoil, bloom, and camera kick

### 4.1 Server (`ThornsWeaponRecoilSolve`)

- Per-weapon **spray pattern** tables (`Vector2`: x = yaw, y = pitch step units) in `_patternByWeaponId`; multiplied by `RecoilPatternScaleDegrees` and stance multipliers.
- **Bloom**: small random cone on top of pattern; bloom ordinal increments per shot; reset when gap > `RecoilResetDelaySeconds`.
- Outputs **final hitscan direction** — clients never compute this for hits.

### 4.2 Client visual kick (`ThornsPawnMovement`)

- RPC delivers pitch/yaw kick (millidegrees → degrees).
- **`OwnerApplyMomentaryWeaponRecoil`** queues pending pitch/yaw; **`IntegrateSmoothWeaponRecoil`** blends into `_look` each frame using **`WeaponRecoilSmoothRate`** (exponential decay style — avoids one-frame teleport).
- **`ThornsPawnCamera`** applies pitch from `LookAngles` to the **View** object (local rotation around right axis).

Crosshair UI is **not** driven by bloom — bloom only affects **server rays**.

---

## 5. Reloading

- **`RequestReload`** (`[Rpc.Host]`): validates slot, weapon def, not melee (ignored), not broken, not already reloading, clip not full, reserve ammo exists; sets `_hostReloadInProgress` (+ shotgun pump session flag when using shell cycle).
- **`HostReloadAsync`**: 
  - **Mag**: single `ReloadTimeSeconds` wait, then move bullets from reserve (`AmmoTypeId` match) into clip up to `ClipSize`.
  - **Shell cycle**: repeated waits using `ShellReloadGameplayGateSeconds` / per-shell `ReloadTimeSeconds`, add `ReloadShellCountPerRpc` shells per iteration until full or dry.
- Cancels if hotbar changes, weapon instance id changes, death, etc.
- **`PushWeaponHudToOwnerHost`** → **`ClientReceiveWeaponHudState`** (`[Rpc.Owner]`): loaded ammo, reserve, broken, reloading, shotgun pump HUD flags.

Equip / death clears reload state via `HostResetCooldownAfterWeaponEquip`, `HostClearEquipmentAfterDeath`, `HostOnSelectedNonWeapon`.

---

## 6. Equipment & hotbar sync

- **`ThornsHotbarEquipment.RequestSelectHotbarSlot`** (`[Rpc.Host]`): server selects row, resolves weapon item, sets **`ObserversCombatWeaponDefinitionId`** (`[Sync( SyncFlags.FromHost )]`) for **everyone** (TP pose / mirror).
- **`PushEquipmentToOwner`** → **`ClientReceiveEquipmentState`** (`[Rpc.Owner]`): owner-only FP mirror — calls **`ThornsWeapon.ApplyOwnerEquipmentPresentation`** (spawn/update FP viewmodel path, combat id mirror).
- **`ThornsWeapon.HostApplyEquippedWorldPresentation`**: host sets TP **SkinnedModelRenderer** model from item def’s **world** asset (logging only path name); **scale** is owned by **`ThornsWeaponWorldVisual`** on each peer.

---

## 7. First-person viewmodels

### 7.1 Controller (`ThornsViewModelController`)

- Child under **`View`**: typically `WeaponViewmodel` with **`SkinnedModelRenderer`**, optional **`ThornsViewModelFpAnimator`** for stock Facepunch graphs.
- **Human arms**: separate `SkinnedModelRenderer` child with **`BoneMergeTarget`** = weapon skin — Facepunch FP convention (arms merged **onto** weapon): see [first-person weapons doc](https://sbox.game/dev/doc/assets/ready-to-use-assets/first-person-weapons).
- Inspector tuning: grip position/euler, presentation offset, ADS forward pull (`ViewModelAdsForwardOffset`), overlay pass, anim graph toggle.

### 7.2 Animator (`ThornsViewModelFpAnimator`)

- Drives anim graph parameters: `b_attack`, `attack_hold`, `ironsights`, `firing_mode`, reload sequences / shotgun shell impulses, sprint for knives, etc.
- **`OwnerNotifyServerConfirmedFire`**: pulses **`b_attack`** when server confirms ammo spent (`RpcFireOutcome`).
- **ADS vs hip**: graph `ironsights` or DirectPlayback sequences depending on flags.
- **Recoil animation softness**: `IronsightsFireScaleWhileAds`, `HipCameraRotationScale`, `AdsCameraRotationScale` → map to graph params `ironsights_fire_scale`, `camera_rotation_scale` (see Facepunch FP weapons documentation).

### 7.3 Input → fire RPC

Local owner (`ThornsWeapon.OnUpdate`): reads combat id from mirror, auto vs semi, sends **`RequestFire`** with **camera-forward-derived direction** (intent only). Actual hits come from host trace after recoil solve.

---

## 8. Third-person world weapon

- **`WeaponWorld`** child on pawn root; component **`ThornsWeaponWorldVisual`** (`Order( 200 )` — after body/movement).
- **`ObserversCombatWeaponDefinitionId`** selects which **`w_*.vmdl`** to load; **`ThornsWeaponResourceLoad`** may substitute dev box on failure (tint orange).
- **Scale**: `ThornsWeapon.WorldMeshLocalScaleWeapon` / `WorldMeshLocalScaleWeaponLoadFailed` (defaults tuned in code).
- **Placement**:
  - **Manual (current default)**: parent to Citizen **`Body`** via `ThornsWeapon.ParentWorldWeaponToCitizenRig` with **`TpWeaponManualLocalPositionRelBody`** / rotation / fallback if no Body — defaults in code match inspector group **Third-person manual (vs Body)** (project uses e.g. `(12, -8, 32.5)` for bind pose vs Citizen torso).
  - **Optional bone attach**: `AttachWorldWeaponToCitizenRightHandBone` + `TryAlignThirdPersonWeaponToCitizenRightHand` using **`SkinnedModelRenderer.TryGetBoneTransform`** on Body skin — candidate bone names in `CitizenTpWeaponRightHandBoneCandidates`.
- **Hide for owning player**: `HideForOwningPlayer` disables TP renderer locally but **still applies mesh/scale** so networked transforms stay correct for remote viewers.
- **`AlignWorldWeaponToBody`** each frame; **`ParentWorldWeaponToCitizenRig`** avoids redundant `SetParent` every frame (prevents scale blow-ups); **re-applies** local pose when parent is stable.

Citizen rig notes: [Citizen characters](https://sbox.game/dev/doc/assets/ready-to-use-assets/citizen-characters) (bones, IK, units).

---

## 9. HUD & UX mirrors

- Weapon HUD numbers come from **`ClientReceiveWeaponHudState`** (loaded / reserve / broken / reloading / pump).
- Equipment labels use owner RPC + inventory mirrors (`ThornsDebugHudHost` etc.) — not authoritative for combat.

---

## 10. Porting checklist for another project

1. **Authority**: single connection owns pawn; host validates fire/reload/inventory mutations.
2. **Separate intent from outcome**: client sends aim intent; server computes final shot vector (recoil/bloom).
3. **Split definitions**: static combat tuning vs item/asset registry (FP + TP paths).
4. **Owner-only cosmetic RPCs**: fire confirmation, HUD, FP equip; use **reliable numeric types** for small floats if RPC serialization is flaky.
5. **TP weapon**: replicate presentation consistently on all peers; don’t skip transform updates on the owning client just because the mesh is hidden.
6. **FP**: camera on pawn `View`; weapon under same transform family as camera; stock Facepunch graphs expect documented parameters.
7. **Reload**: distinguish magazine vs tube/shell loops with explicit state machines and cancellation rules.

---

## 11. Related external docs

- [First-person weapons (Facepunch)](https://sbox.game/dev/doc/assets/ready-to-use-assets/first-person-weapons) — anim parameters, bonemerge direction, `camera_rotation_scale`, `ironsights_fire_scale`.
- [Citizen characters](https://sbox.game/dev/doc/assets/ready-to-use-assets/citizen-characters) — skeleton / IK context.
- [sboxweapons collection](https://sbox.game/facepunch/sboxweapons) — asset packs (`v_` / `w_`).

---

*Generated from the Thorns codebase as a migration/porting aid; update this file when behavior changes.*
