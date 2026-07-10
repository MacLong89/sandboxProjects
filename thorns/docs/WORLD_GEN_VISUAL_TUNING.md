# Thorns — World Generation Visual Tuning & QA Workflow

**Audience:** World design, level art, engineering polish  
**Phase:** Post-architecture — systems are stable; we tune *readability*, *atmosphere*, and *exploration flow*  
**Companion docs:** `THORNS_WORLD_GENERATION.md` (design authority), `Code/Terrain/WorldGen/` (pipeline)

---

## 0. Principles (polish-phase north star)

Thorns is **sparse survival geography with ~30 authored POIs**, not infinite clutter. A “good” seed is not the busiest seed — it is the seed where:

- Wilderness travel feels **lonely and risky**
- The **main city reads as the skyline anchor** from multiple approach vectors
- **Towns feel distinct** (A/B/C identity) and **isolated sites feel discoverable**
- **Roads and trails suggest intent** without becoming a highway grid
- **Biomes/regions** change mood without breaking navigation
- Players can answer: *Where am I? What’s dangerous? What’s worth the trip?*

When tuning, prefer **one lever per iteration** and keep a **seed journal** (see §5).

---

## 1. Structured world QA checklist

Run every candidate seed through this checklist. Mark **Pass / Soft fail / Hard fail** and attach screenshots (§2).

### A. Boot & console gate (30 seconds)

| # | Check | Pass criteria | Thorns signals |
|---|--------|---------------|----------------|
| A1 | World boots without stall | Host loads; player spawns | No long `ToolsStallMonitor` during worldgen |
| A2 | QA summary present | Full placement stats logged | `[Thorns WorldGen QA]` block after Phase 8 |
| A3 | Building budget | **30/30** placed (or documented exception) | `TotalBuildings=30/30` |
| A4 | City manifest | **12/12** main city | `City=12/12` |
| A5 | Town manifests | **5/5** each | `TownA/B/C=5/5` |
| A6 | Isolated | **3/3** | `Isolated=3/3` |
| A7 | Dominant failure | `MainFailureReason=None` | Not `IncompletePlacement` / heavy `Overlap` |
| A8 | Fallback rate | Skyline types mostly **compiled**, not 3×3 spam | `BuildingsOnFallback` low; blueprint valid ≫ invalid |
| A9 | Terrain validation | Rejects ≪ passes on placed footprints | `TerrainValidationPass` ≫ `TerrainValidationReject` |
| A10 | Roads registered | Corridors > 0; trails connect hubs | `RoadCorridors`, `RoadLength`; settlement log `trails=3` |

**Hard fail:** A3–A6 miss by ≥2 buildings, or city **&lt;9/12** with skyline fallbacks dominating core.

### B. Macro geography (5 minutes, elevated camera)

| # | Check | Pass criteria |
|---|--------|---------------|
| B1 | City centrality | Main city near map heart; not hugging coast/cliff |
| B2 | Town triangle | Three towns form readable triangle around city |
| B3 | Isolated spacing | Wilderness POIs not stacked; each has approach drama |
| B4 | Trail readability | Dirt roads visible on ground *and* minimap flow |
| B5 | Coast / edge | Map edge reads as ocean/barrier, not broken mesh |
| B6 | Region contrast | Forest / open / wet bands distinguishable at altitude |
| B7 | No “flat pancake” | Macro settlement bowl subtle; not obvious crater |
| B8 | No “spiky chaos” | Hills don’t fight pads; no terrain through floors (§7) |

### C. Main city — skyline & identity (10 minutes on foot + 2 minutes aerial)

