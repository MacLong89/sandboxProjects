# Game Clarity Audit — Under Pressure

**Scope:** Implementation under `C:\Users\Macra\Projects\sandboxProjects\under_pressure` (Code/, UI, scenes, ProjectSettings, assets). No code modified. No analytics invented.

**Evidence labels used below:** **[V]** verified in code · **[I]** reasoned inference · **[U]** unknown without runtime play

---

## 1. Verdict

1. **Campaign softlock after level 3.** Levels 4–25 are `StubSite` jobs with **zero cleanable panels**. `IsComplete` requires `TotalCells > 0`, so the player can never reach 99%, never unlock Depart, and is stuck on an empty UrbanPlaza pad with story copy that promises combat/timed/forensic jobs. **[V]**
2. **Story and systems are ahead of the game.** Briefings, `CrimeScene` / `RevealHook`, `TimeLimitSeconds`, hitman/gun unlock, and multi-tool `FollowUp` exist in data or UI, but are disabled, unused, or unreachable in normal play. Players trust the briefing, then find nothing to clean. **[V]**
3. **HUD lies on unfinished / finished states.** On stubs (and when a job is fully clean), `ToolMatchesJob` is false → persistent “Wrong tool” warning while holding the pressure washer. Bottom hint says “walk to the van” but interaction is look + RMB. **[V]**
4. **Core loop of levels 1–3 is readable** (spray, progress, van depart, upgrades), but secondary systems (scrub/squeegee shop, prestige, leaderboard, reset, pests) compete for attention before they matter. **[I]**

---

## 2. Findings table

| Issue | Category | Severity | Frequency | Evidence | Player impact |
|-------|----------|----------|-----------|----------|---------------|
| Levels 4–25 empty; cannot complete / depart | broken | **P0** | All who finish L3 | `CampaignCatalog.StubSite` empty `Panels`; `JobSiteManager.IsComplete` needs `TotalCells > 0`; `GameCore.DepartJob` needs `AwaitingDeparture` | Softlock; campaign dead |
| `up_complete` also fails on stubs | broken | P1 | Devs / stuck players | `GameCore.CheatInstantComplete` returns if `TotalCells <= 0` | Only `up_level` escapes |
| Briefing vs empty pad (L4–25) | confusing | **P1** | All who reach L4 | Briefings in `CampaignCatalog`; empty site via `StubSite` | Feels broken / unfinished |
| `TimeLimitSeconds` never enforced | broken | P1 | L11+ narrative | Field on `JobDef`; **no readers** in Code/ | “Clock ticking” with no clock |
| `CrimeScene` / `RevealHook` never shown | confusing | P2 | All levels | Authored in catalog; HUD shows `Briefing`/`Location`/`Act` only | Story intent invisible |
| Hitman briefing disabled (`HitmanBriefingJobIndex = -1`) | broken | P1 | All normal play | `GameConstants`; `HitmanBriefingNpc.EnsureForJob` early-out | Gun / contract path unreachable |
| Gun needs `HitmanContractUnlocked`; only briefing/cheat sets it | broken | P1 | Normal play | `ToolCatalog` + `DismissHitmanBriefing` / `CheatJumpToLevel` | Classified tool dead content |
| No panel uses `FollowUp` (scrub/squeegee) | confusing | P2 | Anyone opening van shop | Grep: no `FollowUp =`; L1–3 washer-only stages | Buys tools that never unlock surfaces |
| L2 copy promises windows/scrub; one floor panel only | confusing | P2 | Level 2 | `CampaignCatalog` L2 blurb vs `Panels` | Wrong mental model of job |
| Permanent “Wrong tool” when `TotalCells == 0` or job spotless | confusing | P1 | Stubs + post-complete | `Hud.razor` `ToolMatches` ← `ToolMatchesJob` | Distrust HUD; noise |
| Hint: “walk to van” vs RMB look-interact | confusing / friction | P2 | Early players | `Hud.razor` hint; `Van.UpdateInteraction` Attack2 | Miss tool swap / depart |
| Tab “Van” = van class upgrade; world van = tools | confusing | P2 | Menu users | `Hud.razor` Van tab vs van locker | Wrong van for tools |
| RESET ALL PROGRESS one click, no confirm | friction / trust | P1 | Anyone opening Overview | `Hud.razor` Overview | Accidental wipe |
| Prestige copy incomplete (keeps tools/flags; resets job to 1) | confusing | P2 | Prestige users | `PrestigeSystem.TryPrestige`; HUD desc | Unexpected story/tool state |
| Pest re-soil / attack with no “wrong tool on surface” feedback | feedback | P2 | L2–3 | `CleanAt` returns 0 silently; pests `Resoil` | Spray “does nothing” feel |
| Left feed stacks pests + tips + harassment | clutter | P2 | L2–3 with rats | `Hud.razor` left-feed | Obscures primary progress |
| Huge cash (46px) always on; upgrades before needed | clutter | P3 | Always | `Hud.razor.scss` `.cash` | Economy noise early |
| Ambient peds MaxActive=10 on stub UrbanPlaza | clutter / perf | P2 | L4–25 | `AmbientPedestrianDensity` + stub `Theme` | Busy empty pad |
| WorldMap ~7200 + 28 horizon boxes every job | performance | P2 | Every load | `GameConstants.DefaultMapSize`; `WorldMapBuilder` | Hitch / draw cost **[U]** FPS |
| Grime masks up to 512² + upload on clean | performance | P2 | Cleaning | `CleanableSurface` MaskCap / `UploadRegion` | Frame dips while spraying **[U]** |
| Spray droplet pools (~46 GOs) every spray frame | performance | P3 | Spraying | `PressureWasher` pools | Extra cost **[U]** |
| Public lobby MaxPlayers=1 on start | risk | P3 | Session start | `GameManager` | Odd network setup; solo only **[I]** |
| Gun use-sound = `button.sound` | feedback | P3 | If gun unlocked | `ToolCatalog` Gun `UseSound` | Weak shoot feedback |
| Glass surfaces use metal material | feedback | P3 | Future glass jobs | `JobWorldBuilder` `CleanSurface.Glass` → Metal | Visual mismatch |
| Save wipe on schema &lt; 6 | broken / trust | P2 | Old saves | `SaveManager.Migrate` | Progress loss on update |
| Job index wraps modulo after catalog | confusing | P3 | After L25 if force-advanced | `JobSiteManager.LoadJob` | Silent loop to L1 |

