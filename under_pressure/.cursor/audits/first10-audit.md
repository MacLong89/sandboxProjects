# Game First 10 Minutes Audit — Under Pressure

**Scope:** cold-open for a brand-new player with zero prior context. Implementation is authoritative. No code was modified.

**Evidence labels:** **[V]** verified in code · **[I]** reasoned inference · **[U]** unknown without playtest

---

## 1. Verdict

**Yes — for understanding and first fun inside ten minutes on the authored opening (levels 1–3).**  
**No — for a healthy “continue the campaign” loop once the player leaves level 3.**

**Why:** Boot skips menus and drops into a short story briefing, then the player can immediately hold LMB and see dirt clear and cash tick up. Level 1 is a safe, washer-only driveway with no pests and a boosted starter jet. That sells the fantasy fast.  
But levels **4–25 are empty `StubSite` pads with zero cleanable panels**, so `IsComplete` never becomes true and the player **cannot depart** — a hard softlock. That is outside a careful first-10 if they only finish L1, but it is the real campaign path and must be treated as opening-adjacent risk if they rush or use cheats.

---

## 2. Minute-by-minute beats

| Beat | What the player experiences |
|------|-----------------------------|
| **Launch → menu** | **No menu.** `game.scene` boots `GameManager` → solo lobby → `GameCore`. **[V]** `Assets/scenes/game.scene`, `Code/Networking/GameManager.cs` |
| **Load → spawn** | Save load → job build → player/van/enemies/HUD spawn → **mission briefing modal** blocks movement. **[V]** `GameCore.OnStart` |
| **0–90 sec** | Dismiss briefing → (often) daily reward popup → FPS look/move → hold **LMB** to spray. Cash climbs; progress bar moves. Hint: “Hold LMB to clean · Tab for menu · walk to the van”. **[V]** `Hud.razor`, `PressureWasher.cs` |
| **90 sec–3 min** | Core loop clear: wash driveway → earn cash. Optional: uncover “VOTE VANCE” → Leo discovery popup. Tab opens full shop (upgrades, prestige, leaderboard, reset). **[V]** L1 secrets in `CampaignCatalog.cs`; shop in `Hud.razor` |
| **3–6 min** | Likely still on L1 or finishing toward 99%/100%. First affordable upgrade (~$40 Pressure) possible after daily + some cleaning. **[I]** timing; **[V]** upgrade costs |
| **6–10 min** | Possible: finish L1 → van → depart (~5s blackout) → L2 briefing (“Late Night at the Car Wash”) with rats that **resoil** (no attack). Unlikely to finish L2+L3 in this window. **[I]** pace; **[V]** depart timings, L2 rats |

**Exact moments**

| Moment | Status |
|--------|--------|
| First control | After briefing (+ daily if granted) — ~15–45s from boot **[I]** |
| First understood goal | Briefing + job strip “X% CLEAN” — immediate after Start Job **[V]** |
| First reward | Cash per cleaned cells; daily ~$250 on first calendar day **[V]** |
| First fun | Dirt clearing under the jet (seconds after control) **[I]** |
| First meaningful choice | Spotless vs leave at 99%; buy Pressure vs save; Tab into full meta-shop **[I]** |

---

## 3. Time-to markers

| Marker | Estimate | Basis |
|--------|----------|--------|
| First control | ~2 UI clicks after boot (briefing → daily) | **[V]** gate order; **[I]** seconds |
| First understood goal | Same beat as control | Briefing + HUD job strip **[V]** |
| First reward | <$1 of cleaning, or daily on dismiss | `JobSiteManager` earn; `DailyRewardSystem` **[V]** |
| First fun | ~5–20s after control | Spray feedback **[I]** |
| First choice | ~1–4 min (upgrade / spotless / Tab) | **[I]** |
| Finish L1 | ~3–8 min new player | Large driveway panels **[I]** — **[U]** exact |
| Reach L4 softlock | After L1–L3 complete | Stub empty panels **[V]** |

---

## 4. Onboarding inventory

### Explicit teaching
| Item | Quality |
|------|---------|
| Mission briefing (title, act, location, body) | Story + vibe; not controls **[V]** |
| Bottom hint: LMB / Tab / van | Useful, always on, easy to miss **[V]** |
| Van focus card: “Right-click to open your van” | Contextual **[V]** |
| Pest guide + aim card | From L2; tool-to-pest mapping **[V]** |
| Missing-tool feed | When job needs unowned tool (not L1–3) **[V]** |
| Completion / spotless / knockout / discovery popups | Reactive, not progressive tutorial **[V]** |

**No tutorial, coach marks, first-run flag, or skippable guided steps.** Grep found none. **[V]**

