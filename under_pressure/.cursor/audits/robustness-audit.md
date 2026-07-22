# Under Pressure — Game Robustness Audit

**Scope:** Code-only review of `C:\Users\Macra\Projects\sandboxProjects\under_pressure`. No code was modified. Claims tagged **verified** / **inferred** / **needs runtime repro**.

---

## 1. Executive Summary

The session loop for **levels 1–3** is relatively coherent: briefing → clean → 99% completion → van depart → next job, with pests freezing after completion and UI blocking during menus/cinematics.

The campaign is **not release-ready**. **Levels 4–25 are narrative stubs with zero panels**, and `IsComplete` requires `TotalCells > 0`, so normal play **hard-softlocks** after level 3. Economy integrity is weak: **cash is persisted without dirt progress**, so quit/reload farms cell payouts; pests resoil without clawing cash; prestige can leave `AwaitingDeparture` set and skip the next job. Hitman/gun and timed jobs are effectively dead systems. Networking is solo (`MaxPlayers = 1`) with no shared authority model.

---

## 2. Gameplay Robustness Score: **34 / 100**

| Band | Why |
|------|-----|
| −40 | Majority of campaign softlocks (stub jobs) |
| −15 | Save/reload + resoil cash duplication |
| −10 | Prestige / departure state desync |
| −8 | Dead hitman unlock + unused time limits |
| + partial credit | Levels 1–3 flow, knockout/depart guards, tool ownership checks |

---

## 3. Critical Bugs

### C1 — Stub jobs softlock the campaign (levels 4–25)
- **System:** Job catalog + completion
- **Evidence:** `CampaignCatalog.StubSite` sets empty `Panels`; comment at levels 4–25 rebuild section. `JobSiteManager.IsComplete` → `TotalCells > 0 && Progress >= 0.99`. `GameCore.TickCompletion` never sets `AwaitingDeparture`. Van UI stays on “FINISH THE JOB TO LEAVE”. `CheatInstantComplete` also refuses `TotalCells <= 0`.
- **Trigger:** Finish level 3 → depart → land on level 4+ stub.
- **Impact:** Permanent progression softlock (only `up_level` / reset / prestige escape).
- **Confidence:** **Verified** from code.

### C2 — Quit/reload duplicates cleaning payouts
- **System:** Save/load + economy
- **Evidence:** `SaveData` stores cash/job index/upgrades, **not** panel cleanliness. `SaveManager` autosaves every 20s (`GameConstants.AutosaveInterval`). Restart → `Jobs.LoadJob` rebuilds dirty surfaces; `CellsCleaned` → `Wallet.Earn` again.
- **Trigger:** Clean most of a job (cash autosaved) → quit → reload → re-clean.
- **Impact:** Arbitrary cash / run-earned / lifetime / leaderboard inflation.
- **Confidence:** **Verified**. Runtime only needed to confirm autosave timing feel.

### C3 — Prestige while awaiting departure skips the next job
- **System:** Prestige + departure state machine
- **Evidence:** `DoPrestige` → `TryPrestige` → `LoadJob(0)` clears upgrades/cash/job index but **does not clear** `AwaitingDeparture`. `DepartJob` only checks `AwaitingDeparture && !IsDeparting`.
- **Trigger:** Hit 99% → open shop → prestige → depart immediately on fresh level 1.
- **Impact:** Skip cleaning after prestige; free advancement.
- **Confidence:** **Verified**.

---

## 4. Likely Bugs

### L1 — Completion / perfect bonuses re-earnable across sessions
- Same as C2: bonuses are session-gated (`AwaitingDeparture`, `_perfectAwarded`) but **not saved**. Reload after 99%/100% awards → can award again.
- **Confidence:** **Verified**.

### L2 — Pest resoil is an infinite cash pump (in-session)
- **Evidence:** `JobSiteManager.WirePanels`: resoiling decrements `CleanedCells` with explicit “no cash clawed back”; re-clean pays again. Level 2–3 rats have `ResoilPeriod > 0`.
- **Trigger:** Let rats dirty cleaned patches; rewash forever.
- **Confidence:** **Verified** (intentional comment, still an exploit for release).

