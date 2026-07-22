# Game Architecture Audit — Under Pressure

**Scope:** Entire `Code/` tree (~51 files, ~flat `UnderPressure` namespace). No code was modified.  
**Evidence classes:** **Fact** (verified in source) · **Inference** (reasoned from structure) · **Unknown** (needs runtime/tooling).

---

## 1. Executive Summary

Under Pressure is a solo s&box session built as a **runtime-composed, catalog-driven cleaning loop**: `GameManager` creates a 1-player lobby and spawns `GameCore`, which owns plain-C# systems (`SaveData`, wallet, upgrades, tools, jobs) and boots world/player/enemies/HUD. The **cleaning domain is architecturally strong** (`CleanableSurface` mask layers, tool stages, discovery hooks). The **session orchestrator is a god object**, **networking is decorative**, and the **25-level campaign is mostly narrative stubs** (3 authored sites, 22 empty pads). Persistence owns meta-progress well but **not mid-job state**. For a commercial path, the biggest risks are content scaling in C#, singleton mesh coupling, and unfinished feature contracts (`TimeLimitSeconds`, `IsCombatLevel`, hitman arc).

---

## 2. Architecture Score: **61 / 100**

| Band | Meaning |
|------|---------|
| 80–100 | Commercial-ready seams (content, net, save, UI) |
| 60–79 | Solid prototype with clear domains; scaling debt |
| 40–59 | Playable but structure fights growth |
| &lt;40 | Rewrite pressure |

**61** = good domain folders + strong cleaning core, offset by god-object session hub, fake multiplayer, content-as-code, and dead/half-wired systems.

---

## 3. Biggest Strengths

1. **Domain folder layout matches concepts** — `Cleaning/`, `Economy/`, `Enemies/`, `Persistence/`, `Progression/`, `Player/`, `World/`, `Networking/`, `UI/`. Easy to locate systems (**Fact**).

2. **Plain-C# economy/progress vs scene components** — `PlayerWallet`, `UpgradeSystem`, `ToolSystem`, `PrestigeSystem`, `JobSiteManager` take `SaveData` and stay free of `Component` (**Fact**: `GameCore.OnAwake`). Clear persistence ownership of the save blob.

3. **Cleaning as a real simulation core** — `CleanableSurface` (mask textures, stacked `CleanStage`, shape masks, blood/resoil, discoveries) is the best-designed subsystem (**Fact**: ~614 lines, event-based payouts via `JobSiteManager.WirePanels`).

4. **Data-driven catalogs** — `ToolCatalog`, `EnemyCatalog`, `UpgradeSystem.All`, `MapThemes`, `JobDef`/`PanelDef`/`DecorDef`/`EnemySpawnDef` (**Fact**). Extending pest/tool stats does not require rewriting AI.

5. **Shared world build path** — `JobWorldBuilder` used by both play (`JobSiteManager`) and `LevelViewer` (**Fact**). Art preview does not fork geometry code.

6. **Central tuning + depth convention** — `GameConstants` and `DepthLayers` reduce magic-number scatter and z-fight debt (**Fact**).

7. **Autosave + schema versioning** — `SaveManager.Migrate`, wipe-on-major-campaign-change (v6) (**Fact**). Intentional hard resets for content overhauls.

---

## 4. Biggest Weaknesses

1. **`GameCore` is a god object** (~657 lines) — owns save bootstrap, system construction, UI modal flags (~20 booleans), departure cinematic, daily/hitman/discovery/knockout flows, spawn of player/van/enemies/peds/HUD, cheat API, ambience (**Fact**: `GameCore.cs`). Violates SRP; every new overlay/feature lands here.

2. **Singleton mesh, no interfaces** — Static `Instance` on: `GameCore`, `PressurePlayer`, `PressureWasher`, `EnemyManager`, `AmbientPedestrianManager`, `HitmanBriefingNpc`, `LevelViewer` (**Fact**). Zero `interface`/`abstract` types in `Code/` (**Fact**). Hidden coupling via globals; hard to test or dual-session.

