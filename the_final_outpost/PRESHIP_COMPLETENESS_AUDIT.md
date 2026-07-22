# Pre-Ship Completeness Audit — The Final Outpost

**Date:** 2026-07-20  
**Scope:** Full project (`Code/`, `Assets/`, `the_final_outpost.sbproj`, scenes)  
**Method:** Code-authoritative inspection. No fixes applied.  
**Confirmed vs suspected:** Confirmed unless labeled *Suspected*.

---

## A. Executive Summary

| Metric | Value |
|--------|------:|
| Overall completion | **~72%** (Survival ~85%; Road to a Cure ~55–60% with self-disclosed instability) |
| Shippable today? | **No** — not as a finished dual-mode release |
| P0 | **2** |
| P1 | **5** |
| P2 | **9** |
| P3 | **8** |

**Three largest risks**
1. Main menu advertises Road to a Cure with live text `- IN DEVELOPMENT!!! THINGS WILL BE VERY BROKEN!` (`Hud.razor:791`) — either unfinished mode in player face, or intentional EA framing that still needs a decision.
2. Project declares `GameNetworkType: Multiplayer` and creates a **Public** lobby with `MaxPlayers = 1` while zero gameplay state is networked (`GameBoot.TryCreateLobby`, `.sbproj`).
3. Referenced UI brand/build icons do not exist; structures are 100% procedural primitives — acceptable as style only if store presentation is deliberately low-poly and icons are replaced or removed from expectations.

**Three strongest completed areas**
1. Survival day/night loop: build → defend → recap → unlock → prestige, with real economy and save migration (`SaveData.Version = 21`).
2. Persistence: dual saves + profile, autosave, defeat/prestige wipe semantics, offline idle + daily rewards.
3. Progression wiring: objectives, milestones, night unlocks (power-derived), upgrades with felt effects, expeditions with real opportunity cost.

**Playable loop status:** Survival can complete a full session end-to-end. Cure can reach Victory in code, but is self-flagged unstable and should not be treated as release-ready without a dedicated bug/playtest pass.

---

## B. Current Real Game Loop

### Survival (actual)
Launch → procedural boot (`GameBoot` / `OutpostBootstrap`) → Main Menu → Continue or Start New Survival → Day phase: spend scrap to place towers/walls/barracks, recruit/train defenders, hire workers, claim/clear plots, shop upgrades, optional expeditions → Start Night → zombies spawn/scale by night → defend core/walls → survive → Night Recap (scrap, unlocks) → Day again → optional prestige after night 10 → core destroy → Game Over (wipe run, keep milestones/legacy) → menu / new run.

**Loop form:** Player spends scrap → builds defense + economy → survives night → earns scrap/unlocks → spends again → climbs best night / prestige.

### Road to a Cure (actual, self-flagged)
Menu → team pick → colony economy (wood/stone/food/supplies/knowledge/specimens) → tech tree + workers + civic buildings → seasonal threats → research tiers → Victory; defeat restores season checkpoint.

### What a new player can do in the first minutes
Pan/zoom camera, place unlocked buildings, recruit, open shop/settings/leaderboard, start night. Tutorial tips exist for early nights/seasons. Controls are not listed on the main menu (WASD pan from `OutpostCamera`; Attack1/2 for build).

---

## C. Master Feature Inventory

