# Thorns Terrain Prototype

Heightmap-driven terrain sculpting for s&box — **no procedural noise generation**.

## Quick start

1. Place your source DEM/heightmap at `Assets/map/co_height.png` (grayscale: black = low, white = high).
2. Open the project in s&box and run `scenes/thorns_terrain.scene`.
3. The **Thorns Terrain Bootstrap** component generates terrain on play (host-authoritative).

## Pipeline

1. **Import** — Load `co_height.png` from content.
2. **Crop** — Deterministic seed picks a ~15–35% sub-region with best contrast/traversal score.
3. **Stylize** (preserves drainage & macro forms):
   - Micro-noise reduction
   - Plains smoothing
   - Valley widening
   - Mountain exaggeration
   - Ridge sharpening
   - Cliff exposure
4. **Sea level** — Lowlands become lakes/ocean basins.
5. **Materials** — Grass / dirt / rock splats from height & slope.
6. **Water sheet** — Flat `models/dev/plane` at sea level with `thorns_terrain_water.vmat` (not painted on terrain).

## Water sheet (manual setup)

Runtime object: **`Thorns Water`** — child of **Thorns Terrain World**, created by `ThornsWaterSheet` when `CreateWaterSheet` is true.

| What | Path |
|------|------|
| Plane mesh | `models/dev/plane.vmdl` (`ThornsTerrainConfig.WaterPlaneModel`) |
| Stylized material | `Assets/terrain_materials/thorns_terrain_water.vmat` → `shaders/thorns_water.shader` |
| Complex fallback | `Assets/terrain_materials/thorns_terrain_water_fallback.vmat` |
| Albedo (optional) | Drop `Assets/terrain_materials/water.png`, then compile `terrain_materials/water.vtex` |
| Config override | **Thorns Terrain World** → `ThornsTerrainConfig.WaterSurfaceMaterial` |
| Per-object override | **Thorns Water** → `Thorns Water Surface` → **Material Override Path** |

**Pink/error water** usually means the `.vmat` / `.vtex` were never compiled: open each asset in the s&box Asset Browser and **Recompile**. Until `water.png` exists, vmats use `materials/default/default_color.tga` plus blue tint from `ThornsWaterSurface`.

## Tuning

Select **Thorns Terrain World** in the scene and edit `ThornsTerrainConfig` on the bootstrap component, or adjust defaults in `Code/Terrain/ThornsTerrainConfig.cs`.

Key knobs: `WorldSeed`, `SeaLevelNormalized`, `VerticalExaggeration`, stylization strengths, `TerrainResolution` (power of two).

## Materials

Terrain materials live in `Assets/terrain_materials/` as `.tmat` files (max 4 per terrain). Recompile assets after editing.

## Architecture

| Module | Role |
|--------|------|
| `HeightmapLoader` | PNG/DEM import |
| `RegionCropSelector` | Gameplay-readable crop selection |
| `TerrainSculptPipeline` | Ordered stylization passes |
| `TerrainMaterialPainter` | Control map from height/slope/water |
| `ThornsTerrainBootstrap` | Runtime apply to `Sandbox.Terrain` + `TerrainStorage` |
| `TerrainChunkSampler` | Deterministic world height queries (chunk-safe) |

## Multiplayer

With `HostAuthoritative` enabled, only the host generates; clients should share the same seed/config for identical worlds (networked storage sync can be added later).

## Day / night cycle (celestial)

| Component | Object | Role |
|-----------|--------|------|
| `ThornsCelestialSystem` | Sun | Sole authority: replicated `TimeOfDay01` (0=midnight, 0.25=sunrise, 0.5=noon, 0.75=sunset), sun/moon, procedural sky shader, clouds, stars, ambient, fog tint |
| `ThornsCelestialSprites` | Main camera (auto) | Optional soft sun/moon billboards |
| `ThornsAtmosphere` | Thorns Atmosphere | Gradient fog color driven from celestial; artist sliders for grade/haze |

Sky material: `materials/skybox/thorns_sky_celestial.vmat` (`thorns_celestial_sky.shader`). No HDRI/cubemap sky.

Console: `set_time 0.5`, `freeze_time 1`, `sky_debug 1`. Only time is replicated; all visuals are evaluated locally.
