# PRE-SHIP COMPLETENESS AUDIT — Under Pressure

**Project:** `C:\Users\Macra\Projects\sandboxProjects\under_pressure`  
**Audit date:** 2026-07-20  
**Method:** Code/assets/scenes inspection only. No fixes applied. Confirmed vs suspected labeled.

---

## A. Executive Summary

| Metric | Value |
|--------|-------|
| **Overall completion** | **~28%** |
| **Shippable to real players?** | **No** |
| **P0** | **3** |
| **P1** | **11** |
| **P2** | **14** |
| **P3** | **8** |

**Three largest risks**
1. **Campaign softlock after level 3** — levels 4–25 are narrative-only `StubSite`s with empty panels; `IsComplete` never becomes true.
2. **Promised late-game systems are disconnected** — timed jobs, combat levels, hitman/gun unlock, scrub/squeegee FollowUp never drive real play.
3. **22/25 sites have no authored gameplay** — players who finish Act I land on empty UrbanPlaza pads with story text only.

**Three strongest completed areas**
1. **Core wash loop** — `PressureWasher`, `CleanableSurface`, spray VFX, water/stamina, van depart flow (levels 1–3).
2. **Meta systems** — wallet, upgrades, prestige, daily reward, save v6, Tab menu HUD.
3. **Authored Act I sites** — levels 1–3 with decor, secrets, discoveries, pests (2–3), briefings.

**Playable loop status:** Fully playable **only for levels 1–3**. After departing level 3, the session softlocks on level 4 unless the player resets progress or uses prestige/cheats.

---

## B. Current Real Game Loop

What players can **actually** do today:

1. Launch `scenes/game.scene` → `GameManager` creates a **1-player** lobby → `GameCore` boots.
2. Load save → spawn player, van, enemies, pedestrians, HUD → mission briefing → daily popup (if due).
3. Hold **LMB** to pressure-wash dirty panels → earn cash per cell → optional pest fights (from job index ≥ 2).
4. At **99%**, completion bonus + “return to van”; optional **100%** perfect bonus.
5. **RMB** on van → equip/buy tools → **DEPART** → blackout + van SFX → next job briefing.
6. Between jobs: Tab menu → upgrades, prestige, leaderboard, van tier, reset-all.
7. **After level 3:** load stub level 4 → empty pad, **0% forever**, depart locked → loop broken.

**Not available in real play:** timed fail states, combat objectives, hitman briefing/gun unlock (disabled), scrub/squeegee-required surfaces, levels 4–25 content, multiplayer sessions, campaign ending/credits.

---

## C. Master Feature Inventory