| # | Check | Pass criteria |
|---|--------|---------------|
| C1 | Skyline anchor | **3 core landmarks** visible from ≥2 approach angles |
| C2 | Height hierarchy | Tallest mass at core; mid/outer rings step down |
| C3 | Core density | Plaza + towers feel **urban**, not 3 shacks in a field |
| C4 | District read | Commercial core ≠ industrial outer ≠ residential mid |
| C5 | Road frontage | Buildings face corridors; not backs to main streets |
| C6 | Walkable core | Can navigate blocks without constant mesh snag |
| C7 | Floor/terrain | No grass/rock clipping through interior floors |
| C8 | Combat sightlines | Open streets + cover alcoves; not uniform tunnel |
| C9 | Risk/reward | City feels **exposed** — sniper lines, approach vectors |
| C10 | Memorability | Screenshot at 5s glance: “that’s the main city” |

### D. Towns A / B / C (5 minutes each)

| # | Check | Pass criteria |
|---|--------|---------------|
| D1 | Identity | Town matches manifest flavor (A rural/store, B cabin/warehouse, C ruin-heavy) |
| D2 | Scale | Clearly **smaller** than city; still a “place” not a prop |
| D3 | Street logic | Main street readable; lots not random spins |
| D4 | Spacing | ~5 buildings with breathing room; not overlap soup |
| D5 | Approach | First sight from trail sells “safe-ish hub” vs wilderness |

### E. Wilderness & exploration (15 minutes travel)

| # | Check | Pass criteria |
|---|--------|---------------|
| E1 | Lonely travel | ≥2 min between POIs feels empty, not broken |
| E2 | Landmark ping | See city silhouette or minimap blip before arrival |
| E3 | Isolated discovery | Each isolated site rewards detour (silhouette, radio, military, cabin) |
| E4 | Resource rhythm | Trees/nodes not wall-blocking trails |
| E5 | Hazard read | Cliffs/wet/coast telegraph danger before commitment |
| E6 | Night/atmosphere | (When lit) POIs readable; wilderness oppressive, not flat grey |
| E7 | Loop closure | Can route city → town → town → city without dead-end frustration |

### F. Systems cross-check (console + minimap)

| # | Check | Pass criteria |
|---|--------|---------------|
| F1 | Minimap POIs | Per-building blips; no misleading hub blobs (design choice) |
| F2 | POI dataset | `POI dataset rebuilt count=30` (± loot props) |
| F3 | Scatter respect | Trees/boulders skip building pads & roads |
| F4 | Persistence | Fresh world loads; save path sane on host |

---

## 2. Screenshot & debug camera workflows

### Standard capture kit

For every seed review, capture **this set** (name files `seed####_tag.png`):

| Shot ID | Camera | Purpose |
|---------|--------|---------|
| `MAP_TOP` | High vertical (~1500–3000u above city) | Triangle of towns + trails + coast |
| `CITY_SKYLINE_N/S/E/W` | Ground ~400–800u from core, low angle | Skyline readability per cardinal |
| `CITY_CORE` | Street-level in core plaza | Density, floor clip, combat read |
| `CITY_BLOCK` | Mid-ring street | Spacing + frontage |
| `TOWN_A/B/C_ARRIVAL` | First view from trail approach | Identity + navigation |
| `ISO_1/2/3` | Each isolated site, medium-wide | Memorability |
| `WILDERNESS_MID` | Between two POIs | Loneliness / resource rhythm |
| `MINIMAP` | HUD minimap at city + mid-travel | Exploration flow |

### Camera discipline

1. **Disable motion blur / excessive FOV** for comparison shots (consistent lens across seeds).
2. **Same time-of-day** per seed batch (note time in journal).
3. **Mark north** in screenshot (sun position or compass HUD) for skyline comparisons.
4. **Two heights rule:** every POI gets *arrival* (player) + *helicopter* (layout) angles.

### In-game debug camera (s&box host)

1. Host listen server → gameplay scene.
2. Fly cam / noclip to `[Thorns Settlement]` log coordinates for city/towns.
3. Use `DrawSettlementLayoutDebug` overlays while framing aerial shots (§3).
4. Pause on `[Thorns WorldGen QA]` in console; copy block into seed journal.

### Regression compare

