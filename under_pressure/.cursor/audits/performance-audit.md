# Game Performance Audit — Under Pressure

**Scope:** `C:\Users\Macra\Projects\sandboxProjects\under_pressure` (read-only). No profiler run; no timings invented. Claims tagged **verified** (from code), **inferred** (reasonable from structure), or **needs profiler**.

**Campaign context (verified):** 25 jobs; levels 1–3 authored; levels 4–25 are empty `StubSite` pads. Authoring will grow panels/enemies/decor into systems that already show scale cliffs.

---

## 1. Executive Summary

Architecture is mostly sound for a single-player cleaner: dirty panels use runtime mask textures instead of thousands of tile GameObjects; spray droplets are pooled; scenery shares `MeshPrimitives` boxes. The live frame cost is dominated by **cleaning CPU + GPU texture uploads**, **2–3 Scene.Trace rays/frame** (washer aim + use + van), **HUD rebuild churn** (water/progress + pest LINQ), and **draw-call/entity count** from horizon + fence/house composites. Job transitions destroy/rebuild the whole site — acceptable today, spike-prone as content grows. Networking is a solo lobby only.

---

## 2. Performance Score: **64 / 100**

| Band | Meaning |
|------|---------|
| 80–100 | Production-ready at intended scale |
| 60–79 | Playable; clear wins before content expansion |
| 40–59 | Systemic hotspots under light load |
| &lt;40 | Unshippable without major rework |

**64** reflects good core design with several medium–high risks that will worsen as levels 4–25 gain panels, pests, and combat density. Score is structural, not measured FPS.

---

## 3. Critical Bottlenecks

### C1 — Large runtime grime masks + per-frame `CleanAt` + `Texture.Update`
- **System:** `CleanableSurface`
- **Evidence:** `MaskDensity = 1.2`, `MaskCap = 512` (`CleanableSurface.cs`). Level‑1 driveway `320×520` → **384×512 = 196,608 texels/layer** (verified from catalog + formula). Each layer allocates `float[] Clean`, `Color32[] Pixels`, GPU texture; optional underlay for secrets. `CleanAt` nested loops + `MathF.Sqrt` falloff + `UploadRegion` every successful spray tick.
- **Current risk:** High while spraying large panels (L1–L3).
- **Scaled risk:** Critical if multi-layer follow-ups + many large panels co-exist.
- **Claim type:** verified structure; **needs profiler** for ms/frame and GPU upload cost.
- **Est. gain if fixed (density 0.6 or soft cap 256, dirty-rect coalesce):** **~30–50% of cleaning-path CPU / upload bandwidth** — estimate; assumes upload+loop dominate spray frames.

### C2 — Dual raycasts every gameplay frame (washer) + third from van
- **System:** `PressureWasher`, `Van`
- **Evidence:** `UpdateAim` always traces 600u; `DoUse` traces again when M1 held; `Van.UpdateInteraction` traces every frame when UI not blocking (`PressureWasher.cs`, `Van.cs`).
- **Current risk:** Medium–high (3 traces on busy frames).
- **Scaled risk:** High with denser static geometry / more dynamic colliders.
- **Claim type:** verified; cost **needs profiler**.
- **Est. gain (merge aim+use, van every 2–3 frames or distance gate):** **~0.1–0.5 ms/frame** typical for short traces — rough estimate only.

### C3 — Job load full teardown/rebuild spike
- **System:** `JobSiteManager.LoadJob` → `JobWorldBuilder` / `WorldMapBuilder` / `Scenery`
- **Evidence:** `_root?.Destroy()` then rebuild terrain, 28-segment horizon (up to ~3 buildings/segment), trees/houses/fences, panels with bake+`Texture.Create`, enemies, pedestrians.
- **Current risk:** Medium hitch on depart / `up_level`.
- **Scaled risk:** Critical as authored jobs densify.
- **Claim type:** verified; hitch length **needs profiler**.

---

## 4. High Impact Optimizations