| Feature | Status | Purpose | Functional? | Connected to loop? | Assets complete? | Audio complete? | UI complete? | Multiplayer complete? | Priority |
|---------|--------|---------|-------------|--------------------|------------------|-----------------|--------------|----------------------|----------|
| Pressure washing | Mostly complete | Core verb | Yes | Yes (L1–3) | Procedural OK | Mostly | Yes | N/A (solo) | — |
| Cleanable panels / secrets | Mostly complete | Clean + story reveals | Yes L1–3 | Yes L1–3 | Procedural | Partial | Discovery popup yes | N/A | P0 for L4–25 |
| Job complete → van depart | Mostly complete | Advance campaign | Yes if cells exist | Breaks on stubs | — | Van SFX yes | Yes | N/A | P0 |
| 25-level campaign | Partially implemented | Full story | **No** (3/25) | Softlocks | Stub pads | Briefing SFX none | Briefing text only | N/A | P0 |
| StubSite L4–25 | Stubbed | Hold narrative | Sites empty | **Disconnected** | Empty pad | — | Story in briefing | N/A | P0 |
| TimeLimitSeconds | Placeholder only | Timed pressure | **Never read** | No | — | No | No timer UI | N/A | P1 |
| IsCombatLevel | Placeholder only | Combat jobs | **Never read** | No | — | No | No | N/A | P1 |
| CrimeScene / RevealHook | Disconnected | Story UI | Data only | No | — | — | Not shown | N/A | P1 |
| Pests (rat/pigeon/etc.) | Mostly complete | Harassment / bounty | Yes when spawned | L2–3 yes | Box/citizen fallback | Weak/wrong SFX | Pest HUD yes | N/A | P2 models |
| Mid-job RivalWasher wave | Partially implemented | Mid-job spike | Code exists | Needs progress ≥50% | Fallback | Alert yes | Alert yes | N/A | P2 |
| Hitman fixer briefing | Stubbed / disabled | Unlock gun | `HitmanBriefingJobIndex = -1` | **No** | Citizen/box | UI click | Dialogue UI exists | N/A | P1 |
| Gun + contracts | Partially implemented | Assassin side-path | Code exists | Unlock never fires | Box humans | **button.sound** | Van gate exists | N/A | P1 |
| Scrub brush / squeegee | Partially implemented | Hand tools | Mechanics yes | **No FollowUp panels** | Wand viewmodels | brush/squeegee yes | Van shop yes | N/A | P1 |
| Tool upgrades (brush/sq) | Mostly complete | Improve tools | Yes | Weak (tools unused) | — | Purchase SFX | Yes | N/A | P1 |
| Economy / wallet | Complete | Earn/spend | Yes | Yes | — | economy.sound | Yes | N/A | — |
| Prestige | Mostly complete | Long-tail reset | Yes | Escapes softlock awkwardly | — | level_up | Yes | N/A | P2 clarity |
| Daily reward | Complete | Login streak | Yes | Meta | — | tame on dismiss | Popup yes | N/A | — |
| Save / load | Mostly complete | Persistence | Yes | Yes | — | — | Reset UI | Client-side only | P2 |
| Leaderboard | Mostly complete | Lifetime earnings | Suspected OK if stats live | Meta | — | — | Tab exists | Cloud | P2 |
| Ambient pedestrians | Mostly complete | Atmosphere | Yes | Cosmetic | Citizen | Footsteps partial | No | N/A | P3 |
| World/map builder | Mostly complete | Setting | Yes | Theme for stubs too | Materials/trees | Ambience | — | N/A | — |
| Van interact / locker | Mostly complete | Tools + depart | Yes | Yes | Box van tiers | — | Yes | N/A | — |
| HUD / briefings / menus | Mostly complete | UX | Yes | Yes | Icons/fonts | Partial UI SFX | No settings | N/A | P1 settings |
| Knockout / pest hits | Mostly complete | Failure pressure | Yes L2+ | Yes | Screen only | Footstep misuse | Popups yes | N/A | P2 |
| Networking / co-op | Missing | Multiplayer | Lobby MaxPlayers=1 | Solo only | — | — | — | **No** | Defer |
| In-game settings | Missing | Volume/sens/FOV | No | No | — | Hardcoded vols | No panel | N/A | P1 |
| Tutorial / controls screen | Partially implemented | Onboarding | HUD hint only | Weak | — | — | Hint line | N/A | P1 |
| Campaign ending | Missing | Closure | Wraps via modulo | No ending | — | — | No | N/A | P1 |
| Level viewer | Complete (dev) | Authoring | Yes | Dev only | — | — | LevelViewerHud | N/A | Defer |
| Dev cheats | Complete (dev) | QA | `up_*` cmds | Not player | — | — | — | N/A | Strip/gate |

---

## D. Ship Blockers

### P0

#### P0-1 — Levels 4–25 softlock (empty StubSites)
- **Problem:** After level 3, jobs have `Panels = []` → `TotalCells == 0` → `IsComplete` always false → depart never unlocks.
- **Evidence:** `CampaignCatalog.StubSite` (empty lists); `JobSiteManager.IsComplete` requires `TotalCells > 0`; `GameCore.DepartJob` requires `AwaitingDeparture`.
- **Location:** `Code/Cleaning/CampaignCatalog.cs` (`StubSite`, jobs 4–25); `Code/Cleaning/JobSiteManager.cs` (`IsComplete`, `Progress`); `Code/Core/GameCore.cs` (`TickCompletion`, `DepartJob`).
- **Player impact:** Campaign ends in a permanent stuck state; progress % stays 0; “FINISH THE JOB TO LEAVE” forever.
- **Required fix:** Author real panels/props/decor/enemies (or temporary skip/auto-complete) for every StubSite before shipping a 25-level campaign.
- **Dependencies:** Level art/layout; secrets if story discoveries matter; enemy lists if combat flags matter.
- **Completion criteria:** Every level 4–25 has `TotalCells > 0`, can reach 99%, depart, and load next; `up_complete` works for QA.

**Repro (confirmed):** Complete L1–L3 → depart → L4 “Corporate Loading Dock” → no dirt → cannot depart. `up_complete` warns “No active job to complete” when `TotalCells <= 0`.