When changing one tuning knob, re-host **same `TerrainSeed`** before/after and only re-shoot affected shots (e.g. road change → `CITY_CORE` + trail approach shots).

---

## 3. Recommended debug overlays

Enable on the terrain system component (`ThornsTerrainSystem`):

| Property | When to use |
|----------|-------------|
| `DrawSettlementLayoutDebug` | **Primary worldgen overlay** — phases 2–8 |
| `DebugBuildingTypeColors` | **Per-type minimap + structure tint** (see below) |

### Building type debug colors (`DebugBuildingTypeColors` on terrain)

When enabled (default **on** during tuning), each proc building gets a distinct minimap blip and structure tint:

| Type | Minimap color | Label |
|------|---------------|--------|
| Skyscraper | Cyan | `Skyscraper (NF)` |
| ApartmentTower | Magenta | `AptTower (NF)` |
| OfficeBuilding | Blue | `Office (NF)` |
| Apartment | Gold | |
| Store | Green | |
| Factory | Orange | |
| Warehouse | Brown | |
| House | Tan | |
| Cabin / Barn / Ruin / Military / Radio | See `ThornsProcBuildingTypeDebugColors` | |

`NF` = story count in `DisplayName` — core skyline should show **8F / 5F / 4F**, not **1F** (fallback warning).

### What `DrawSettlementLayoutDebug` shows (by pipeline phase)

| Phase | Overlay source | What you see |
|-------|----------------|--------------|
| Select settlements | `ThornsWorldSettlementDebugViz` | City/town/isolated centers, slot hints |
| Settlement terrain | `ThornsWorldSettlementTerrainDebugViz` | Macro influence zones, slope heatmap |
| Settlement blocks | `ThornsWorldSettlementBlockDebugViz` | Districts, lots, corridors |
| Apply road terrain | `ThornsWorldRoadTerrainDebugViz` | Road corridor influence |
| Spawn buildings | `ThornsWorldSettlementPlacementDebugViz` | Green = placed, red = rejected footprints |
| Spawn buildings | `ThornsWorldSettlementTerrainDebugViz` | Local feather pads under foundations |

### Supplemental overlays (targeted)

| Tool | Use for |
|------|---------|
| `ThornsProcBuildingDebugViz` | Interior/ramp validation failures |
| `ThornsWorldSettlementPlacementDebugViz` failure colors | Overlap vs terrain reject diagnosis |
| Minimap (gameplay) | Player-facing exploration read |
| Console `[Thorns Placement]` per-zone | `Overlap` vs `TerrainCornerDelta` ratios |

### Overlay workflow (recommended session)

```
1. Enable DrawSettlementLayoutDebug
2. Host fresh seed → don't move until WorldGen QA prints
3. Aerial pass: macro zones + roads + block lots
4. Walk city core with placement footprints still visible
5. Disable overlay → capture clean beauty shots (§2)
6. Paste QA block into seed journal
```

---

## 4. Tuning iteration loops

Work in **loops**, not random knob-twiddling. Each loop has a hypothesis, one primary lever, and a seed pair.

### Loop A — Seed viability (daily)

**Goal:** Find seeds worth art-directing.  
**Lever:** `TerrainSeed` / `RandomizeSeedOnHost`  
**Process:** Run checklist §1A–A10 only → promote seeds to “candidate” or discard.  
**Exit:** ≥3 candidates per week with 30/30 placement and city ≥10/12.

### Loop B — City skyline & density

**Goal:** Main city feels like a **city**.  
**Levers (in order):**

1. `ThornsWorldSettlementPlanner` — `SpacingMultiplier` (main city zone)
2. `ThornsWorldSettlementBuildingPicker.CityRingClearanceMul`
3. `ThornsWorldSettlementBlockGenerator` — core lot radii / mid block sizes
4. Blueprint validation / fallback rate (`ThornsProcBuildingIdentityGenerator`)