---

## 3. Broken systems

### P0 — Stub campaign softlock (reproducible defect)
**Trigger:** Complete L1–3 → Depart → load level 4+ (`StubSite`).  
**Mechanism:** `Panels = []` → `TotalCells = 0` → `IsComplete` false forever → `AwaitingDeparture` never set → Depart button stays “FINISH THE JOB TO LEAVE”.  
**Files:** `Code/Cleaning/CampaignCatalog.cs` (`StubSite`), `Code/Cleaning/JobSiteManager.cs`, `Code/Core/GameCore.cs`, `Code/UI/Hud.razor`.  
**Escape:** Console `up_level N` or Overview “RESET ALL PROGRESS” / `up_reset`. `up_complete` **does not** work (`TotalCells <= 0`).

### P1 — Narrative systems not implemented
| System | Authored | Runtime |
|--------|----------|---------|
| Timed jobs | `TimeLimitSeconds` (e.g. L11 = 420) | **Never read** |
| CrimeScene / RevealHook | On every `JobDef` | **Not in HUD** |
| Multi-tool layers | `FollowUp` on `PanelDef` | **Never set** on any job |
| Hitman fixer | NPC + briefing UI | `HitmanBriefingJobIndex = -1` → never spawns |
| Gun / contract pests | Tool + enemy kinds | Need `HitmanContractUnlocked`; normal path blocked |
| Combat levels | `IsCombatLevel` on stubs | No special combat logic found beyond pests |

### P1 — Completion / cheat gap on stubs
`CheatInstantComplete` refuses empty jobs → developers stuck the same way as players unless `up_level`.

### Likely defect — “Wrong tool” after job done / on stubs
`ToolMatchesJob` = any surface `HasWorkFor(equipped)`. Empty or fully clean → false → mismatch chip + warn. **[V]** logic; **[U]** how often noticed in play.

### Risk — AwardCompletionBonus not internally idempotent
Guarded only by `!AwaitingDeparture`. Safe in current flow; fragile if state is reset incorrectly later. **[I]**

### Risk — Public solo lobby
`Networking.CreateLobby` Public, MaxPlayers=1. Unclear if that confuses s&box session UX. **[U]**

### Verified save behavior
Corrupt/old saves: load failure → fresh; version &lt; 6 → wipe. Autosave every 20s + on destroy/key events. **[V]**

---

## 4. Confusion map