| Feature | Status | Purpose | Functional? | Connected to loop? | Assets complete? | Audio complete? | UI complete? | Multiplayer complete? | Priority |
|---------|--------|---------|-------------|--------------------|------------------|-----------------|--------------|----------------------|----------|
| Survival day/night | Complete | Core loop | Yes | Yes | Partial (primitives) | Partial (music-only nights) | Yes | N/A (SP) | — |
| Road to a Cure | Partially implemented | Seasonal colony + cure | Mostly | Yes | Partial | Partial | Yes + warning | N/A | P0/P1 |
| Build / place / sell / repair | Complete | Defense economy | Yes | Yes | Partial | Purchase SFX | Yes | N/A | — |
| Wall-mount placement | Disconnected | Tower on walls | Code yes, flag off | No | N/A | N/A | Toast only | N/A | P2 |
| Defenders / weapons | Mostly complete | Active defense | Yes | Yes | Partial (cloud weapons) | Fire SFX day only | Yes | N/A | P2 |
| Workers | Complete | Day production | Yes | Yes | Citizen + anim | Minimal | Yes | N/A | — |
| Plots / claim / clear | Complete | Expand + resources | Yes | Yes | Primitives | Minimal | Yes | N/A | — |
| Rival civs (Cure) | Complete | Territory pressure | Yes | Yes | Primitives | No | Partial | N/A | P2 |
| Expeditions | Complete | Risk/reward away team | Yes | Yes | N/A | Minimal | Yes | N/A | — |
| Upgrades / shop | Complete | Permanent power | Yes | Yes | Icons fallback | Purchase | Yes | N/A | — |
| Objectives / milestones | Complete | Onboarding + lifetime | Yes | Yes | Icons | Toast | Yes | N/A | — |
| Prestige / legacy | Complete | Meta progression | Yes | Yes | N/A | Yes | Yes | N/A | — |
| Tech tree (Cure) | Complete | Unlock buildings/passives | Yes | Yes | Icons | Purchase | Yes | N/A | — |
| Cure research tiers | Complete | Win condition | Yes | Yes | N/A | Clear SFX | Yes | N/A | — |
| Cure teams | Complete | Run identity | Yes | Yes | Icons | N/A | Yes | N/A | — |
| Zombie bestiary | Mostly complete | Catalog collectible | Yes | Weak | N/A | N/A | Yes | N/A | P3 |
| Combo system | Obsolete | Kill streak scrap | No-op | No | N/A | N/A | No | N/A | P3 |
| Day/night lighting | Complete | Atmosphere | Yes | Visual | N/A | Crossfade | N/A | N/A | — |
| Combat audio director | Disconnected at night | Mix gunfire/impacts | Day path | Flag blocks night | N/A | Dead at night default | N/A | N/A | P2 |
| Night combat music | Complete | Night audio identity | Yes | Yes | 2 loops | Yes | N/A | N/A | — |
| Leaderboard | Complete | Compete nights | Yes | Survival | N/A | N/A | Yes | Services API | — |
| Save / profile | Complete | Persistence | Yes | Yes | N/A | N/A | Settings audio | N/A | — |
| Lobby / networking | Stubbed | Session discovery | Lobby only | No gameplay sync | N/A | N/A | None | Broken if joined | P0 |
| Minimap | Missing | Navigation | No | No | CSS stub | No | No | N/A | P3 |
| Custom UI icons / logo | Missing | Brand + clarity | Fallback glyphs | Soft fail | No files | N/A | Fallback | N/A | P1 |
| Credits / loading art | Missing | Release presentation | No | No | No | No | No | N/A | P1 |
| Settings (non-audio) | Missing | Accessibility / controls | No | No | N/A | Audio only | Thin | N/A | P1 |
| Unit command (night) | Complete | Focus/area fire | Yes | Yes | N/A | Minimal | Button | N/A | — |
| Unit command (day Cure) | Complete | Move/attack orders | Yes | Cure | N/A | Minimal | Yes | N/A | — |

---

## D. Ship Blockers

### P0-1 — Road to a Cure shipped with explicit “broken” warning
- **Problem:** Live main menu warns the mode is in development and very broken.
- **Evidence:** `Code/UI/Hud.razor:791` — `mode-dev-warn` span.
- **Impact:** Players either avoid half the product or enter expecting failure; store/review perception is that the game is unfinished.
- **Required fix:** Either (A) finish + playtest Cure and remove the banner, or (B) hide/gate Cure behind a clear Early Access / Dev flag, or (C) ship Survival-only and remove Cure from the main menu.
- **Dependencies:** Cure playtest pass; decision on product scope.
- **Done when:** No player-facing “broken” copy on the default menu path for modes you intend players to use.