### L3 — Leaderboard / lifetime float truncation
- `LeaderboardService.SubmitEarned` casts `double` → `float`. Large prestige/helper totals will drift.
- **Confidence:** **Inferred** from API usage.

### L4 — Mid-job wave / contract content unreachable
- Mid-wave rivals require `Jobs.Index >= 4` (`EnemyManager.TickMidJobWave`). Contract spawns need unlock + index ≥ 24. Both sit on stub/unreachable path.
- **Confidence:** **Verified**.

### L5 — Stale catalog comment vs reality
- File header says levels 2–25 are narrative-only; levels 2–3 are fully authored. Docs/authoring confusion risk.
- **Confidence:** **Verified**.

---

## 5. Hidden Edge Cases

| Case | Behavior | Tag |
|------|----------|-----|
| **Joining late** | Lobby `MaxPlayers = 1` — no late join | Verified |
| **Disconnect** | `OnDestroy` saves + flushes stats; no reconnect protocol | Verified / inferred for host drop |
| **Dying / knockout** | 3 hits → blackout, cash %, tank/stamina drain, respawn at van; attacks ignored while KO | Verified |
| **Respawn** | `RecoverAtSpawn` / `LoadGeneration` snap — OK | Verified |
| **Saving / loading** | No dirt state → C2 | Verified |
| **Pausing** | No pause; menus block input; world freeze only for depart/briefing/discovery/fixer | Verified |
| **Lag / packet loss** | Solo local sim — N/A today | Inferred |
| **Duplicate / spam clicks** | Spend/equip gated; locked Depart/Prestige still clickable but no-op | Verified |
| **High/low FPS** | Cleaning/attacks use `Time.Delta` + clamps — generally OK; huge Δ may feel bursty | Inferred |
| **Zero players** | Host-only component bootstrap | Verified |
| **Max players** | Hard-capped at 1 | Verified |
| **Job wrap** | `LoadJob` modulo wraps catalog forever — irrelevant until stubs completable | Verified |
| **AFK helper income** | `OnUpdate` always earns `AutoIncomePerSecond * Prestige` → feeds `RunEarned` for prestige | Verified |
| **Tool switch mid-hold** | Sound restarts; cleaning uses current equipped tool | Verified |
| **Wrong tool on pest** | `TryDamage` no-ops; pest blocks surface behind | Verified |
| **Knockout during await depart** | Attacks/resoil stop when `AwaitingDeparture` | Verified |
| **Empty job + `up_complete`** | Explicitly fails | Verified |

---

## 6. Exploits

| Exploit | Flow | Severity | Tag |
|---------|------|----------|-----|
| **Reload farm** | Clean → autosave → quit → reload | Critical | Verified |
| **Resoil farm** | Leave pests alive, rewash | High | Verified |
| **Prestige skip** | Complete → prestige → depart | High | Verified |
| **Prestige keeps tools** | `OwnedTools` / equipped / hitman flags not cleared; van/upgrades are | Medium | Verified |
| **AFK prestige** | Hire Helper + leave game open | Medium (design?) | Verified |
| **Dev console** | `up_complete`, `up_level`, `up_reset`, `up_fixer` unrestricted | High if shipped to players | Verified |
| **UI “RESET ALL PROGRESS”** | In Overview — full wipe, easy misclick | Medium UX | Verified |
| **Gun buy without unlock** | UI hides gun; `TryBuy` does **not** check `RequiresHitmanUnlock` / contract flag | Low today (UI gated); latent | Verified |

---

## 7. Multiplayer Risks

**Current posture:** Solo-only. `GameManager` creates lobby `MaxPlayers = 1`, `Privacy = Public`.

| Risk | Detail | Tag |
|------|--------|-----|
| **No networked game state** | `GameCore`, wallet, jobs, enemies are local components — not replicated | Verified |
| **Raising MaxPlayers without rewrite** | Second client would not share job/cash/progress; duplicate worlds / desync | Inferred |
| **Public lobby name** | Discoverable empty 1-slot lobby — noise, not gameplay break | Inferred |
| **Late join / disconnect / reconnect** | Unsupported by design | Verified |
| **Authority** | Whoever runs `GameCore` is sole authority — fine for 1P | Verified |