| # | Change | Evidence | Risk now → scaled | Type | Est. gain (assumptions) |
|---|--------|----------|-------------------|------|-------------------------|
| H1 | Cap mask texels more aggressively / lower density for ground pads; keep walls finer | `MaskDensity`/`MaskCap`; L1 196k texels | High → Critical | verified | **25–40%** clean-loop+upload if texel area halves |
| H2 | Cache `List&lt;CleanableSurface&gt;` on job load; stop `Scene.GetAllComponents&lt;CleanableSurface&gt;()` in `Enemy.NearestSurface` / `SplatterBloodAt` | `Enemy.cs`, `CleanableSurface.SplatterBloodAt` | Med → High | verified | **Major** on re-soil ticks if panel count grows (O(enemies×panels) → O(1)/nearest) |
| H3 | HUD: stop rebuilding on every water % tick; sample water/stamina at 5–10 Hz; cache `ActivePests()` | `Hud.razor` `BuildHash` + `PestSummary` → LINQ `GroupBy/OrderBy/ToList` | High → High | verified | **Large UI CPU cut while spraying**; needs profiler |
| H4 | Coalesce texture uploads (dirty flag / max 1 upload per layer per frame) | `UploadRegion` each `CleanAt`/`Resoil` | Med → High | verified | **10–30%** GPU/CPU on heavy spray if multiple uploads coalesce |
| H5 | Horizon / perimeter: instancing, LOD, or fewer segments; fence as single mesh not N pickets | `WorldMapBuilder` 28 segs; `Scenery.BuildFence` ~length/26 boxes | Med → Critical | verified entity explosion | **Draw-call heavy**; est. **halve scenery draw calls** on dense themes if batched |

---

## 5. Medium Impact Optimizations

| # | Change | Evidence | Type |
|---|--------|----------|------|
| M1 | Single shared aim ray for washer + van focus (or van distance check before trace) | Dual/triple traces | verified |
| M2 | Spray FX: fewer spheres (46 enabled objs) or GPU particles | `StreamCoreSegments=20`, outer 12, splash 14 | verified; visual quality tradeoff |
| M3 | `CheckDiscoveries` / `RegionRevealProgress` only when texels in discovery AABB changed | Called every `CleanAt` | verified |
| M4 | `LayerHasWork` full texel scan for follow-up layers — track dirty “has unlocked work” counters | `CleanableSurface.LayerHasWork` | verified |
| M5 | `GraffitiRaster.GlyphRows` / `Rows` allocate per glyph at bake — static glyph table | `GraffitiRaster.cs` | verified (startup only) |
| M6 | Autosave off main thread or defer when spraying (`AutosaveInterval=20`) | `GameCore.OnUpdate` + `SaveManager.Save` JSON | verified; hitch **needs profiler** |
| M7 | `CitizenHumanoid.CreateBoneObjects = true` — disable if unused | `CitizenHumanoid.TrySetup` | verified; cost **needs profiler** |
| M8 | `Scratch` realloc when brush AABB size changes — pool max-size scratch | `UploadRegion` | verified |
| M9 | `ToolMatchesJob` / `HasWorkFor` / `RequiredTools` LINQ — cache per job/equip change | `JobSiteManager`, Hud `MissingTool` | verified |
| M10 | `VisualGrade.EnsureCameraPostProcessing` scans all cameras every frame | `VisualGrade.OnUpdate` | verified (cheap if 1 cam) |

---

## 6. Low Impact Optimizations

- Prefetch `MeshPrimitives` / `GameMaterials` at boot (lazy loads today) — verified.
- `EnemyManager.ActiveCount` `RemoveAll` on property get — prefer prune in update only — verified.
- String `FormatCash` / `ToString` during HUD rebuild — inferred small; worse if rebuild every frame while spraying — needs profiler.
- `BuildMaskQuad` index `foreach (new[]{…})` allocates once per layer at setup — verified, negligible.
- Mid-job rival spawn / respawn lists already light — verified.

---

## 7. Memory Concerns