### P0-2 — Public multiplayer lobby with no networked gameplay
- **Problem:** `.sbproj` sets `GameNetworkType: Multiplayer`, `MaxPlayers: 1`. `GameBoot.TryCreateLobby` creates `LobbyPrivacy.Public` with `MaxPlayers = 1`. No `[Sync]` / `[Rpc]` / authority on game systems.
- **Evidence:** `the_final_outpost.sbproj:17–21`; `Code/Networking/GameBoot.cs:142–158`; codebase-wide absence of networking attributes on gameplay.
- **Impact:** Game appears in multiplayer contexts; any unexpected join path yields a non-functional session.
- **Required fix:** Treat as single-player: private/solo lobby (or no lobby), align `.sbproj` metadata, or implement real MP (large scope — defer).
- **Dependencies:** Product decision SP vs MP.
- **Done when:** No public empty multiplayer surface; metadata matches actual architecture.

### P1-1 — Missing brand and build UI art referenced in code
- **Problem:** `UiIcons` points at `ui/brand_emblem.png` and `ui/build_*.png`; none exist under `Assets/`.
- **Evidence:** `Code/UI/UiIcons.cs`; glob of `Assets/**/*.png` → 0 files.
- **Impact:** Empty brand mark; generic Material Icons for all build tiles — looks unfinished on store screenshots.
- **Required fix:** Add PNGs matching the stylized look, or stop referencing missing paths and commit to glyph-only UI intentionally.
- **Done when:** Brand mark renders a real logo; build dock icons are intentional art or intentional glyphs with no broken paths.

### P1-2 — No credits / controls / loading presentation
- **Problem:** No credits modal; no controls help on menu; boot is “Starting...” text only; no in-repo thumbnail/icon.
- **Evidence:** `Hud.razor` modal inventory; `GameBoot.EnsureBootScreen`; `.sbproj` has no Icon field.
- **Impact:** New players ask “what are the controls?”; release listing looks incomplete.
- **Required fix:** Controls blurb on menu or first tip; credits; store icon/thumbnail outside or in project.
- **Done when:** First-time player can learn camera/build controls without external docs; credits reachable; store art exists.

### P1-3 — Settings are audio-only
- **Problem:** Settings modal only binds Master/SFX/Ambience/Music (`AudioSettings`). No sensitivity, FOV, key display, graphics, or accessibility.
- **Evidence:** `Hud.razor` SettingsOpen block; no other settings systems in `Code/Core`.
- **Impact:** Players cannot tune camera feel or see controls; accessibility expectations unmet for a release.
- **Required fix:** Minimum: camera sensitivity + control list; optional FOV; document that graphics use engine defaults.
- **Done when:** Settings cover audio + at least camera sensitivity and a controls reference.

### P1-4 — Cure mode must pass a stability gate before removing warning
- **Problem:** Banner implies known breakage; Survival has many prior AUDIT FIX comments, Cure complexity is higher (seasons, rivals, tech, research, sickness).
- **Evidence:** Banner + size of `Code/Cure/*` surface.
- **Impact:** Shipping Cure without a dedicated pass recreates P0-1 in practice.
- **Required fix:** Structured Cure playtest (team × season × defeat/victory × save reload); fix P0/P1 bugs found; then remove banner.
- **Done when:** Cure completes a full win and checkpoint-defeat path without softlocks; banner removed or mode hidden.

### P1-5 — Night combat SFX intentionally silenced while combat director is elaborate
- **Problem:** Default `UseNightCombatMusicLoop = true` makes all `Sfx.Play`/`TryPlay` no-ops during Night (`Sfx.BlocksNightGameplaySounds`). `CombatAudio` director is unreachable at night.
- **Evidence:** `GameConstants.cs:27`; `Sfx.cs:36–47`; `CombatAudio.cs`; `NIGHT_COMBAT_MUSIC.md`.
- **Impact:** Nights feel music-only; gunfire/hits/kills give no local feedback — weak game-feel for the climax of the loop.
- **Required fix:** Product decision: keep music-only (document + ensure music always starts) **or** layer sparse combat SFX under music.
- **Done when:** Night climax has intentional, tested audio feedback matching the chosen design.

---

## E. Half-Implemented Features

