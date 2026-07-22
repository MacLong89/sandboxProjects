# Game Retention Audit — Under Pressure

**Evidence basis:** code inspection only. No live telemetry. Behavioral claims are **hypotheses** unless marked as verified from implementation.

---

## 1. Verdict

**Not yet an effective Play Fund hour engine.** The first ~1–3 jobs deliver a clear fantasy (`hose grime → cash → upgrades → next site`) with juice, persistence, and return scaffolding (daily, prestige, leaderboard). After that, the campaign intentionally ships **empty stub sites** for levels 4–25 — and empty jobs can never reach the 99% complete threshold — so unique players, return days, and session depth all collapse at the first content cliff.

For clamped player-hours, the bottleneck is **activation → meaningful job completion past L3**, not prestige or dailies.

---

## 2. Inferred pitch and archetype

| | |
|---|---|
| **Fantasy** | You’re a pressure washer who keeps uncovering something darker under every stain. |
| **Archetype** | First-person cleaning / ASMR-adjacent loop + idle upgrades + narrative “crime scene” reveal, with pest combat and a late hitman arc designed in. |
| **Target** | Casual–midcore s&box players who want a satisfying core verb, short jobs, and light meta progression. |
| **Promise fit** | Title + HUD brand (“UNDER PRESSURE”) + L1 briefing (“suburban driveway”) align. Act titles / conspiracy copy promise a thriller campaign the **playable geometry does not yet deliver** past L3. |

**Verified:** 25-job catalog with full narrative metadata; only L1–L3 fully authored sites (`CampaignCatalog.cs`).

---

## 3. Minute-by-minute first session (inferred)

### 0–90 seconds
1. `GameManager` creates a **solo** lobby (`MaxPlayers = 1`) and spawns `GameCore`.
2. Mission briefing modal blocks play (`OpenMissionBriefing` → “Start Job”).
3. Player is on L1 driveway with boosted washer (`Level1WasherPower/RadiusMultiplier`), **no pests**.
4. Hint: Hold LMB · Tab menu · van for tools (`Hud.razor`).

**Hypothesis:** Most players who dismiss the briefing can spray within ~30–60s. Briefing is light friction, not a welcome wall.

### ~2–5 minutes
- Cleaning pays per cell (`JobSiteManager` → `Wallet.Earn`).
- Cash ticks in top bar; first PSI / nozzle purchase in Tab shop is cheap (Pressure base **$40**).
- Possible L1 secret: `VOTE VANCE` monologue (`NotifyDiscovery`).
- Daily popup appears **after** first briefing dismiss if login reward granted.

**Hypothesis:** First “fun” = dirt clearing visually; first meaningful reward = cash + optional discovery; first upgrade often within first job.

### ~5–10 minutes
- L1 complete at **99%** → bonus + “return to van / push 100% for perfect” (`GameCore.TickCompletion`).
- Van depart cinematic → L2 briefing.
- L2 introduces rats (resoil) — first skill check without hostile attacks yet (`PestAttackUnlockJob = 2` → hostility from **job index 2 = L3**).

### ~10–20 minutes
- L2–L3 deepen story + blood/crime tone + secrets (`THEY KNOW`).
- Hostile pests from L3; knockout after 3 hits + cash penalty.
- **Then L4 stub:** empty pad, `Panels = []` → `TotalCells == 0` → `IsComplete` forever false → **softlock**.

**End of session hook (intended):** unfinished job, spotless bonus, next briefing, daily streak, prestige bar. **Actual after L3:** stuck empty site or quit.

---

## 4. Core-loop diagnosis

**Verb chain:**  
`spray/scrub → cash (cells + bounties) → spend (upgrades / tools / van) → unlock power or surface access → next job / prestige → repeat`

| Loop stage | Approx duration (hypothesis) | Notes |
|---|---|---|
| Micro clean | 1–5s bursts | Water/stamina gates create micro-rests |
| Job | ~5–15 min early | L1–L3 panel sizes; pests add time |
| Spend | 30–90s in Tab/van | Clear feedback on purchase SFX |
| Job→job | ~5–8s depart | Blackout + van SFX + briefing |
| Prestige | Multi-session | $6k × 4ⁿ run earnings |

**Strengths:** Clear goals (job strip %), visible power (PSI/nozzle/tank), juice (spray VFX, completion cards, discovery monologues), agency (99% leave vs 100% perfect).

**Stalls:**
1. **L4+ empty jobs** — catastrophic.
2. Pest **resoil + respawn** without enough power → treadmill without a new decision.
3. Mid-job rival wave only from job index ≥ 4 (`EnemyManager.TickMidJobWave`) — wired for content that isn’t playable yet.
4. Authored but unused: `TimeLimitSeconds`, `IsCombatLevel`, `CrimeScene`, `RevealHook` (never referenced outside catalog defs).
5. Hitman briefing disabled (`HitmanBriefingJobIndex = -1`) while gun/contract still exist behind dead flags.

---

## 5. Scorecard (1–5)