3. **Networking is not multiplayer architecture** — `GameManager` / `LevelViewer` only `CreateLobby` with `MaxPlayers = 1`. No `[Sync]`, `Rpc`, host/proxy, or networked pawn (**Fact**: grep). Lobby creation implies net without designing ownership.

4. **Campaign content unfinished & code-embedded** — Levels 1–3 authored; 4–25 are `StubSite` (empty panels/props/decor/enemies) with story strings only (**Fact**: `CampaignCatalog`). `JobDef.TimeLimitSeconds` / `IsCombatLevel` are set on stubs but **never read by gameplay** (**Fact**: only declared/assigned).

5. **Incomplete / disabled story systems still coupled** — `HitmanBriefingJobIndex = -1` disables fixer spawn, but `HitmanBriefingNpc`, gun unlock, contract enemies, HUD dialogue strings remain (**Fact**: `GameConstants`, `HitmanBriefingNpc.EnsureForJob`). Dead path maintenance cost.

6. **Save owns meta, not session** — Persists cash, upgrades, tools, job index, discoveries, prestige, dailies — **not** panel cleanliness, pest state, water/stamina, or “awaiting departure” (**Fact**: `SaveData`). Quit mid-job always reloads a clean site at `JobIndex` (**Inference** from load path).

7. **Hot-path scene scans** — `Enemy.NearestSurface`, `CleanableSurface.SplatterBloodAt` walk all `CleanableSurface` via `GetAllComponents` (**Fact**). Fine at current panel counts; scales poorly with many interactables.

---

## 5. High Priority Problems

| # | System | Evidence | Risk if left alone |
|---|--------|----------|-------------------|
| H1 | **Session god object** | `GameCore` UI state + flow + spawn + cinematics | New features (timers, acts, co-op UI) all collide; regressions in modal/input (`IsUiBlocking` / `IsWorldFrozen`) |
| H2 | **Campaign scale as C# megadefs** | `CampaignCatalog` ~508 lines; 22 stubs; helpers `FlatGround`/`StubSite` | Authoring velocity collapses; merge conflicts; no non-programmer pipeline |
| H3 | **Fake multiplayer entry** | `GameManager` lobby, MaxPlayers=1, no sync | Player/host assumptions harden; “add co-op later” becomes rewrite of player, cleaning, economy |
| H4 | **Declared but unwired job features** | `TimeLimitSeconds`, `IsCombatLevel` unused | Design docs lie; designers ship metadata that never gates spawn/HUD |
| H5 | **Mid-job progress not saved** | `SaveData` has `JobIndex` only | Soft lock frustration; exploits via quit; prestige/idle loops assume session continuity |
| H6 | **Completion bonus idempotency soft** | `AwardCompletionBonus` has no internal once-flag; gated only by `AwaitingDeparture` in `TickCompletion` | Refactors or double-tick paths can double-pay (**Inference**; not proven at runtime) |

---

## 6. Medium Priority Problems

| # | System | Evidence | Risk |
|---|--------|----------|------|
| M1 | **`PressureWasher` mixes verb + VFX + combat** | Spray pools, gun branch, `FindEnemy`, aim HUD | Hard to add tools/modes without bloating one component |
| M2 | **`Enemy` mixes AI + attack economy + visuals** | Movement, resoiling, attacks, box/citizen/animal build | New enemy types = more switch cases; visual pipeline already branched |
| M3 | **`Hud.razor` as UI god panel** | Reads `GameCore`, `EnemyManager`, `PressureWasher`, `HitmanBriefingNpc`; huge `BuildHash` | Any state change rebuilds whole HUD; story/copy lives in UI (`HitmanBriefingText`) |
| M4 | **Prestige vs tools asymmetry** | `PrestigeSystem.TryPrestige` clears upgrades + job, resets wallet; does **not** clear `OwnedTools` / equip | Balance/intent unclear; prestige runs keep purchased tools (**Fact**) |
| M5 | **Hitman arc half-removed** | Index −1; unlock still on `SaveData`; contract kinds gated in `EnemyManager` | Confusing for content authors; cheat `CheatJumpToLevel` still manipulates hitman flags |
| M6 | **O(n) surface search** | `Enemy.NearestSurface`, blood splat | Lag when panel count grows (large multi-site maps) |
| M7 | **Ambient peds always on** | `AmbientPedestrianManager` theme density | Fill rate cost on busy themes; no LOD/cull policy beyond roam prune |
| M8 | **Nullable disabled** | `csproj` `<Nullable>disable</Nullable>` | Null-related bugs harder to catch as codebase grows |