1. **Wall-mount placement** — full branches in `BuildManager` gated by `AllowWallMountPlacement = false`; toast “Wall mounts are disabled for now.” (`GameConstants.cs:84`, `BuildManager.cs:334`).
2. **Weapon model cloud fallback** — box placeholder if `facepunch.sboxweapons` mesh missing (`WeaponModelLoader.cs:49,71`).
3. **Minimap** — `.minimap-stub` CSS only (`Hud.razor.scss:1075+`); no markup/logic.
4. **ComboSystem** — intentional no-op shell (`ComboSystem.cs`).
5. **OutpostManager.RepairAll / RepairCoreBy** — dead; UI uses paid `BuildManager.TryRepairAll` (`OutpostManager.cs` AUDIT note).
6. **Hud.AskReset** — method exists; wipe path is via main-menu Start New confirm, not Settings.
7. **Input.config** — full FPS template bindings unused by this top-down game.
8. **CombatAudio director** — complete code path disabled at night by music flag.
9. **Library vs School** — both Knowledge-only after same tech; School dominates (`TechTree.cs` literacy; `CureConstants` rates).

---

## F. Features That Exist but Do Not Yet Matter

### Zombie Bestiary / Catalog
- **Exists:** Discovery + kill counts + stats UI (`ZombieBestiary`, Catalog modal).
- **Why weak:** No milestone/objective reward for completion.
- **Should create:** Collectible completion decision / scrap reward.
- **Recommend:** Complete (small milestone) or simplify (accept as lore).

### Day/Night lighting (as gameplay)
- **Exists:** Visual blend (`DayNightLighting`).
- **Why:** No combat/economy coupling (intentional).
- **Recommend:** Keep as atmosphere; do not treat as a mechanic.

### Combo kill streak constants
- **Exists:** Tunables in `GameConstants` + no-op `ComboSystem`.
- **Recommend:** Remove or re-enable with HUD; do not leave half-dead.

### Public lobby creation
- **Exists:** Lobby name “The Final Outpost”.
- **Why:** No MP gameplay.
- **Recommend:** Remove/privatize (see P0-2).

### Production build tab in Survival
- **Exists:** Disabled tab (`Hud.razor:478`).
- **Why:** Confusing without tooltip.
- **Recommend:** Tooltip “Cure mode only” or hide in Survival.

---

## G. Missing Assets

### Models
- No project `.vmdl` buildings/props — all `MeshPrimitives` boxes/cylinders/pyramids (`PlacedBuilding.BuildVisual`, walls, bullets).
- Humanoids: stock `citizen.vmdl` + cloud weapons.
- **Priority:** P2 for distinct building silhouettes if store positioning needs it; primitives OK if style is committed.
- **Placeholder acceptable for launch?** Yes for Survival if marketing matches low-poly; no for “AAA” expectations.

### Materials / Textures
- Present: `fo_grass`, `fo_stone`, `fo_wood`, `fo_roof` only.
- No textures/png/vtex in repo.
- **Priority:** P2 if wanting unique building reads.

### Icons / UI art
- Missing: `ui/brand_emblem.png`, `ui/build_gun_tower.png`, `ui/build_cannon.png`, `ui/build_long_range.png`, `ui/build_wall.png`, `ui/build_barracks.png`, `ui/build_lab.png`.
- **Priority:** P1.

### Animations
- Citizen walk/aim/hold present; no recruit fire recoil; no turret muzzle kick; no death/ragdoll (destroy + `DestructionFx`).
- **Priority:** P2.

### Effects
- Blood-tint box bursts only; no muzzle flash, impact sparks, decals, hit markers, damage numbers.
- **Priority:** P2 for combat climax.

### Marketing art
- No in-repo logo, thumbnail, cover, loading splash, tutorial images.
- **Priority:** P1 for store.

---

## H. Missing Audio

| System / action | Gap |
|-----------------|-----|
| Night combat (default) | All SFX blocked; music only |
| Menu music | None (silence until day ambience) |
| Footsteps (workers/zombies) | None |
| Build place / sell / repair complete | Purchase click only / limited |
| Plot claim / clear complete | Minimal/none dedicated |
| Tech unlock / research tier | Purchase / wave clear reuse |
| Victory fanfare | Reuses clear SFX path |
| UI hover / error | Click only |
| Core critical warning | No dedicated alert loop |
| Night ambience without music flag | None if music disabled |