#### P0-2 — Majority of marketed campaign content missing
- **Problem:** File states narrative-only stubs for rebuild; 22/25 levels are empty play-pads with UrbanPlaza theme.
- **Evidence:** `CampaignCatalog.cs` lines ~311–542; header comment; `Theme = MapTheme.UrbanPlaza`, `GroundColor = StubPad`.
- **Location:** `Code/Cleaning/CampaignCatalog.cs`.
- **Player impact:** Briefings describe docks, yachts, combat, reservoirs, etc.; world is a blank pad — reads as broken/unfinished.
- **Required fix:** Rebuild each site to match briefing (or cut campaign to 3 levels and rewrite packaging).
- **Dependencies:** Scenery/decor kinds, panels, secrets, optional combat systems.
- **Completion criteria:** Each shipped level’s physical site matches its briefing purpose; no StubPad-only jobs in release build.

#### P0-3 — No complete session through intended campaign
- **Problem:** A full “story campaign” session cannot finish; only a 3-job demo loop works.
- **Evidence:** Chain of P0-1 + no ending (index wraps in `LoadJob` modulo).
- **Location:** `JobSiteManager.LoadJob` / `AdvanceToNext`; `JobCatalog.Get` modulo wrap.
- **Player impact:** Cannot finish product as advertised; only endless Act I or softlock.
- **Required fix:** Finish or cut content; add explicit end state (credits / finale / prestige-only infinite mode).
- **Dependencies:** P0-1, P0-2, ending design.
- **Completion criteria:** Player can start fresh and reach a defined end (or clearly scoped short campaign) without cheats.

### P1

#### P1-1 — `TimeLimitSeconds` never enforced
- **Evidence:** Set on level 11 (`timeLimit: 420f`); **zero reads** outside `JobDef` / `StubSite`.
- **Location:** `JobCatalog.cs` (`TimeLimitSeconds`); `CampaignCatalog.cs` L11; no timer in `GameCore`/`Hud`.
- **Impact:** “Clock is ticking” briefing is false.
- **Fix:** Implement countdown, fail/depart rules, HUD; or remove field/copy.
- **Done when:** Timed jobs fail or resolve by design with clear UI.

#### P1-2 — `IsCombatLevel` never used
- **Evidence:** Set on levels 15–25; never referenced in gameplay systems.
- **Location:** `CampaignCatalog.cs`; grep shows only definition/assignment.
- **Impact:** Combat act is narrative-only; stubs also have empty `Enemies`.
- **Fix:** Wire combat spawning/pressure or drop combat act from ship scope.
- **Done when:** Combat jobs behave differently in play, or flags removed from shipped jobs.

#### P1-3 — Hitman / gun unlock path disabled
- **Evidence:** `GameConstants.HitmanBriefingJobIndex = -1`; `HitmanBriefingNpc.EnsureForJob` returns when index mismatch / `< 0`.
- **Location:** `GameConstants.cs`; `HitmanBriefingNpc.cs`; gun `RequiresHitmanUnlock` in `ToolSystem.cs`.
- **Impact:** Classified gun and contract pests unreachable in normal play.
- **Fix:** Re-enable briefing on an authored job, or remove gun/contracts from ship UI/copy.
- **Done when:** Unlock is reachable in-campaign, or feature fully removed from player-facing surfaces.

#### P1-4 — Scrub brush / squeegee have no job surfaces
- **Evidence:** No `FollowUp` set on any `CampaignCatalog` panels; L1–3 are washer-only.
- **Location:** `PanelDef.FollowUp` in `JobCatalog.cs`; unused in `CampaignCatalog.cs`.
- **Impact:** Expensive tools + whole upgrade groups exist without gameplay need (raccoon needs brush but never spawns in authored jobs).
- **Fix:** Add FollowUp glass/moss panels, or hide tools/upgrades until needed.
- **Done when:** At least one shipped job requires each paid tool meaningfully.

#### P1-5 — `CrimeScene` / `RevealHook` never shown
- **Evidence:** Stored on `JobDef`; briefing UI uses `Briefing`/`Location`/`Act` only (`GameCore.OpenMissionBriefing`, `Hud.razor` mission-brief).
- **Impact:** Authored story metadata invisible; discoveries only if secrets exist (stubs have none).
- **Fix:** Surface in briefing UI or discovery system; add secrets on rebuilt levels.
- **Done when:** Players see crime/reveal info that matches design.