| Dimension | Score | Evidence |
|---|---|---|
| Promise clarity | **3** | Strong cleaning fantasy; thriller campaign oversold vs playable levels |
| First 90 seconds | **4** | Briefing → LMB clean; L1 pest-free + washer boost |
| First 10 minutes | **4** | Cash, upgrades, completion, van depart; discovery optional |
| Core-loop strength | **4** | Solid spray→earn→upgrade while content exists |
| Feedback / game feel | **4** | Spray mesh, HUD progress, SFX, completion/perfect, harassment |
| Goal / upgrade pacing | **3** | Early costs good; Helper $10k / Van steep; prestige 4× growth |
| Agency / mastery | **3** | Tool match, pests, perfect optional; combat/timed modes not live |
| Replayability | **2** | Prestige + secrets thin (few IDs); jobs wrap with stubs |
| Return systems | **3** | Daily streak, prestige, leaderboard present; no seasons/events |
| Social retention | **1** | Solo lobby only; LB is async comparison only |
| Sustainable content depth | **1** | 22/25 jobs are narrative stubs with no panels |
| Player-hours potential | **2** | High if L4–10 authored; currently capped at ~1–3 jobs |

---

## 6. Three strongest retention mechanisms

1. **Satisfying core verb + immediate cash** — `PressureWasher` + per-cell earn + HUD progress (`JobSiteManager`, `Hud.razor`).
2. **Job cadence with optional perfection** — 99% leave / 100% perfect bonus (`GameConstants.JobCompleteThreshold`, `PerfectBonusFactor`).
3. **Meta layer already scaffolded** — upgrades, daily streak, prestige multiplier, lifetime leaderboard (`UpgradeSystem`, `DailyRewardSystem`, `PrestigeSystem`, `LeaderboardService`).

---

## 7. Five largest retention leaks (ranked)

### 1. Severity: Critical — Campaign softlock at L4+
**Evidence:** `CampaignCatalog.StubSite` sets empty `Panels`; `JobSiteManager.IsComplete` requires `TotalCells > 0` and ≥99% progress.  
**Play Fund impact:** Unique engaged players never get a 2nd/3rd session goal; session depth caps after L3.  
**Hypothesis:** Majority of progressing players churn on empty pad with no depart unlock.

### 2. Severity: Critical — Promise vs playable depth mismatch
**Evidence:** Full Act I–IV story in catalog; comment: “levels 2–25 keep narrative metadata only — physical sites rebuilt…”; L4–25 are stubs.  
**Impact:** Trailer/thumbnail/conspiracy pitch converts players the game cannot feed.

### 3. Severity: High — Return hooks exist but have little content to return *to*
**Evidence:** Daily + prestige + LB work; campaign has ~3 real sites; prestige resets to L1 and replays the same short authored set.  
**Hypothesis:** D1 may be OK from dailies; D7 collapses without new jobs.

### 4. Severity: Medium — Social multiplier is near zero
**Evidence:** `GameManager` `MaxPlayers = 1`; no co-op, parties, or shared jobs. LB is lifetime $ only.  
**Impact:** No invite loop; Play Fund unique players won’t multiply via friends.

### 5. Severity: Medium — Designed systems dormant / half-wired
**Evidence:** `TimeLimitSeconds` / `IsCombatLevel` unused; `HitmanBriefingJobIndex = -1`; gun requires unlock that briefing never grants in normal flow; scrub/squeegee priced but early panels lack `FollowUp`.  
**Impact:** Mastery curve and “new capability” beats don’t fire when players need them most (post-L3).

---

## 8. Prioritized fixes

| Priority | Fix | Impact | Effort | Expected behavior change | Success metric |
|---|---|---|---|---|---|
| P0 | Author **L4–L6** as real panels/enemies (or temporary skip stubs → next authored) | Unblocks campaign | High / Med | Players finish session with “next job” unfinished goal | % reaching L4 complete; session p50/p90 |
| P0 | Softlock guard: if `TotalCells == 0`, auto-advance or show “site under construction” + skip | Stops rage-quit | Low | No stuck 0% jobs | Softlock rate → 0 |
| P1 | Surface **RevealHook** on HUD when discovery fires; show CrimeScene in briefing | Story retention without new maps | Low | More discovery dismissals; longer early sessions | Discovery rate; briefing→clean time |
| P1 | Re-enable fixer on a live job index or gate gun differently | Unlocks combat fantasy | Med | Tool unlock + contract goals | Hitman unlock rate; gun equip rate |
| P2 | Prestige: keep 1–2 upgrade levels or vanity (van tier) so reset feels like investment | Return after prestige | Low–Med | Higher prestige attempt + post-prestige play | Prestige starts; return within 7d |
| P2 | Daily: streak-safe alternative (e.g. “catch-up chest” once) so miss ≠ punish | Habit without dark pattern | Low | Higher D1–D3 open rate | Daily claim rate; streak distribution |
| P3 | 2-player “crew clean” lobby | Social multiplier | High | Longer sessions with friend | Duo session length vs solo; invites |
| P3 | Leaderboard seasons / weekly job score (not only lifetime $) | Recurring rivalry | Med | Weekly returns | Active days / player |