**Measure:** `City=12/12`, `BuildingsOnFallback` for Skyscraper/Office/Tower, screenshots `CITY_SKYLINE_*`.  
**Exit:** Core has 3 distinct tall silhouettes; ≤2 compact fallbacks in core.

### Loop C — Settlement spacing & placement

**Goal:** Fewer overlap/terrain rejects without breaking survival sparse read.  
**Levers:**

1. `ThornsWorldGenFootprintReservation.SettlementFootprintEdgeGap` (main city multiplier)
2. Lot path `edgeGap` scale in `ThornsWorldGenSettlementPlacer`
3. `ThornsWorldSettlementTerrainValidation` thresholds (main city only)
4. Block assignment: largest-lot for core (`AssignCitySlotsToLots`)

**Measure:** `[Thorns Placement] Main City: failures={Overlap=…, TerrainCornerDelta=…}`.  
**Exit:** Overlap + corner delta failures drop **50%** vs baseline seed set.

### Loop D — Terrain blending & floor contact

**Goal:** No terrain through floors; smooth pads.  
**Levers:**

1. `ThornsWorldGenTerrainPadFactory` — apron, skirt, `PeakBlend`
2. `ThornsWorldSettlementTerrainShaping` — `CityPeakBlend` / macro bowl
3. `FillHeightmap` pad vs road order (`ThornsTerrainGeometry`)
4. Post-pad resample at spawn (`ThornsWorldGenSettlementPlacer`)

**Measure:** On-foot `CITY_CORE` + `WorstCornerDelta` in QA; visual floor clip count.  
**Exit:** Zero visible clip in 12/12 city buildings; `WorstCornerDelta` stable across seeds.

### Loop E — Roads & trails

**Goal:** Readable paths; no foliage walls on roads.  
**Levers (on `ThornsTerrainSystem`):**

- `RoadCityFlattenStrength`, `RoadTownFlattenStrength`, `RoadTrailFlattenStrength`
- `Road*EdgeFalloff`, `RoadFoliageClearanceRadius`, `RoadBoulderClearanceRadius`

**Measure:** `FoliageRoadSkips`, `BoulderRoadSkips`, `RoadLength`, trail screenshots.  
**Exit:** Trails visible from 200u; city ring roads read as streets not scars.

### Loop F — Atmosphere & biome readability

**Goal:** Wilderness mood + regional identity.  
**Levers:** Scatter densities, forest cluster noise, coastal falloff, fog/lighting (scene), region weights in macro terrain phase.  
**Measure:** `WILDERNESS_MID` shots; player survey “can you tell forest from plains?”.  
**Exit:** ≥80% informal tester agreement on region read without map open.

### Loop G — Exploration flow (playtest)

**Goal:** Tension curve city → wilderness → isolated → city.  
**Process:** 20-minute solo run per seed; note boredom, confusion, cheap deaths.  
**Exit:** Can name all 4 hub types from memory after one run.

---

## 5. Seed comparison tooling (ideas)

### Implemented today (use now)

| Tool | How |
|------|-----|
| Deterministic regen | Fixed `TerrainSeed` on `ThornsTerrainSystem` |
| QA export | Copy `[Thorns WorldGen QA]` + `[Thorns Placement]` blocks |
| Visual overlays | `DrawSettlementLayoutDebug` |
| Minimap POI blips | Per-building markers from spawn |

### Low-effort additions (recommended next)

| Idea | Value | Sketch |
|------|-------|--------|
| **Seed journal file** | `Thorns/saves/worldgen_journal.txt` — append QA block + pass/fail | Host command or post-`PublishSummary` hook |
| **Screenshot manifest** | JSON `{ seed, shots: [{id, path}], qa: {...} }` | Folder per seed under `Thorns/captures/` |
| **Candidate seed list** | `worldgen_candidates.json` with tags: `skyline_ok`, `roads_weak` | Curate 10–20 seeds for A/B |
| **Side-by-side host** | Two listen servers, two seeds, split screen | Manual but effective for art review |

