# Night combat music loop (revert guide)

When `GameConstants.UseNightCombatMusicLoop` is **true** (default):

- During `GamePhase.Night` only, `combat.mp3` and `combat2.mp3` loop together via `MusicPlayer`.
- Most gameplay SFX is suppressed during night (gunfire, impacts, purchases).
- **Exceptions still play under the music bed:** wave start, wave clear, game over, and UI clicks
  (`Sfx.AllowedDuringNightMusic`).
- Day ambience, night recap, menu, and post-wave sounds behave as before once phase leaves Night.

## Revert

Set in `GameConstants.cs`:

```csharp
UseNightCombatMusicLoop = false;
```

Optionally remove `NightCombatMusicPlayer` from `OutpostBootstrap` — not required; it no-ops when the flag is false.

## Before this feature

- Night used layered combat SFX (`CombatAudio` director + `Sfx`) with no dedicated music bed.
- `ambience.mp3` played during Day only (`AmbiencePlayer`).
- Wave start, gunfire, impacts, etc. all played during Night.

## Files

| File | Role |
|------|------|
| `Code/Core/NightCombatMusicPlayer.cs` | Dual loop playback |
| `Code/Core/GameConstants.cs` | `UseNightCombatMusicLoop` toggle |
| `Code/Core/Sfx.cs` | Blocks most non-loop sounds at night |
| `Code/Networking/OutpostBootstrap.cs` | Spawns component |

Required assets: `Assets/sounds/fo_ambience.sound`, `Assets/sounds/fo_combat.sound`, `Assets/sounds/fo_combat2.sound` (each references the matching mp3).