| Item | Evidence | Est. size (inferred from sizes) | Type |
|------|----------|--------------------------------|------|
| Per-layer CPU buffers | `float` + `Color32` × texels | L1 driveway layer ≈ 196k×(4+4) ≈ **1.5 MB** CPU + GPU texture (+ underlay if secrets) | verified counts; byte math inferred |
| Multi-layer panels | `Stages()` + follow-up | 2× above when follow-up used | verified |
| Scratch realloc | `new Color32[w*h]` on size change | Churn / fragmentation under spray | verified |
| Horizon boxes | 28×(1–3) buildings + windows | Hundreds of GameObjects + renderers | verified loops |
| Fence pickets | `length/26` boxes × rails/posts | Long industrial fences → hundreds of boxes | verified |
| Citizen skinned + bones | up to 10 peds (UrbanPlaza) + humanoid pests | High per-agent memory vs box pests | verified density + setup |
| Job destroy | full graph teardown | Transient peaks on load | verified |

**Leak risk:** Event handlers on surfaces are wired once per load and die with `_root.Destroy()` — **inferred OK**. Sound handles stopped on destroy — verified. No obvious unbounded list growth beyond `_discoveryQueue` (bounded by content).

---

## 8. CPU Concerns

**Per-frame (gameplay, UI open off):**
1. `PressureWasher.OnUpdate` — meters, aim ray, optional use ray + clean loops + 46 droplet transforms — verified.
2. `Van` interaction ray — verified.
3. Each `Enemy.OnUpdate` — move, timers, optional `NearestSurface` scan on re-soil — verified.
4. Each `AmbientPedestrian` + `CitizenHumanoid` anim — verified.
5. `Hud` `BuildHash` — may rebuild UI when water/progress/pests change — verified.
6. `GameCore` — idle income, completion, departure, autosave timer — verified light except save.
7. `VisualGrade` — hash + optional camera scan — verified.

**Hot cleaning path detail (verified):** For each texel in brush AABB: falloff (`Sqrt` if round), active mask, prior-layer checks, alpha write; then full-region copy to scratch + `Texture.Update`.

**Nested cost:** N pests × `GetAllComponents&lt;CleanableSurface&gt;()` on re-soil — verified algorithmic issue even if N is small today (3 rats on L2/L3).

---

## 9. Network Concerns

- **Verified:** `GameManager` creates lobby `MaxPlayers = 1`. No entity replication of cleaning masks, pests, or wallet in game code.
- **Leaderboard / Stats:** `LeaderboardService` increments/flushes lifetime earnings — infrequent, not per-frame — verified.
- **Scaled:** If multiplayer is ever added, mask dirt state and clean deltas would be the #1 bandwidth problem — **inferred** from architecture; not present now.
- **Risk now:** Low. **Scaled MP:** Critical design gap.

---

## 10. Rendering Concerns

| Concern | Evidence | Type |
|---------|----------|------|
| Many unique GameObject boxes (no batching assumed) | `Scenery.Box`, horizon, fences | verified count sources; batching **needs profiler / GPU counters** |
| Translucent grime layers + underlay | `grime_fade.vmat` copies per layer | verified; overdraw **needs profiler** |
| Spray spheres translucent | 46 water spray meshes | verified |
| Post FX bloom + color grade | `VisualGrade`, camera `EnablePostProcessing = true` | verified |
| ZFar 12000 + huge map (7200 default) | `PressurePlayer`, `GameConstants.DefaultMapSize` | verified; fill-rate **needs profiler** |
| Skinned citizens | animgraph + bone objects | verified |

Shared `MeshPrimitives.Box/Quad/Sphere` is good (verified). Problem is instance count, not unique mesh count.

---

## 11. Allocation Concerns

| Hotspot | When | Type |
|---------|------|------|
| `EnemyManager.ActivePests()` LINQ + `ToList` | Every HUD rebuild / hash that touches pests | verified |
| `PestSummary` / `PestSummaryHash` / markup foreach | Multiple evaluations possible per rebuild | verified / inferred multiple calls |
| `Hud` cash/progress strings | Each rebuild | verified |
| `UploadRegion` scratch resize | Brush size / edge changes | verified |
| Panel setup: large arrays + textures + `Material.CreateCopy` | Job load | verified |
| Glyph bake `new int[]` per character | Panel setup with graffiti/secrets | verified |
| `RequiredTools` / `Stages()` allocate lists | HUD / tool checks | verified |
| Autosave `Json.Serialize` | Every 20s | verified |