#### P1-6 — No in-game settings
- **Evidence:** No settings panel; volumes hardcoded (`GameCore.StartAmbience` 0.35f; `Sfx.Play`); FOV constant; no sens option.
- **Location:** `Hud.razor` (Overview/Upgrades/Leaderboard/Van only); `GameConstants.FieldOfView`.
- **Impact:** Below minimum release expectations for volume/sensitivity.
- **Fix:** Master/music/SFX (+ look sens); persist in save or engine prefs.
- **Done when:** Players can change and keep audio/look settings.

#### P1-7 — No campaign ending
- **Evidence:** `LoadJob` wraps with modulo; level 25 stub has no finale UI.
- **Impact:** Even after content exists, campaign would loop to level 1 silently.
- **Fix:** End screen / credits / New Game+ gate after 25.
- **Done when:** Completing final job shows closure, not silent wrap.

#### P1-8 — Tutorial / objective clarity insufficient for stubs & tools
- **Evidence:** Single bottom hint in `Hud.razor`; no controls screen; stub briefings contradict empty world.
- **Impact:** “Is the game broken?” on level 4; tool buy prompts without need.
- **Fix:** Ship only finished levels + short how-to; align copy with reality.
- **Done when:** New player completes first loop without confusion; no contradictory briefings.

#### P1-9 — Gun / clean-tick SFX are UI button placeholders
- **Evidence:** `Sfx.Gunshot = "sounds/button.sound"`; `Sfx.CleanTick = "sounds/button.sound"`.
- **Location:** `Code/Core/Sfx.cs`.
- **Impact:** Combat/clean feedback sounds wrong.
- **Fix:** Dedicated gunshot + clean tick assets.
- **Done when:** Distinct, non-UI sounds for those events.

#### P1-10 — Package presents Multiplayer while game is solo-only
- **Evidence:** `under_pressure.sbproj` `GameNetworkType: Multiplayer`, `MaxPlayers: 1`; `GameManager` lobby `MaxPlayers = 1`; no replication of game state.
- **Impact:** Store/browser expectations wrong; joining others impossible.
- **Fix:** Mark Singleplayer (or implement real co-op — large scope).
- **Done when:** Store metadata matches solo gameplay.

#### P1-11 — Pest models missing (acceptable short-term; not for “finished” art bar)
- **Evidence:** `PestModels` paths under `models/under_pressure/pests/*` — directory absent; only foliage trees in `Assets/models`.
- **Impact:** Box/citizen fallbacks; fine for prototype, weak for ship polish if combat marketed.
- **Fix:** Ship models or commit to stylized boxes in store art/description.
- **Done when:** Visuals match marketing; no broken model paths expected.

---

## E. Half-Implemented Features

| System | What’s there | What’s missing |
|--------|--------------|----------------|
| Campaign L4–25 | Names, blurbs, briefings, acts, pay multipliers, flags | Panels, props, decor, enemies, secrets |
| Timed jobs | `TimeLimitSeconds` data | Timer tick, fail, UI, SFX |
| Combat levels | `IsCombatLevel` + story | Combat logic, enemy layouts, win rules |
| Hitman arc | NPC, dialogue UI, gun tool, contract enemy defs | Enabled job index, authored contract jobs, unlock flow |
| Hand tools | Buy/equip, viewmodels, stamina, upgrades, sounds | Any panel FollowUp requiring them |
| Mid-job rival wave | Spawns RivalWasher at 50% on job index ≥ 4 | Never fires on stubs (`Progress` stays 0) |
| Leaderboard | In-HUD board + stats submit | Package `LeaderboardType: None`; cloud reliability unverified |
| Prestige | Works | Copy understates job reset; OwnedTools survive |
| Glass surface | `CleanSurface.Glass` | Uses `GameMaterials.Metal` (`JobWorldBuilder`) |
| Animal pests | Model pipeline + paths | Actual `.vmdl` assets |
| Ambience | `MusicPlayer` on mp3 | Empty `catch` swallows failure (`GameCore.StartAmbience`) |
| Level viewer | Separate scene + HUD | Dev tool, not player journey |

---

## F. Features That Exist but Do Not Yet Matter