Avoid: more AFK idle, punitive streak-only systems, or prestige without new sites.

---

## 9. 30-day experiment plan

**Week 1 — Stop the bleed**
- Ship softlock skip OR L4 real site.
- Instrument funnels (below).
- **A/B (or sequential):** briefing length short vs current.
- Success: Start → first cell clean > target; L3→L4 progression > 0.

**Week 2 — Session depth**
- Ship L5–L6; wire one timed job (`TimeLimitSeconds`) on Dark Highway when built.
- Tune pest resoil on L2–L3 if completion times spike.
- Success: p50 session length ↑; jobs completed / session ↑.

**Week 3 — Return**
- Daily reward visibility on title/boot; prestige soft keep (e.g. van tier).
- Collection panel: list discovered secret IDs in Overview.
- Success: D1 retention ↑; daily claim among returners ↑.

**Week 4 — Leverage & social probe**
- One “weekly featured job” modifier (pay ×1.5, extra pest).
- Soft invite: “ghost rival” using LB percentile text (no co-op yet) **or** prototype 2P wash if cheap.
- Success: active days / player; optional friend join rate.

Smallest testable first: **stub skip + L4 authored pad** beats any meta tweak.

---

## 10. Instrumentation plan

**Verified today:** only `lifetime_earned` via `Sandbox.Services.Stats` (`LeaderboardService`). **No** funnel/session/retention analytics in code.

### Events (propose)

| Event | Props |
|---|---|
| `session_start` | prestige, job_index, cash, streak |
| `briefing_dismiss` | job_index, dwell_ms |
| `first_clean` | t_since_start_ms, job_index |
| `upgrade_buy` | id, level, cost, job_index |
| `tool_buy` / `tool_equip` | type |
| `job_complete` | job_index, progress, spotless, duration_s, pest_hits |
| `job_depart` | job_index |
| `discovery` | id, job_index |
| `pest_knockout` | job_index, penalty |
| `daily_claim` | streak, amount |
| `prestige_attempt` / `prestige_done` | level, run_earned |
| `softlock_empty_job` | job_index *(if TotalCells==0 for >60s)* |
| `session_end` | duration_s, jobs_completed, reason |

### Funnels / metrics (Play Fund–aligned)

- **Start → core verb:** `session_start` → `first_clean` (target &lt; 90s).
- **Time to first reward / upgrade:** first `Earn` / `upgrade_buy`.
- **Tutorial-ish steps:** briefing dismiss → first clean → first shop open → first upgrade.
- **Session length distribution** (p25/p50/p90), not only mean.
- **Jobs completed / session**; **active days / player**.
- **D1 / D7 / D30** retention (store open days).
- **Return after first prestige.**
- **Progression wall:** drop-off by `job_index` (expect cliff at 3→4 until fixed).
- **Party impact:** N/A until MaxPlayers &gt; 1; until then track LB tab opens as weak social proxy.

---

## Appendix — Systems map (cited)

| System | Role | Path |
|---|---|---|
| Loop hub | Bootstrap, completion, depart, daily, discovery | `Code/Core/GameCore.cs` |
| Campaign | 25 jobs; L1–3 real, L4–25 stubs | `Code/Cleaning/CampaignCatalog.cs` |
| Job pay/progress | Cells, 99%/100%, multipliers | `Code/Cleaning/JobSiteManager.cs` |
| Upgrades | PSI, tools, van, helper | `Code/Economy/UpgradeSystem.cs` |
| Wallet / LB submit | Cash, run/lifetime | `Code/Economy/PlayerWallet.cs` |
| Prestige | Reset upgrades+job; +25% earn / level | `Code/Progression/PrestigeSystem.cs` |
| Daily | UTC streak; miss resets | `Code/Progression/DailyRewardSystem.cs` |
| Leaderboard | Lifetime earnings board | `Code/Progression/LeaderboardService.cs` |
| Save | Cash, upgrades, secrets, streak | `Code/Persistence/SaveData.cs` |
| Enemies | Resoil, attack, mid-wave rivals | `Code/Enemies/*` |
| Secrets | Under-grime + monologue | `SecretRaster.cs`, `CleanableSurface.cs` |
| HUD | Goals, shop, LB, van, briefings | `Code/UI/Hud.razor` |
| Solo entry | MaxPlayers 1 | `Code/Networking/GameManager.cs` |
| Tuning | Economy, daily, prestige, pests | `Code/Core/GameConstants.cs` |

---

### Engagement summary (skill §4–6)

| Lever | Status |
|---|---|
| Early competence | Strong (L1 boost, no pests) |
| Decisions | Moderate (perfect vs leave; upgrades; tools later) |
| Mastery | Partial (pests); combat/timed unused |
| Curiosity | Strong design, weak supply (few discoveries live) |
| Social | Almost none |
| Habit | Daily streak (obligation risk on miss) |
| Investment | Upgrades + secrets + lifetime $ |
| Reset | Prestige keeps lifetime + tools + secrets; clears upgrades/cash/job |

**Weakest Play Fund multiplier today:** content depth past L3 (kills session depth and return). Second: social. Meta systems are ahead of content.