---

## 7. Low Priority Problems

| # | Evidence | Risk |
|---|----------|------|
| L1 | Duplicate van park math in `Van.ParkAtJob` and `LevelViewer.ParkVan` | Drift on van placement |
| L2 | `ToolSystem.IsUnlocked` alias of `IsOwned` | API noise |
| L3 | `EnemyManager.TickMidJobWave` hardcodes job index `>= 4` | Magic threshold outside `GameConstants` |
| L4 | `RootNamespace` = `Sandbox` while types live in `UnderPressure` | Tooling/docs confusion |
| L5 | Empty `catch` on ambience start in `GameCore` | Silent audio failures |
| L6 | Leaderboard float cast of double earnings | Precision loss at high lifetime totals (**Inference**) |

---

## 8. Technical Debt

**Verified**

- **Content debt:** 22/25 jobs are narrative-only stubs (`CampaignCatalog`).
- **Feature debt:** Timed/combat job fields unused; hitman briefing job disabled (`HitmanBriefingJobIndex = -1`) while related types remain.
- **Orchestration debt:** UI/cinematic/progression concentrated in `GameCore` + monolithic `Hud.razor`.
- **Architecture debt:** 7 component singletons; no service locator/DI/events bus beyond local C# `event`s on wallet/upgrades/surfaces.
- **Save migration policy:** Major overhauls wipe progress (v3, v6) — workable early, punitive for live players later.
- **Scenery kit growth:** `Scenery` + `WorldMapBuilder` procedural box kits — each new landmark is more C# switch arms (`DecorKind`).

**Inferred**

- Without an external level format, campaign completion will dominate engineering time vs gameplay systems.
- Adding a second concurrent player or spectator will touch almost every `Instance` call site.

**Unknown (confirm with tooling/runtime)**

- Whether `AwardCompletionBonus` can fire twice under hitch/replay (**check:** breakpoint / assert once-per-load).
- Peak `CleanableSurface` mask memory with many large panels (**check:** texture/GPU profiler).
- Actual frame cost of ambient peds + enemy citizen anims on target hardware (**check:** frame budget).
- Dead assets outside `Code/` (not audited — skill asked for codebase; assets not fully scanned).

---

## 9. Suggested Refactors (by impact)

1. **Split `GameCore` into session services**  
   Extract: `JobFlowController` (complete/depart/briefing), `UiModalState` (flags only), `SessionSpawner` (player/van/enemies/HUD), keep `GameCore` as thin composition root.  
   **Risk mitigated:** H1.

2. **Externalize campaign levels**  
   Move `JobDef` graphs to JSON/ScenePrefabs (or s&box resources); keep `CampaignCatalog` as loader + validation. Keep `LevelViewer` as the art loop.  
   **Risk mitigated:** H2.

3. **Implement or delete net pretence**  
   Either strip lobby creation to offline single-player, or introduce explicit `IGameSession` with host authority, synced job index, and “local-only” cleaning until design exists.  
   **Risk mitigated:** H3.

4. **Wire or remove `JobDef` contracts**  
   Implement timer/combat spawn multipliers from `TimeLimitSeconds` / `IsCombatLevel`, or delete fields until needed.  
   **Risk mitigated:** H4.

5. **Save policy decision**  
   Either document “jobs always restart clean” as design, or persist a compact dirty-mask checksum / stage progress for current job.  
   **Risk mitigated:** H5.

6. **Introduce surface registry**  
   `JobSiteManager` already holds `_surfaces` — expose for enemies/blood instead of `GetAllComponents`.  
   **Risk mitigated:** M6.