| Feature | Exists | Why it doesn’t matter | Should |
|---------|--------|----------------------|--------|
| Scrub brush / squeegee | Shop + mechanics | No surfaces need them | Complete with FollowUp jobs **or remove/defer** |
| Brush/squeegee upgrades | Full upgrade groups | No target surfaces | Same |
| Gun + contracts | Tool + enemy kinds | Unlock disabled; no spawns on stubs | Complete unlock + L25 content **or remove** |
| `CrimeScene` / `RevealHook` | Authored strings | Never displayed | Show in UI **or delete** |
| `IsCombatLevel` / `TimeLimitSeconds` | Flags on stubs | Unused | Implement **or remove** |
| Hire Helper (AutoHelper) | Passive income upgrade | Optional meta; fine but weak early | Keep as meta sink |
| Ambient pedestrians on stubs | Crowds on empty pad | Atmosphere without activity | Fine after sites exist |
| Rival mid-wave | Code path | Unreachable on stubs | Keep for later levels |
| Prestige as softlock escape | Works | Not a substitute for campaign | Don’t rely on it for progression |
| Level viewer | Full | Not in player loop | Keep internal |

---

## G. Missing Assets

### Models
- **Pest VMDLs** (`PestModels.cs`): pigeon, wasp, leech, rat, raccoon, stray_dog — **missing** (box fallbacks). Priority: P1–P2.
- **Job architecture** for L4–25: docks, yacht, subway, lab, dam, etc. — **missing** (empty pad). Priority: **P0**.
- **Dedicated player body** — FP only; intentional. Priority: P3.
- **Props** — colored boxes via `PropDef` / `MeshPrimitives.Box`. Priority: P2 for ship polish; OK for stylized if consistent.

### Materials / textures
- Present: grass, concrete, wood, metal, bark, leaves, shingles, grime, water_spray, sky (`Assets/materials/up`, `Assets/textures/up`).
- Glass cleaning uses metal material — **placeholder mapping**. Priority: P2.

### Icons / UI art
- Material icon font names in HUD — functional, not custom icons. Priority: P3.
- Fonts: Poppins set present. Logo: brand text in menu (“UNDER PRESSURE”), `thumbnail.png` present.

### Animations
- Player: no third-person; wand bob for scrub only (`WandView`).
- Pests: citizen locomotion when citizen loads; boxes otherwise.
- Missing: damage/death/celebration anims beyond UI. Priority: P2–P3.

### Effects
- Spray droplet pools exist; blood splatter helper for gun kills. Limited VFX variety. Priority: P2.

### Marketing art
- `thumbnail.png` present (~3MB). No verified cover/description/controls copy in repo beyond sbproj title. Priority: P1 for store page (suspected incomplete outside repo).

---

## H. Missing Audio

| System / action | Status |
|-----------------|--------|
| Ambience loop | Present (`sounds/ambience.mp3` via MusicPlayer) |
| Pressure washer / spray / brush / squeegee | Present as tool use loops |
| Footsteps | Present |
| Van depart | Present |
| Purchase / prestige / job complete / reward | Present (mapped) |
| Clean tick | **Placeholder** → `button.sound` |
| Gunshot | **Placeholder** → `button.sound` |
| Menu music | Missing (ambience only) |
| UI hover (distinct) | Missing / reused |
| Objective fail / timer tick / countdown | Missing (no timer system) |
| Round/victory/defeat stingers | Partial (completion uses skill_upgrade/tame) |
| Pest-specific attacks | Reuses clean/footstep/purchase/spray |
| Dialogue VO | Missing (text only) |
| Environment bed per theme | One global ambience |

**Major actions with no dedicated sound:** timer warning, combat start, mission fail, discovery sting (uses UI flow only), door/machine (N/A until content).

---

## I. Missing UI and Feedback

- **No settings screen** (volume, sensitivity, FOV, keybinds display).
- **No pause-with-settings**; Tab is economy menu.
- **No timer HUD** despite timed-job data.
- **No combat objective HUD** despite combat flags.
- **CrimeScene / RevealHook** unused in briefing.
- **Stub levels:** progress bar stuck at 0% with no explanation that site is unfinished.
- **Depart button** locked with “FINISH THE JOB” when job is unwinnable — feels broken.
- **Gun unlock toast** path unused in normal play.
- **Controller:** some bindings in `Input.config`; look is mouse `AnalogLook` — gamepad completeness **suspected incomplete**.
- **Credits / version / patch notes** — not in HUD.
- **Loading screen** — engine default; no custom.
- Feedback that **works** on L1–3: spray VFX, progress bar, completion/perfect cards, pest guide, harassment, knockout, discovery monologue, van focus prompt.