| Element | Wrong assumption player makes |
|---------|-------------------------------|
| Mission briefing L4–25 | “I start a big combat/crime scene job” → empty gray pad |
| Progress “0% CLEAN” + locked Depart | “I missed the dirt” / “bug” — actually nothing to clean |
| Bottom “Wrong tool” on stubs / 100% jobs | “My washer is wrong” — matcher has no work left |
| “walk to the van to swap tools” | Walking near van is enough — need aim + **RMB** |
| Tab → Van | Opens tool locker — only Van Class upgrade |
| L2 blurb (windows / scrub) | Need scrub brush — only washer floor panel |
| Scrub/Squeegee in van (costs 900 / 2400) | Required soon — never required by current panels |
| Classified gun (if somehow seen) | Unlocks via story — briefing index disabled |
| Pest guide “Use X tool” | Wrong tool = visible fail — wrong tool does **nothing** (silent) |
| Overview “Earn Multiplier” | Explains van + prestige + job pay — dense; Hire Helper = AFK cash while session open only |
| Prestige | Soft reset of meta — also jumps story to level 1; keeps owned tools / hitman flags |
| Daily popup after briefing | Separate “login gift” — can feel like random cash |
| ★ prestige chip | Another currency — it’s earnings multiplier only |
| RESET ALL PROGRESS next to Overview stats | Soft “new game” with confirm — one-click wipe |
| Completion at 99% | Job done = leave — still prompted to grind to 100% for perfect (intentional but easy to miss timing) |

---

## 5. Clutter and noise cuts

### Remove / hide until needed
- **Pest guide** until first pest job (L2+) or first pest on screen — OK gated by count, but shrink when &gt;3. **[V]**
- **Prestige chip / prestige panel** until first prestige unlock proximity (e.g. RunEarned ≥ 50% of requirement).
- **Scrub / Squeegee / Gun upgrades groups** until those tools owned or a job needs them.
- **Leaderboard tab** from first-session critical path (defer).
- **RESET ALL PROGRESS** from Overview → Settings / hold-to-confirm (or remove from player-facing UI).
- **Ambient pedestrians** on `StubSite` / empty play pads (or cap UrbanPlaza density when `Panels.Count == 0`).

### Defer
- Daily reward until after first successful clean or first depart (currently after first briefing dismiss — OK but stacks modals).
- Discovery monologues: fine; avoid stacking with harassment.

### Merge
- Tab “Van” + world van locker into one mental model (“Work Van”: tools + depart + class upgrade).
- “Wrong tool” warn + MissingTool card → single primary CTA.

### Shrink
- Cash number (46px) early game; keep progress strip as primary.
- Bottom perpetual control hint → short first-job coach, then hide.

### Promote (HUD priority order)
1. **What to do now** (clean / buy tool / return to van / dismiss briefing)  
2. **Job progress** + level name  
3. **Equipped tool** + resource (water/stamina)  
4. **Cash** (secondary)  
5. Pest aim cue when looking at pest  
6. Shop / prestige / leaderboard  

---

## 6. Optimization targets

Ranked by cost/benefit (clarity + perf):

1. **Don’t load stub jobs as playable** — gate campaign, ship “Coming soon”, or auto-skip empty jobs. Fixes softlock + wasted world build. **High / Medium**
2. **Skip or shrink `WorldMapBuilder` for stubs** — still pays for 7200 map + horizon + UrbanPlaza peds. **High / Low–Med**
3. **Reduce ambient ped density when no panels / combat** — especially stubs. **Med / Low**
4. **Throttle grime `Texture.Update`** (dirty rect coalesce / lower MaskDensity on large panels). **Med / Med**
5. **Simplify spray VFX** when not looking at cleanable hit. **Low / Low**
6. **Profile job load hitch** (Scenery houses + map) — **[U]** measure before micro-optimizing boxes.

Flow wins (not FPS): one-click Depart when focused on van after complete; remember last menu tab; confirm wipe.

---

## 7. Prioritized fix list

| # | Fix | Effort | Expected result |
|---|-----|--------|-----------------|
| 1 | **Stop softlock:** treat empty jobs as non-playable (block advance past last authored job, or temporary skip/`AwaitingDeparture` for stubs with “site under construction”) | S–M | Campaign doesn’t brick |
| 2 | Align catalog comment with reality: only L1–3 authored; hide L4–25 from progression until built | S | Honest scope |
| 3 | Fix `ToolMatches` / mismatch UI when `TotalCells==0` or `AwaitingDeparture` / spotless | S | HUD trust |
| 4 | Fix van hint: “Look at van · **RMB** open locker” | S | Discoverability |
| 5 | Confirm dialog on RESET ALL PROGRESS | S | Trust |
| 6 | Show or delete `CrimeScene`/`RevealHook`; implement or remove `TimeLimitSeconds` | M | Story ↔ gameplay |
| 7 | Re-enable hitman on a real job index **or** remove gun from catalog until ready | M | No dead systems |
| 8 | Add ≥1 `FollowUp` panel **or** hide scrub/squeegee until needed | M | Tool shop makes sense |
| 9 | Unify L2 copy / panels (windows vs floor) | S | Briefing honesty |
| 10 | Merge Van tab + locker UX | M | Less menu confusion |
| 11 | Stub ped/map cost cuts | S | Perf + clarity on empty levels |
| 12 | Surface wrong-tool feedback (pulse / audio when `CleanAt` = 0 on cleanable) | M | Action clarity |