### Medium-effort (engineering)

| Idea | Value |
|------|-------|
| **Batch seed runner** | Headless host N seeds → CSV of QA metrics only |
| **QA threshold gate** | Fail CI if `City < 10` or `TotalBuildings < 28` on golden seeds |
| **Heatmap export** | PNG height/slope from `ThornsWorldSettlementTerrainDebugViz` sampling |
| **Diff two specs** | Compare `Terrain replica` hash + pad count + road corridor count |

### Ambitious (production)

| Idea | Value |
|------|-------|
| **In-editor seed browser** | Grid of thumbnails + QA scores |
| **Replay ghost route** | Record ideal city→town→isolated path per seed |
| **Automated “skyline score”** | Ray samples at ring around city counting vertical mass |

---

## 6. Metrics for “good” world readability

Use **quantitative gates** to reject bad seeds fast, then **qualitative** review for keepers.

### Quantitative gates (console)

| Metric | Good | Investigate | Reject |
|--------|------|-------------|--------|
| `TotalBuildings` | 30/30 | 28–29 | ≤27 |
| `City` | 12/12 | 10–11 | ≤9 |
| `TownA/B/C` | 5/5 each | 4/5 | ≤3 any |
| `Isolated` | 3/3 | 2/3 | ≤1 |
| `BuildingsOnFallback` | 0–3 | 4–8 | ≥9 |
| `BlueprintInvalid / Valid` | invalid &lt; valid | — | invalid ≥ valid |
| `MainFailureReason` | `None` | `IncompletePlacement` | Dominant `Overlap` |
| City `Overlap` failures | &lt; 400 | 400–900 | &gt; 900 |
| City `TerrainCornerDelta` | &lt; 400 | 400–800 | &gt; 800 |
| `WorstCornerDelta` (placed) | &lt; 40 | 40–70 | &gt; 70 |
| `AvgPlacedSlope` | &lt; 12 | 12–20 | &gt; 20 |
| `RoadCorridors` | ≥18 city-heavy | 12–17 | &lt; 12 |
| `FoliageRoadSkips` | &gt; 0 (roads clearing) | 0 | — |
| `LocalFeatherPads` | ≈ placed buildings | much lower | — |

### Qualitative rubric (1–5 each, target ≥4.0 avg)

| Pillar | Question |
|--------|----------|
| **Navigation** | Can I route between hubs without map? |
| **Skyline** | Can I find the city from wilderness by silhouette? |
| **Landmark memory** | After one visit, can I describe city vs Town B vs isolated military? |
| **Atmosphere** | Does wilderness feel oppressive/sparse, not empty/broken? |
| **Survival tension** | Is travel exposure meaningful; are hubs worth risk? |
| **Combat readability** | Are sightlines fair (cover + risk) in city/town? |
| **Exploration reward** | Do detours to isolated sites feel intentional? |

**Promote seed to “golden”** if: all quantitative **Good** + qualitative avg **≥4.0** + no hard fails in §1B/C/E.

---

## 7. Common procedural-world visual failure cases

Watch for these; they map to Thorns systems and fixes.

