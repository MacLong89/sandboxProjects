# Offshore Fishing

Cozy single-player 2D side-on fishing incremental for s&box.

## Play

1. Open `offshore_fishing.sbproj` in the s&box editor.
2. Play startup scene `scenes/game.scene`.
3. Controls:
   - **A/D** walk on dock / steer boat
   - **E** shop (near cabin) or board/disembark boat
   - **LMB** charge cast, hook bite, hold to reel
   - **R** open shop from dock / alternate reel
   - **Tab** fish log
   - **Esc** pause / close panels

## Architecture

- `Code/Core` — engine-free simulation (state, fishing, economy, save DTOs, content)
- `Code/Sbox` — input, save (`FileSystem.Data`), sprites, camera, audio hooks
- `Code/UI` — Razor HUD / shop / catch reveal
- `Assets/textures/art` — pixel sprites (true alpha cutouts)
- `Assets/sounds` — generated WAV beds
- `Tools/ArtPipeline` — sprite generation + alpha validation
- `Tools/AudioPipeline` — SFX generation
- `Tools/CoreHarness` — headless smoke + balance sim

## Art pipeline

```powershell
python Tools/ArtPipeline/generate_sprites.py
python Tools/ArtPipeline/import_hero_assets.py
python Tools/ArtPipeline/validate_alpha.py
python Tools/AudioPipeline/generate_sfx.py
```

## Balance / tests

```powershell
dotnet run --project Tools/CoreHarness/CoreHarness.csproj -c Release
```

In-game console: `offshore_balance 300`