17 `.sound` files exist and are referenced; inventory is small but coherent for day + UI + music.

---

## I. Missing UI and Feedback

- Dead/orphaned: minimap stub CSS; unbound `AskReset` helper; disabled Production tab without explanation.
- Missing screens: credits, controls help, graphics/accessibility settings.
- Weak feedback: recruit fire with no recoil anim; turrets aim without muzzle FX; night SFX silence; no damage numbers/hit markers; brand mark empty.
- Boot failure path exists (`BootError` / recovery) — good.
- Empty states present for workers, expeditions, leaderboard.

---

## J. Role and Game Mode Gaps

### Modes
| Mode | Start | Objective | Win | Loss | Reset | Results | Gaps |
|------|-------|-----------|-----|------|-------|---------|------|
| Survival | Menu | Survive nights | Prestige / high score | Core destroy | Wipe run | Game Over + LB | No “endless end”; fine for roguelite |
| Road to a Cure | Team pick | Research cure | Victory | Season checkpoint | Soft restore | Victory / Game Over / Season Recap | Self-flagged broken; needs stability pass |

### Cure teams
All six teams have real multipliers (`TeamBonuses`) — not flavor-only. No MP roles. Single-player commander only — no co-op role gaps beyond P0 lobby issue.

### Survival vs Cure asymmetry
Day unit orders Cure-only (`UnitOrderController`). Survival players coming from Cure may expect click-move — tip/docs gap (P3).

---

## K. World and Level Gaps

### Areas With No Current Gameplay Purpose
- **Outer terrain beyond claimable plots:** Atmosphere + spawn ring only — OK; do not populate randomly without purpose.
- **Sea plane rim:** Visual boundary — OK.
- **Home arena courtyard:** Primary combat space — purposeful.
- **Unclaimed plots:** Purpose = claim/clear cost sink — purposeful.
- **Rival territory (Cure):** Purposeful (2× cost).

No empty interior “rooms” (no building interiors). World is one procedural arena — content depth is systems, not handcrafted levels.

Unfinished: no landmarks, no biome variety, no secondary maps (P3 content).

---

## L. Multiplayer and Reset Risks

| Risk | Severity | Notes |
|------|----------|-------|
| Public lobby, no sync | P0 | Confirmed |
| Mid-night quit | P2 | Night combat not resumable; day state saved |
| Camera position | P3 | Session-only |
| Round/night reset | OK | Survive → Day; wipe on defeat; Cure checkpoint |
| Duplicate rewards | Low risk | Single local player authority |
| Host-only assumptions | N/A | Entire game is local SP |

**Confirmed:** Designed and tested as single-player only.

---

## M. Content Gaps

| Content | Count / note | Launch need |
|---------|--------------|-------------|
| Maps | 1 procedural arena | OK for genre |
| Modes | 2 (1 flagged) | Decide scope |
| Enemy kinds | Multiple in bestiary | OK |
| Weapons | 5 distinct | OK |
| Upgrades | 6 | OK |
| Buildings | ~12 (Library≈School) | Merge/differentiate |
| Music | 1 ambience + 2 combat | Thin but OK |
| Dialogue | None | Not required |
| Difficulty modes | None | Post-launch OK |

**Must-have before launch:** Resolve Cure visibility; SP networking metadata; brand/store art; controls help; night audio decision.  
**Post-launch:** More maps/biomes, minimap, wall-mount, more music, catalog rewards.

---

## N. Remove, Merge, or Defer

| Item | Action | Why |
|------|--------|-----|
| ComboSystem + Combo* constants | Remove or fully restore | Dead maintenance surface |
| OutpostManager free repair APIs | Remove or private | Landmine vs paid repair |
| Minimap CSS stub | Remove or implement | Dead CSS |
| Wall-mount placement | Defer | Large polish; flag already off |
| Library building | Merge into School or differentiate | Dominated choice |
| Unused Input.config FPS binds | Trim | Template noise |
| True multiplayer | Defer | Massive scope; not required for SP ship |
| Custom building meshes | Defer if style committed | Not blocking Survival loop |