---

## 8. Quick wins

1. Cap progression at last authored job (index 2) until stubs exist — **or** temporary auto-complete empty jobs with a clear “site not ready” card.  
2. Suppress tool-mismatch warning when `TotalCells == 0` or `AwaitingDeparture`.  
3. Rewrite bottom hint for van RMB.  
4. Confirm on RESET ALL PROGRESS.  
5. Strip UrbanPlaza ambient burst on stubs (`Panels.Count == 0`).  
6. Soften L2 blurb to match the single wash pad.  
7. Hide scrub/squeegee purchases (or mark “Coming soon”) until a FollowUp job ships.  
8. Mission briefing: if `Panels.Count == 0`, show “This site is being rebuilt” instead of full combat fantasy.  

---

## 9. Verification plan

| Fix area | How to verify |
|----------|----------------|
| Softlock | Fresh save → finish L1–3 → depart → on L4: can leave OR see explicit gate; no infinite 0% lock. Console: no reliance on `up_level`. |
| Stub honesty | Briefing text matches empty/blocked state; Depart not permanently locked without explanation. |
| Tool HUD | On L4 stub and on L1 after 100%: no “Wrong tool” with washer equipped. Mid-clean with washer: no false warn. |
| Van interact | New player playtest task: “Open van and equip scrub brush” without hint text — success rate; then with updated RMB hint. |
| Reset | Click RESET once → confirm cancel keeps save; confirm OK → L1 + cash 0. |
| Timers | If implemented: L11 shows countdown; fail/success defined. If removed: briefing no longer mentions clock. |
| Hitman/gun | Either fixer appears on intended level and gun appears in van after dialogue, or gun hidden and no classified copy. |
| FollowUp | Job that needs scrub: MissingTool card → buy → equip → clean residue; wrong tool silent fail replaced by feedback. |
| Perf | `[U]` Profile job load and spray on L1 driveway + L2 gas station (frame time / hitch). Compare stub load before/after ped+map trim. |
| Save | Load v5 save → expect wipe (document in patch notes). Autosave survives kill process mid-job. |
| Prestige | Prestige → L1, cash 0, upgrades 0, tools still owned; multiplier chip updates. |

**Playtest tasks (no invented metrics):**  
1) “Finish first job and leave in the van.”  
2) “Without reading chat, open the van locker.”  
3) After L3, “What is your next objective?” (expect confusion on stub unless gated).  

---

## Evidence notes (facts vs inferences vs unknowns)

**Verified facts [V]**  
- Solo boot: `GameManager` → lobby → `GameCore` runtime spawn of player/van/enemies/peds/HUD.  
- Campaign: L1–3 authored; L4–25 `StubSite` empty panels/props/enemies.  
- Completion gate: 99% of `TotalCells`; depart only via van when `AwaitingDeparture`.  
- Hitman: `HitmanBriefingJobIndex = -1`.  
- No code references consuming `TimeLimitSeconds`, `CrimeScene`, or `RevealHook` for UI/logic.  
- No `FollowUp =` assignments in catalog.  
- Tools: washer starter; scrub/squeegee purchasable; gun gated.  
- HUD surfaces cash, job strip, tool chip, water/stamina, left feed, menus, briefings, discoveries, knockout, daily.  
- Inputs: Attack1 spray, Attack2 van, Tab (Score) menu, Escape closes overlays.  

**Inferences [I]**  
- Softlock is the dominant live clarity failure for anyone past Act I.  
- Players will blame the tool/HUD before realizing the site is empty.  
- Van “walk” wording causes missed RMB interactions.  
- Economy/prestige UI is early noise relative to spray-to-progress loop.  

**Unknowns [U]**  
- Actual FPS/hitches on target hardware.  
- Whether Public MaxPlayers=1 lobby causes visible s&box UX issues.  
- How often players buy scrub/squeegee before they matter.  
- Audio asset quality / missing mp3 behind `.sound` (paths exist under `Assets/sounds/`).  
- Whether ambience `sounds/ambience.mp3` always loads (caught in try/catch).  

---

**Bottom line:** Treat levels 4–25 as **non-shippable playable content** until panels exist. Until then, gating and HUD honesty matter more than polishing upgrades, prestige, or leaderboard. Levels 1–3 already teach the core verb; don’t let empty stubs and lying tool warnings undo that trust.