### Implicit teaching
| Item | Notes |
|------|--------|
| L1: suburban driveway, washer-only, **no enemies**, 1.2× washer power/radius | Soft sandbox **[V]** `GameConstants.Level1*` |
| L2: rats resoil (`AttackStyle.None`) | Introduces pests without knockout **[V]** |
| L3: blood crime tone; rats still non-attacking | Story escalation **[V]** |
| `PestAttackUnlockJob = 2` | Hostile attacks from job index ≥2 only if `Attack != None` — rats never qualify **[V]** |
| Discoveries | Teach “wash to reveal story” by doing **[V]** |

### First-run defaults (`SaveData` + migrate)
| Default | Value |
|---------|--------|
| Cash | `0` (then daily can grant ~$250) **[V]** |
| JobIndex | `0` (level 1) **[V]** |
| Equipped / owned | Pressure Washer **[V]** |
| Upgrades | empty **[V]** |
| Hitman flags | false; briefing job index **disabled (`-1`)** **[V]** |
| Schema | Version 6; older saves wiped **[V]** `SaveManager` |

### Skippable / forced
- Briefing and daily are **forced until dismissed** (block `IsUiBlocking`). **[V]**
- Completing to 99% is forced to leave; 100% is optional. **[V]**

---

## 5. Opening friction list (ranked)

1. **Levels 4–25 softlock (empty stubs)**  
   - **Wrong assumption:** “Next job will be a real site.”  
   - **Evidence:** `StubSite` → empty `Panels`; `IsComplete => TotalCells > 0 && …` **[V]** `CampaignCatalog.cs`, `JobSiteManager.cs`  
   - Progress stuck at 0%; van “FINISH THE JOB TO LEAVE” forever.

2. **Double modal before the verb**  
   - **Wrong assumption:** “I should already be washing.”  
   - Briefing then daily (`DismissMissionBriefing` → `ShowDailyPopup`). **[V]**

3. **Full meta-shop on Tab from minute one**  
   - **Wrong assumption:** “I need Prestige / Leaderboard / Van Class / Reset now.”  
   - **Evidence:** `Hud.razor` Overview/Upgrades/Leaderboard/Van + RESET ALL. **[V]**  
   - Cheapest Pressure upgrade $40 is fine; Prestige ($6k run earn) and Hire Helper ($10k) are noise. **[V]**

4. **Story briefing denser than controls**  
   - **Wrong assumption:** “This is a narrative adventure first.”  
   - CrimeScene / RevealHook / TimeLimit / IsCombatLevel are **authored but unused by gameplay/HUD**. **[V]** only defined on `JobDef` / catalog.

5. **Van = RMB while looking (Attack2), not a key**  
   - **Wrong assumption:** “E / F interacts.”  
   - Hint says “walk to the van”; focus card says right-click. **[V]** `Van.cs`

6. **Water tank drain without explanation**  
   - **Wrong assumption:** “Washer is broken.”  
   - Tank UI exists; no first-run tip. **[V]** / tip absence **[V]**

7. **Stale catalog comment vs reality**  
   - File header says L1 authored, L2–25 narrative-only; **code has L1–L3 authored**. **[V]** Misleading for authors, not players.

8. **Hitman/fixer arc disabled**  
   - `HitmanBriefingJobIndex = -1` — no level-3 fixer onboarding. **[V]** Not first-10 friction unless expecting that beat.

---

## 6. Tutorial decision

### **Needs a simpler start, not a tutorial**

The core verb (aim + hold LMB → dirt gone → cash) is self-evident after one spray. A step tutorial would slow time-to-fun.

**Minimum design (progressive disclosure):**
1. **Keep** L1 as washer-only, no pests, boosted jet.  
2. **Defer** Tab shop depth: first session show Pressure / Nozzle / Tank only (or soft-gate Prestige/Leaderboard/Reset).  
3. **One coach line** after first spray or at 10%: “Return to the van when the bar fills — right-click it.”  
4. **Do not** tutorial pests until L2; keep rats as resoil-only.  
5. **Gate or stub-fix L4+** before any player can advance into empty pads (block advance, or skip stubs, or ship a minimal pad).

**Teach by doing:** wash → cash → 99% → van depart → discovery on stencil. Tell only: LMB, van RMB, optional “buy Pressure when you can.”

---

## 7. Recommended opening (lean beat sheet)