---

## O. Prioritized Completion Plan

### Stage 1 — Make the game fully playable
1. **P0** Decide Cure: hide, EA-label honestly, or finish + remove banner. Files: `Hud.razor`. Done: menu matches shippable modes.
2. **P0** Fix lobby/metadata to single-player. Files: `GameBoot.cs`, `.sbproj`. Done: no public MP ghost session.

### Stage 2 — Make every feature purposeful
3. **P2** Library vs School differentiation or merge. Files: `TechTree.cs`, `CureConstants`, `Buildable` catalog.
4. **P3** Catalog completion milestone. Files: `Milestones.cs`, `ZombieBestiary`.
5. **P3** Production-tab tooltip / hide in Survival. Files: `Hud.razor`.

### Stage 3 — Replace unacceptable placeholders
6. **P1** Brand + build icons (or intentional glyph-only). Files: `Assets/ui/*`, `UiIcons.cs`.
7. **P1** Store thumbnail / logo / credits screen.
8. **P2** Muzzle flash or recoil on recruit/turret fire. Files: `DefenderManager`, `PlacedBuilding`, `DestructionFx`.
9. **P1/P2** Night audio decision (music-only vs layered SFX). Files: `Sfx.cs`, `GameConstants`, `CombatAudio`.

### Stage 4 — Make multiplayer and resets reliable
10. Treat as SP complete after Stage 1 item 2. Document mid-night quit (session-only combat). Optional: warn on quit during Night.

### Stage 5 — Make the game understandable
11. **P1** Controls help on main menu / first tip. Files: `Hud.razor`, `TutorialTips`.
12. **P1** Expand Settings (sensitivity + controls list). Files: `Hud.razor`, `OutpostCamera`, `SaveData`/`PlayerProfile`.
13. **P1** Cure stability playtest + fix list before un-flagging.

### Stage 6 — Final release polish
14. Remove dead code (Combo, free RepairAll, minimap stub, unused inputs).
15. Enable wall-mount only if designed + tutorialized.
16. Balance pass Survival nights 1–15 and Cure year 1.

---

## P. Final Pre-Ship Checklist

- [ ] Main menu has no “broken/in development” copy for shippable modes
- [ ] Cure is either hidden, clearly EA, or playtested to Victory/defeat without softlocks
- [ ] `.sbproj` network type matches reality; lobby is not Public empty MP
- [ ] Survival: Day → Night → Recap → Day completes twice in a row
- [ ] Survival: core destroy → Game Over → wipe → new run works
- [ ] Survival: prestige path works and legacy scrap applies
- [ ] Save survives restart mid-Day; mid-Night behavior is documented
- [ ] Brand logo visible; no missing icon path warnings in normal play
- [ ] Build dock icons intentional
- [ ] Controls explained for camera + build + start night
- [ ] Settings: audio works; sensitivity or documented defaults
- [ ] Credits reachable
- [ ] Store thumbnail/description final (no template names)
- [ ] Night audio matches intentional design (music and/or SFX)
- [ ] Expeditions: dispatch, collect, unit loss edge case
- [ ] Workers produce; offline idle summary appears after ≥1 min away
- [ ] Daily reward + objectives + milestones grant spendable scrap
- [ ] Leaderboard submit on Game Over
- [ ] Boot failure shows player-visible error (force-test recovery)
- [ ] Dead systems removed or re-enabled (combo, free repair, minimap stub)
- [ ] Wall-mount either enabled+taught or code/docs cleaned
- [ ] Library/School not a dominated duplicate
- [ ] No lorem/debug strings in player UI
- [ ] Fresh install first-run journey completed by someone who never played

---

## Bottom line

**What must be done before safe release:** resolve Road to a Cure’s public “broken” status, stop advertising multiplayer you do not implement, ship brand/store/controls presentation, decide night combat audio, and treat Survival as the release-quality loop unless Cure gets a real stability pass.

Survival’s mechanical loop is largely ready; presentation, product-scope honesty, and networking metadata are what currently block calling this finished.