| Failure | Symptom | Likely cause | Where to look |
|---------|---------|--------------|---------------|
| **Hollow city** | 4–8 buildings; empty blocks | Overlap / terrain rejects; smallest-lot assignment | `[Thorns Placement] Main City`, block debug |
| **Shack skyline** | Core is 3×3 boxes | Blueprint strict fail → compact fallback | `BuildingsOnFallback`, ProcBuilding logs |
| **Terrain through floor** | Grass/rock in interiors | Pad order, TargetZ mismatch, weak apron | Feather debug, `FillHeightmap` pad/road order |
| **Crater city** | Obvious circular bowl | Macro `CityPeakBlend` too strong | `ThornsWorldSettlementTerrainShaping` |
| **Floating buildings** | Gaps under foundations | `surfaceZ` vs pad TargetZ; slope rejects bypassed | Placement + pad factory |
| **Road scar** | Brown/grey stripe, no street read | Flatten too strong / turf missing | `Road*FlattenStrength`, road debug viz |
| **Foliage wall** | Trees on trail | Clearance radius too low | `RoadFoliageClearanceRadius` |
| **Hub soup** | Towns feel same | Manifest not readable; spacing identical | Town composition + screenshots |
| **Invisible trail** | Can’t find next town | Trail segments weak vs wilderness | `trails=3`, road length QA |
| **Coast trap** | City on wet/unplayable edge | Planner scoring | Settlement select phase log |
| **Overlap spam** | Log thousands of Overlap | Edge gap too large vs lot size | Footprint reservation, spacing multipliers |
| **Corner delta spam** | Terrain rejects on macro hills | Validation too tight OR macro bowl insufficient | Terrain validation + macro shaping |
| **Blueprint cascade** | One fail → all fallbacks | Ramp/shaft strict rules | `ThornsProcBuildingStrictValidation` logs |
| **Wrong hub dots** | Minimap lies | Hub markers vs per-building POIs | `ThornsProcBuildingSceneSpawner` minimap |
| **Stall on host** | Hitch during gen | Heavy scatter / validation | Phase timing in log |
| **Non-memorable isolated** | “Another shed” | Fallback + weak silhouette | Isolated type + approach shot |
| **City not center** | World feels off-balance | `PickCityCenter` / map radius | Phase 2 log coordinates |

---

## 8. Quick reference — Thorns worldgen phases

```
 1  MacroTerrain
 2  SelectSettlementLocations
 3  SettlementTerrain          ← macro bowls + slope heatmap debug
 4  RoadNetwork
 5  SettlementBlocks           ← lots / districts debug
11  ApplyRoadTerrain           ← road corridor debug
 6  ReserveBuildingFootprints
 7  GenerateBuildingLayouts
 8  SpawnBuildings             ← placement + feather pads
    [Thorns WorldGen QA]
--- chunk spawn ---
10  GenerateEnvironmentDetails
 9  GenerateLootAndProps
```

---

## 9. Session template (copy per seed)

```text
Seed: __________  Date: __________  Build: __________
RandomizeSeedOnHost: [ ]  DrawSettlementLayoutDebug: [ ]

QUANTITATIVE (paste QA block):
- TotalBuildings:
- City / Towns / Isolated:
- BuildingsOnFallback:
- MainFailureReason:
- City failures Overlap / TerrainCornerDelta:

QUALITATIVE (1-5):
- Navigation:
- Skyline:
- Landmark memory:
- Atmosphere:
- Survival tension:
- Combat readability:
- Exploration reward:

HARD FAILS (§7 tags):

SCREENSHOTS CAPTURED: MAP_TOP, CITY_SKYLINE_N/S/E/W, CITY_CORE, TOWN_A/B/C, ISO_1/2/3, WILDERNESS_MID, MINIMAP

VERDICT: [ ] Reject  [ ] Candidate  [ ] Golden
NOTES:
```

---

## 10. Ownership map (who tunes what)

| Concern | Primary knobs | Owner bias |
|---------|---------------|------------|
| Skyline / city density | Block generator, spacing, blueprint fallbacks | World design + proc buildings |
| Placement / overlap | Footprint reservation, lot assignment | Engineering |
| Terrain / floors | Pad factory, macro shaping, fill order | Engineering + tech art |
| Roads / trails | `ThornsTerrainSystem` road group | World design |
| Biome / scatter | Macro terrain + scatter densities | Environment art |
| Atmosphere | Lighting, fog, audio, weather | Art direction |
| Exploration flow | Planner distances, trail graph, POI manifests | World design |

---

*This document is a living workflow. Update thresholds when `THORNS_WORLD_GENERATION.md` constants or QA report fields change.*