7. **Thin `PressureWasher` / `Enemy`**  
   Separate `ToolUseController`, `SprayVfx`, `PestBrain`, `PestVisualFactory`.  
   **Risk mitigated:** M1–M2.

8. **UI state projection**  
   HUD binds to a read-only view model updated by systems, not 15 singleton digs + hitman strings in Razor.  
   **Risk mitigated:** M3.

---

## 10. Long-term Scalability Concerns

Assume more content, AI, interactables, larger worlds, multiplayer, years of development:

| Assumption | Current bottleneck |
|------------|-------------------|
| **Many more jobs** | C# `CampaignCatalog` + procedural `Scenery` switches; no streaming |
| **Many AI** | Per-enemy `OnUpdate` + full surface scans; citizen humanoids |
| **Many cleanables** | Per-panel mask textures (cap 512²) × layers; GPU upload regions; O(n) nearest-surface |
| **Large worlds** | Single job root rebuild on advance; horizon/perimeter always rebuilt; no streaming cells |
| **Extensive multiplayer** | No authority model; wallet/save local; cleaning is local simulation |
| **Years of live ops** | Wipe-heavy migrations; float leaderboard stats; god-object change risk |

**Inference:** The cleaning simulation can scale with engineering investment; the **session + content + net** layers will not without deliberate seams.

---

## 11. Recommended Architecture Roadmap

### Phase A — Stabilize (1–2 weeks)
- Document session ownership diagram (who mutates `SaveData`, who owns UI flags).
- Delete or implement dead `JobDef` fields; document hitman arc status.
- Guard completion/perfect awards with explicit once-flags on `JobSiteManager`.
- Surface registry for pest/blood queries.

### Phase B — Content pipeline (2–6 weeks)
- Finish stub jobs via data files + `LevelViewer` workflow (already half there).
- Grow `DecorKind` kit carefully or move set pieces to prefab/model assets (pair with asset-authoring skill).
- Keep narrative metadata separate from geometry defs.

### Phase C — Decompose session (ongoing)
- Split `GameCore`; introduce thin events (`JobCompleted`, `DiscoveryFound`) instead of surfaces calling `GameCore.Instance?.NotifyDiscovery`.
- Split HUD into overlays fed by view models.

### Phase D — Commercial systems (when productizing)
- Save strategy for live players (additive migrations, not wipe-by-default).
- Decide multiplayer product slice (async leaderboards only vs co-op cleaning) and design network ownership **before** adding players.
- Economy telemetry (already have cloud stats hooks via `LeaderboardService`).

### Phase E — Scale simulation
- Spatial index for cleanables; pest LOD; pool ambient peds; budget spray VFX.

---

## Dependency / ownership sketch (verified)

```
GameManager (scene)
  └─ GameCore (singleton hub)
        ├─ SaveData ← SaveManager (JSON)
        ├─ PlayerWallet / UpgradeSystem / ToolSystem / PrestigeSystem / JobSiteManager
        ├─ spawns PressurePlayer → PressureWasher + WandView
        ├─ Van, EnemyManager, AmbientPedestrianManager, Hud
        └─ JobSiteManager → JobWorldBuilder → WorldMapBuilder / Scenery / CleanableSurface

Catalogs (static data): CampaignCatalog → JobCatalog, EnemyCatalog, ToolCatalog, UpgradeSystem.All
```

**Data ownership (Fact):** Mutable shared `SaveData` is the single source of truth for meta-progress; runtime job cleanliness lives only on `CleanableSurface` instances under the job root.

**Network ownership (Fact):** None beyond creating a solo lobby — all gameplay is local.

**Save ownership (Fact):** `SaveManager` I/O; `GameCore` decides when to save; systems mutate fields on the shared `SaveData`.

---

## What this audit did / did not claim

- **Did:** Read every `.cs` under `Code/` (structure, coupling, managers, catalogs, UI, net, save).
- **Did not:** Modify code; audit Assets/scenes for dead assets; run the game for double-award or perf proof.

If you want a follow-up, the highest-leverage non-code next step is a **target architecture diagram for Phase C** (session split + content format); implementation should wait for an explicit request.