---

## J. Role and Game Mode Gaps

| Role / mode | Status |
|-------------|--------|
| **Solo cleaner (only role)** | Clear goal on L1–3: wash → earn → upgrade → next job. After L3: no valid objective. |
| **Minute-to-minute** | Spray dirt; manage water; shoo pests (L2+). Downtime: walk to van, shop. |
| **Hitman / contractor** | Designed but unlock disabled; no unique mode UI. |
| **Game modes** | Single continuous campaign only. No MP modes despite metadata. |
| **Start / win / lose / reset** | Start: briefing. Win job: 99% + depart. Lose: knockout (cash penalty), not job fail. No timed fail. Campaign reset: prestige or “Reset All Progress”. Round reset: job load rebuilds world. |
| **Disconnect / late join** | Solo lobby; not designed. |
| **Bots** | Ambient peds only; not teammates. |
| **Results screen** | Per-job flash cards; no campaign results. |

---

## K. World and Level Gaps

### Areas With No Current Gameplay Purpose

| Area | Purpose today | Recommendation |
|------|---------------|----------------|
| **Stub L4–25 play-pads** | Empty UrbanPlaza + horizon + peds | **Give purpose** (rebuild) or **block from ship** |
| **Distant horizon / perimeter** | Atmosphere | Keep |
| **L1 driveway + house** | Core clean + secret | Keep |
| **L2 gas/car wash** | Clean + rats + Aegis secret | Keep |
| **L3 alley / steakhouse** | Blood clean + secret | Keep |
| **Map field beyond pad** | Soft boundary / scenery | Keep; ensure no soft-lock fall voids (**suspected OK** with colliders) |
| **Decorative non-interactive doors/windows** | Readability | OK if not prompted as usable |

Unfinished geometry: stub pads are intentionally blank unfinished content — **confirmed**.

---

## L. Multiplayer and Reset Risks

| Topic | Finding |
|-------|---------|
| Architecture | `MaxPlayers = 1`; local `GameCore` singleton; no `[Sync]`/RPCs for job/wallet |
| “Multiplayer” metadata | Misleading (**confirmed**) |
| Host vs client | N/A for real co-op |
| Job reset | `LoadJob` destroys/rebuilds root; player snap via `LoadGeneration` — **confirmed** for solo |
| Save | Client `FileSystem.Data`; not anti-cheat validated — OK for solo, not for competitive MP |
| Prestige mid-job | Reloads job 0; tools ownership kept |
| Stub softlock | Persist across sessions via `JobIndex` — **confirmed** (reload stays on L4) |
| Departure race | Solo only; low risk |
| Duplicate rewards | Completion gated by `AwaitingDeparture` / `_perfectAwarded` — looks correct solo |
| Suspected | Leaderboard/stats fail silently offline; ambience fail swallowed |

**Features only designed for single-player:** entire game. Treat MP as out of scope for launch.

---

## M. Content Gaps

| Content | Count / note | Launch need |
|---------|--------------|-------------|
| Authored playable levels | **3 / 25** | **Must-have:** finish or cut to 3 |
| Stub narrative levels | **22** | Must rebuild or remove |
| Tools | 4 (1 starter, 2 unused, 1 locked) | Need content OR defer tools |
| Enemy kinds | 10 defs; few used | Enough if levels use them |
| Maps | Procedural themes; not separate map files | OK |
| Music | 1 ambience | Minimum OK; add menu sting P2 |
| Difficulty | Pest difficulty curve exists | Unused on stubs |
| Ending / acts payoff | Written in text only | Must-have closure or shorter campaign |
| Tutorials | Hint line | Short how-to P1 |
| Post-launch | More levels, VO, co-op, cosmetics | After core campaign ships |

**Players see all real content in one session:** after ~3 jobs, they either softlock or prestige-loop Act I — **confirmed shallow for a 25-level product**.

---

## N. Remove, Merge, or Defer