GC pressure while **holding spray** + **HUD refreshing water** is the main live concern — **inferred**; **needs profiler** (allocation timeline).

---

## 12. Scalability Forecast

| Scale factor | Bottleneck prediction |
|--------------|----------------------|
| More / larger panels | Mask memory + `CleanAt` + uploads (C1) |
| Multi-layer (squeegee) jobs | 2× textures + `PriorLayersClean` / `LayerHasWork` scans |
| More pests | Re-soil `GetAllComponents`, HUD LINQ, skinned humanoids |
| More pedestrians | Citizen anim CPU (already capped 2–10 by theme) |
| Larger maps / denser scenery | Draw calls, load hitch (C3) |
| Combat jobs (`IsCombatLevel`) | Same AI path; more traces/hits |
| Multiplayer | Unimplemented; masks would dominate bandwidth |
| Many players | N/A today |

**Stub levels 4–25:** empty pads hide cost; first dense authored mid-game job will surface C1+C3+H5.

---

## 13. Prioritized Optimization Roadmap

### P0 — Before authoring large mid-campaign sites
1. **Surface registry** on `JobSiteManager` (pass into enemies / blood splatter) — removes `GetAllComponents` scans. *(verified issue)*  
   - Est.: eliminates O(panels) per re-soil; **high** at 20+ panels.
2. **Mask budget policy** — lower density for ground, keep walls higher; or soft max ~256 unless marked “detail”. *(verified 196k texel driveway)*  
   - Est.: **~2× fewer** clean texels → proportional CPU/GPU on spray.
3. **HUD dirty flags** — rebuild on discrete events; throttle water bar; cache pest summary until `_active` changes. *(verified BuildHash churn)*  
   - Est.: **large UI savings** while spraying; measure with UI profiler.

### P1 — Same milestone / polish
4. Merge washer aim+use rays; rate-limit van ray.  
5. Upload coalesce / dirty rect merge per layer per frame.  
6. Fence/horizon batching or simplified meshes.  
7. Move autosave off spray frames / async write.

### P2 — Content & FX
8. Reduce spray sphere count or particle system.  
9. Optional bone objects off; simpler ped LOD beyond roam radius.  
10. Static glyph atlas for graffiti bake.  
11. Cache `ToolMatchesJob` / required tools.

### P3 — Future systems
12. Streaming/LOD for map ring.  
13. If MP: delta-encoded mask patches, not full textures.

### Validation plan (**needs profiler**)
- Frame time while spraying L1 driveway vs idle.  
- GPU: `Texture.Update` frequency/size.  
- Draw calls / triangles on L2 GasStation vs StubSite.  
- Allocations/sec with HUD open vs closed while spraying.  
- Job transition hitch `LoadJob` wall time.

---

## Hot-path notes (requested systems)

| System | Verdict |
|--------|---------|
| **PressureWasher** | Pooled FX good; dual rays + full droplet update every spray frame — high. |
| **CleanableSurface** | Correct architecture; texel budget + upload + discovery scans — critical. |
| **GraffitiRaster / SecretRaster** | Bake-time CPU; circle stamps O(radius²); glyph allocs — low at runtime, med at load. |
| **EnemyManager** | Light update; `ActivePests` LINQ is HUD-side cost. |
| **AmbientPedestrianManager** | Caps OK; citizen skinned cost dominates count. |
| **JobWorldBuilder** | Thin orchestrator; cost in panels + scenery. |
| **WorldMapBuilder** | Load/render heavy (horizon 28, trees, fences). |
| **Scenery / MeshPrimitives** | Shared models good; picket/house instance counts scale poorly. |
| **GameCore** | Light; autosave + job advance are spike sources. |
| **Hud.razor** | BuildHash sensitive to water/pests/blackout — primary UI cost. |

---

**Bottom line:** Ship-quality for current L1–L3 content with headroom, but **mask resolution, per-frame traces, HUD/pest LINQ, and box-scene entity counts** are the cliffs. Fix P0 before filling stub levels 4–25.