1. Boot → **short** L1 card: “Hose the driveway. Hold LMB.” (cut act/lore density or move lore to discovery).  
2. Skip or collapse daily into a toast after first cash tick.  
3. Spawn facing dirty driveway; van visible behind.  
4. 0–90s: wash; cash + %; optional Vance stencil → one Leo line.  
5. ~3–5 min: hit 99% → “Right-click van to leave” (or allow 100% bonus).  
6. Depart → L2 card one sentence + “Rats undo your work — blast them.”  
7. End of first session goal: **finish L1 and start L2**, with one Pressure upgrade as the only pitched meta choice.  
8. **Until L4–25 exist:** do not advance past L3, or ship a temporary “End of authored content” instead of StubSite.

---

## 8. Quick wins

1. **Softlock fix:** refuse `AdvanceToNext` into empty-panel jobs, or auto-skip stubs, or add a minimal cleanable pad. **[V]** cause known.  
2. **Merge briefing + daily** into one dismiss, or show daily after first cleaning.  
3. **First-run shop filter:** hide Prestige / Leaderboard / Reset / late tools until L2+ or cash threshold.  
4. **Stronger one-line control hint** on briefing button row: “Hold Left Mouse to wash.”  
5. **Van prompt at 99%** already exists — flash it earlier at 50% once.  
6. **Fix catalog comment** so authors don’t ship more stubs by mistake.  
7. Wire or remove dead fields (`TimeLimitSeconds`, `IsCombatLevel`, CrimeScene/RevealHook in UI) to avoid false expectations later.

---

## 9. Verification plan

**Playtest task (cold account / wiped save):**  
“Launch the game. Do not ask for help. Play for 10 minutes. Stop when you either finish a job and leave, get stuck, or quit.”

**Metrics (manual or telemetry later — none exist today [U]):**
| Metric | Target |
|--------|--------|
| % who spray within 90s of boot | ≥90% |
| % who understand goal (can state “clean the driveway”) by 3 min | ≥85% |
| % who reach first fun (report “satisfying clean”) by 5 min | ≥80% |
| % who complete L1 by 10 min | ≥60% |
| % who open Tab before first spray | note (friction signal) |
| Abandonment point | modal fatigue / tank confusion / post-L3 stub |
| If advanced to L4 | % softlocked (expect ~100% today) **[V]** logic |

**Exact runtime checks [U] until played:** L1 completion minutes; whether new players find van RMB; perceived length of briefing.

---

## Boot-to-play path (traced)

```
game.scene Boot/GameManager
  → Networking.CreateLobby (solo)
  → GameCore.OnAwake: SaveManager.Load → Wallet/Upgrades/Prestige/Tools/Jobs
  → GameCore.OnStart:
       Jobs.LoadJob(Save.JobIndex)
       DailyRewardSystem.Apply (cash may grant; popup deferred)
       SpawnPlayer / Van / EnemyManager / Pedestrians / Hud
       OpenMissionBriefing()  ← blocks input
  → Player: Start Job → optional Daily popup → FPS + Attack1 wash
  → 99% → completion UI → van Attack2 → DepartJob → blackout ~3.8s → next briefing
```

**Files:** `GameManager.cs`, `GameCore.cs`, `SaveManager.cs` / `SaveData.cs`, `PressurePlayer.cs`, `PressureWasher.cs`, `Hud.razor`, `JobSiteManager.cs`, `CampaignCatalog.cs`, `ToolSystem.cs`, `Van.cs`, `GameConstants.cs`

---

## Levels 1–3 (authored) vs 4–25 (StubSite)

| Level | Name | Playable content | Enemies |
|-------|------|------------------|---------|
| **1** | The Daily Grind | Driveway + strip panels, house/trees/fence, Vance secret | None **[V]** |
| **2** | Late Night at the Car Wash | Wash pad + Aegis secret, FoothillsStation decor, props | 3× Rat (resoil, no attack) **[V]** |
| **3** | The Red Flag | Blood ground + wall panels, steakhouse alley, THEY KNOW | 3× Rat **[V]** |
| **4–25** | Full story names/blurbs/acts | **Empty pad** — no panels/props/decor/enemies **[V]** `StubSite` | None |

**StubSite contract:** narrative metadata only; `GroundSize` 1400² grey pad; `Panels = []` → `TotalCells = 0` → never complete. **[V]**

**Disabled/unwired for campaign:** fixer briefing (`HitmanBriefingJobIndex = -1`); `TimeLimitSeconds` / `IsCombatLevel` unused outside data. **[V]**

---

### Verified / inferred / unknown (summary)

- **Verified:** No tutorial; boot→briefing→play; L1–3 geometry; L4–25 stubs; softlock math; defaults; controls; daily/briefing order; L1 washer boost; shop surface area; hitman disabled.  
- **Inferred:** Clock times to fun/L1 clear; player confusion modes; that 10 minutes usually ends mid-L1 or early L2.  
- **Unknown:** Real load hitch length; exact L1 minutes; whether players miss RMB van; live abandonment (no analytics).