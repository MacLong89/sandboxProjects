# Combat audio baseline (pre-director)

Use this to revert combat sound behavior. Set `GameConstants.UseCombatAudioDirector = false`
and route combat calls back through `Sfx.Play` only (see git history for `CombatSystem.cs`).

## Before `CombatAudio`

### Gunfire
- **Every bullet** called `Sfx.Play` in `CombatSystem.FireBullet`.
- Shotgun recruits fired **one sound per pellet** in `DefenderManager.FireVolley`.
- Turrets fired every `FireInterval` (~0.35s) with no concurrency cap.
- No pitch variation; shotgun/turret had `ShotgunVolumeScale` (0.5) via `Sfx.VolumeScaleFor`.

### Zombie impacts
- **Every attack tick** (every `ZombieAttackInterval` ≈ 0.9s per zombie) played `Sfx.WallHit`
  for core, wall, building, recruit hit, and recruit kill — same sample for all.
- No per-target cooldown; 20 zombies on one wall ≈ many overlapping `axe.mp3` hits.
- `ZombieImpactVolumeScale` (0.5) applied in `Sfx.VolumeScaleFor` for `WallHit` only.

### Zombie kills
- **No death sound** on kill — `ZombieDeath` was defined but never played.
- `ZombieHit` (`fo_zombie_hit.sound`) was unused in combat.

### Mix
- No voice limits, no prioritization, no ducking.
- All combat SFX used `AudioSettings.EffectiveSfx` × per-path scale in `Sfx` only.

## After `CombatAudio` (current)

- Gunfire: concurrency cap, global throttle, one sound per shotgun **volley**.
- Impacts: per-target cooldown, concurrency cap, semantic sounds (wall vs recruit vs death).
- Kills: `ZombieDeath` on zombie and recruit kills (throttled).
- High-priority events (core, bomber, recruit death) punch through cooldowns.
- Set `UseCombatAudioDirector = false` to restore baseline without deleting `CombatAudio.cs`.
