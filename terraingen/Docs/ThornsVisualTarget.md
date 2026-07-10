# Thorns — Official Visual Target

This document is the **authoritative look target** for terrain, atmosphere, foliage composition, and future clutter passes. Reference artwork: painterly low-poly wilderness (cerulean sky, warm valleys, dark forest silhouettes, snow peaks).

## Art direction

| Principle | Target |
|-----------|--------|
| Style | Low-poly facets, soft cinematic lighting, dreamlike loneliness |
| Readability | Terrain sculpt always readable; foliage frames vistas, never carpets the map |
| Scale | Mountains dominate; trees feel small; atmospheric perspective on distant peaks |

## Color palette

| Role | Hex (guide) | Notes |
|------|-------------|--------|
| Sky | `#2A82D7` → `#8EC4E8` horizon | Saturated cerulean, not grey |
| Grass (sun) | `#9BC94A` / `#7BA32B` | Warm lime-yellow green |
| Grass (shade) | `#5F7A32` | Olive, cooler in shadow |
| Pine / forest | `#1B3D2F` | Deep saturated green silhouettes |
| Rock | `#4A5568` | Cool blue-grey cliff faces |
| Snow | `#F4F7FA` | Clean white caps, slight cool bias |
| Water | `#5DA9E9` | Reflective sky-blue |
| Distance haze | Cyan-blue tint | Peaks lighten and shift cool |

## Atmosphere

Banded aerial perspective — **foreground stays vivid**, distance gets blue haze (not gray global fog).

| Band | Distance | Look |
|------|----------|------|
| Foreground | 0–150 m | Minimal fog, +sat/contrast in post, warm sun, crisp shadows |
| Midground | 150–600 m | Gentle haze ramp, slight desat |
| Background | 600 m+ | Blue-shifted haze, softer contrast, mountain fade |

| Layer | Implementation |
|--------|----------------|
| **Distance fog** | `GradientFog` — starts ~150 m, steep falloff exponent, low density |
| **Valley fog** | Height fog ~120 m vertical (valleys only, not mountaintops) |
| **Sun** | Warm `LightColor`, cool `SkyColor`, low `FogStrength` (~0.38) |
| **Ambient** | Cool fill via `AmbientLight` |
| **Haze post** | World-distance bands + smooth curves in `thorns_atmosphere_haze.shader` |
| **Grade** | `ColorAdjustments` saturation 1.4, contrast ~1.05; exposure ~1.12 |

Tuned live during play on **`Thorns Atmosphere`** (properties are on the component directly, not nested) — six inspector groups:

| Group | Controls |
|-------|----------|
| **1 — Warm Sunlight** | Sun color, sun intensity |
| **2 — Cool Ambient** | Cool sky bounce, ambient fill, ambient intensity |
| **3 — Shadow Contrast** | Shadow hardness/bias, post shadow depth |
| **4 — Atmospheric Blue** | Sky tint, fog (inherits sky), haze color, distance bands |
| **5 — Color Grade** | Global sat/contrast/brightness, warm highlights, cool shadows |
| **6 — Exposure** | Base exposure, sunlit lift, shadow pull, auto exposure toggle |

Child **Thorns Atmospheric Haze Post** is driven automatically — do not edit it directly.

## Terrain materials

| Layer | Asset | Use (bottom → top) |
|-------|--------|-----|
| Grass | `thorns_grass.tmat` | Foothills and moderate slopes up to grass line (~72% above sea) |
| Dirt | `thorns_dirt.tmat` | Thin band at top of grass line (~2.2% of elev range) |
| Rock | `thorns_rock.tmat` | Mountains, cliffs, steep slopes |
| Snow | `thorns_snow.tmat` | Top 35% of elevation above sea on gentle summits; cliffs stay rock |
| Water | `thorns_terrain_water.vmat` + `thorns_water.shader` | Shoreline depth tint, soft specular, atmospheric distance fade, subtle foam — driven by `ThornsWaterSurface` |

Albedo sources: `Assets/materials/terrain_materials/*.png` (generated via `Scripts/generate_thorns_terrain_textures.py`).

Matte roughness: `*_rough.png` in `Assets/terrain_materials/` — high grayscale values so terrain reads dry/matte, not glossy.

Tint multipliers: `Assets/terrain_materials/thorns_terrain_*.vmat` (`g_vColorTint`, `g_flRoughnessScaleFactor`).

## Foliage composition (placement)

Matches ecosystem foliage system:

- Dense pockets in **valleys** and along **rivers**
- **Treeline** fade with elevation; bare rock/snow above
- **Scenic openings** — meadows, ridges, sightlines
- **Cluster rhythm** — 3–7 trees, occasional hero/lone trees
- Grass thick on meadows; trees dark against sky

## Not in scope yet (future)

- Flower clutter (white daisy patches in meadows)
- Grey boulder props on grassy slopes
- Foliage material tint pass on pine vmdl

## Terrain vertical scale

Target: **cinematic height** — readable peaks, deep valleys, sharp ridges/cliffs (not compressed).

| Knob | Default | Effect |
|------|---------|--------|
| `VerticalExaggeration` | `3.4` | Global lift from sea level (~2× prior `2.1` pass) |
| `PeakExaggerationMultiplier` | `2.45` | Extra summit lift |
| `ValleyDepthMultiplier` | `1.85` | Deeper drainage / valley floors |
| `MountainExaggerationStrength` | `2.5` | High-elevation mass |
| `RidgeSharpeningStrength` | `1.45` | Ridgeline skyline |
| `CliffExposureStrength` | `1.5` | Cliff carve + shoulder silhouettes |
| `ValleyWideningStrength` | `0.92` | Valley breadth |
| `LowlandVerticalCap` | `1.28` | Caps foothill exaggeration (prevents terracing spikes) |

Exaggeration pivots on **sea level**, not `0.5` normalized.

## Tuning knobs

| System | Where |
|--------|--------|
| Sky / fog / sun | `thorns_terrain.scene` — procedural `materials/skybox/thorns_sky_atmosphere.vmat` (no panorama seam) |
| Vertical scale | `ThornsTerrainConfig` on bootstrap |
| Terrain tints | `thorns_terrain_*.vmat`, regenerate PNGs |
| Snow line | `TerrainMaterialPainter` + height percentiles |
| Foliage density | `ThornsFoliageConfig` |