| Item | Action | Why |
|------|--------|-----|
| True multiplayer co-op | **Defer** | No systems; MaxPlayers 1 |
| Hitman/gun/contracts (if not finishing unlock) | **Remove or hide** for launch | Dead path adds confusion |
| Scrub/squeegee (if no FollowUp jobs in v1) | **Defer unlock** until a job needs them | Shop noise |
| Full Act III–IV combat fantasy | **Defer** until combat systems exist | Scope bomb |
| Level viewer as player feature | **Keep internal** | Dev tool |
| Pest unique models | **Defer** if box style is accepted in marketing | Not softlock |
| Localization | **Defer** | Folder empty / unused |
| Prestige | **Keep** | Works; meta loop |
| Ambient pedestrians | **Keep** | Cheap atmosphere |
| `facepunch.van_dev` package | **Review** | Name suggests dev dependency — **suspected** risk for release packaging |

---

## O. Prioritized Completion Plan

### Stage 1 — Make the game fully playable
1. **P0 — Unblock campaign** — Author or temporarily skip L4+ so every shipped level has cleanable cells and depart works. Files: `CampaignCatalog.cs`, `JobWorldBuilder`/`Scenery`. **Done:** no softlock from L1→end of shipped set.
2. **P0 — Define ship scope** — Either “25-level story” or “Act I demo (3 levels)” and align `sbproj`/store copy. **Done:** marketed length matches playable length.
3. **P1 — Campaign ending** — Stop modulo wrap; show finale. Files: `JobSiteManager`, `GameCore`, `Hud`. **Done:** clear end state.

### Stage 2 — Make every feature purposeful
4. Wire or remove `TimeLimitSeconds` / `IsCombatLevel`.
5. Re-enable or remove hitman/gun.
6. Add FollowUp panels for brush/squeegee **or** gate those tools.
7. Show `CrimeScene`/`RevealHook` or delete fields from player data path.

### Stage 3 — Replace unacceptable placeholders
8. Replace `Sfx.Gunshot` / `CleanTick` button mappings.
9. Glass material; reduce “empty pad” look on all shipped levels.
10. Pest models **or** explicit low-poly box art direction in store materials.

### Stage 4 — Make multiplayer and resets reliable
11. Set package to Singleplayer; remove public MP expectations.
12. Verify save on stub→fixed migration; prestige/reset paths; no stuck `JobIndex`.
13. Gate/remove `up_*` cheats from public builds if required by platform.

### Stage 5 — Make the game understandable
14. Settings: volume + sensitivity (+ persist).
15. First-run controls / objective card; fix stub contradiction.
16. Timer/combat UI if those modes ship.

### Stage 6 — Final release polish
17. Audio variety, discovery sting, theme ambience.
18. Balance pay curve across 25 levels.
19. Thumbnail/description/controls/credits/version.
20. Pass on knockout, pest harassment, and van UX.

---

## P. Final Pre-Ship Checklist

```
[ ] No StubSite (empty Panels) remains in the shipped campaign
[ ] Every shipped level: TotalCells > 0, 99% reachable, van depart works
[ ] Completing the last shipped level shows an ending (not silent wrap to L1)
[ ] Store/package player count & network type match solo gameplay
[ ] Briefing text matches what exists in the world
[ ] TimeLimitSeconds either works with HUD/fail rules or is removed
[ ] IsCombatLevel either changes gameplay or is removed
[ ] Hitman/gun either unlocks in-campaign or is hidden/removed
[ ] Scrub/squeegee either required by ≥1 job each or locked away
[ ] CrimeScene/RevealHook shown or removed from player-facing design
[ ] In-game settings: master/music/SFX volume + look sensitivity
[ ] Clean-tick and gunshot use non-UI dedicated sounds
[ ] New player can finish first job without asking “is it broken?”
[ ] Save survives full session; JobIndex cannot softlock forever
[ ] Prestige / Reset All Progress behave as labeled
[ ] Dev cheats unavailable (or clearly undocumented) in release
[ ] Pest visuals acceptable vs marketing
[ ] Credits + version visible
[ ] Thumbnail, description, controls written for real loop
[ ] No P0 softlocks remaining after Act I
[ ] Playtest: cold install → complete shipped campaign → quit → reload
```

---

## Direct answer

**Before this is safe for real players, you must either rebuild levels 4–25 into real jobs (or ship a shorter Act I–only campaign), eliminate the empty-panel softlock, and align packaging/story systems (timers, combat, hitman/gun, hand tools, ending, settings) with what actually works in play.** Until then, Under Pressure is a strong **3-level prototype** with unfinished campaign scaffolding—not a releasable 25-level game.