---

## 8. Save/Load Risks

| Risk | Evidence | Tag |
|------|----------|-----|
| **No mid-job cleanliness** | `SaveData` fields omit surfaces/progress/`AwaitingDeparture` | Verified |
| **Cash without dirt** | Enables C2 | Verified |
| **Schema wipe** | Version &lt; 6 → full reset (`SaveManager.Migrate`) | Verified |
| **Corrupt JSON** | Catch → fresh save (progress loss, no backup) | Verified |
| **Write failure** | Logged warning only — player may think they saved | Verified |
| **`LastPlayedUnix` unused** | Dead field; daily uses `LastDailyDate` UTC | Verified |
| **Daily streak / timezone** | UTC day boundary; local midnight ≠ streak | Inferred |
| **Leaderboard migrate** | Sets `LeaderboardMigrated = true` before successful flush; failed migrate may never retry meaningfully | Verified |
| **Prestige partial reset** | Tools/discoveries/hitman flags persist across prestige | Verified |

---

## 9. Gameplay Consistency Issues

1. **22/25 levels are empty pads** with full story copy — briefing promises work that cannot exist.
2. **Hitman arc disabled:** `HitmanBriefingJobIndex = -1` → `EnsureForJob` never spawns fixer; gun/contract path unreachable without cheats.
3. **`TimeLimitSeconds` / `IsCombatLevel`:** Defined on `JobDef`, never read by simulation/UI.
4. **Prestige UI copy** says “reset upgrades & cash” — omits tools kept and job rewind.
5. **Completion at 99%** vs “spotless 100%” is clear; pests stop after 99% — good. Resoil cash policy fights that clarity.
6. **Campaign loops** via modulo with no end/credits — fine for endless idle, odd for a “25-level story.”
7. **Level 1 washer buff** only while `Jobs.Index == 0` — after prestige back to 0, buff returns (consistent) even with late-game tools (odd).

---

## 10. Missing Logic

- Fail-safe for **zero-panel jobs** (auto-complete, block advance, or hide from catalog).
- Persist **job cleanliness** and/or **completion flags** (or don’t pay until depart).
- Prestige must reset **session flags** (`AwaitingDeparture`, KO, menus) and decide **tool/story** policy.
- `TryBuy` must enforce **`RequiresHitmanUnlock`**.
- Enforce or remove **time limits** and combat flags.
- Re-enable or remove **fixer briefing** (`HitmanBriefingJobIndex`).
- Clawback or diminish **earnings on resoil** (or disable resoil payouts).
- **Idempotent completion/perfect awards** in save data.
- Multiplayer: don’t expand lobby until net architecture exists.
- Optional: gate or strip **dev ConCmds** / in-HUD full reset for public builds.

---

## 11. Recommended Fix Order

1. **Unblock campaign** — author or temporarily truncate catalog to playable jobs; stub jobs must not be enterable without a completion path.
2. **Fix economy integrity** — persist dirt progress *or* defer cell pay until depart *or* snapshot “earned cells” so reload can’t double-dip; fix resoil clawback/policy.
3. **Harden prestige transition** — clear `AwaitingDeparture` / UI / KO; explicitly reset or keep tools; reopen briefing after prestige.
4. **Save schema** — job progress + completion/perfect claimed flags; bump version + migrate.
5. **Restore or cut hitman/gun/time-limit systems** — no half-dead story gates.
6. **Softlock fail-safes** — `TotalCells == 0` detection; depart/debug messaging.
7. **Release hygiene** — restrict cheats; clarify prestige/reset UX; only then consider multiplayer.

---

### Mental test matrix (summary)

Solo loop on levels 1–3 mostly survives UI, knockout, and depart freezes. It fails **campaign continuity** (stubs), **persistence** (reload farm), and **prestige edge** (awaiting-departure skip). Multiplayer join/lag/reconnect are **out of scope** by `MaxPlayers = 1`, but the architecture would not survive enabling them without a full net rewrite.