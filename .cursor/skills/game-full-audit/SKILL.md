---
name: game-full-audit
description: >-
  Runs all thirteen game audit skills in one pass: clarity, first-10, retention,
  architecture, performance, robustness, pre-ship completeness, UI, economy,
  multiplayer, audio/feedback, store promise, and feature expansion. Use
  explicitly with /game-full-audit and a game folder or description. Do not
  modify code unless the user separately requests implementation.
disable-model-invocation: true
---

# Game Full Audit

Run a complete production readiness and next-features review by executing **all thirteen** sibling game audit skills against the same target. Do not modify code unless explicitly requested.

## Child skills (required)

Read each skill file fully, then follow it for the named target. Paths are relative to this skill's parent (`~/.cursor/skills/`):

1. [game-clarity-audit](../game-clarity-audit/SKILL.md) — bugs, confusion, clutter, friction, feedback, performance-as-UX
2. [game-first10-audit](../game-first10-audit/SKILL.md) — cold open, onboarding, time-to-fun
3. [game-retention-audit](../game-retention-audit/SKILL.md) — loop, return hooks, player-hours
4. [game-architecture-audit](../game-architecture-audit/SKILL.md) — structure, coupling, debt, scalability
5. [game-performance-audit](../game-performance-audit/SKILL.md) — CPU/memory/allocs/network/render/scale
6. [game-robustness-audit](../game-robustness-audit/SKILL.md) — logic flaws, exploits, MP/save edge cases
7. [game-preship-completeness-audit](../game-preship-completeness-audit/SKILL.md) — half-finished features, placeholders, missing assets/audio/UI, disconnected systems, ship blockers (follow its [AUDIT_PROMPT.md](../game-preship-completeness-audit/AUDIT_PROMPT.md) verbatim)
8. [game-ui-audit](../game-ui-audit/SKILL.md) — contrast, stacking, overflow, spacing, anti-generic style, hide popups (follow its [CHECKLIST.md](../game-ui-audit/CHECKLIST.md))
9. [game-economy-audit](../game-economy-audit/SKILL.md) — currencies, sources/sinks, prices, prestige, softlocks
10. [game-multiplayer-audit](../game-multiplayer-audit/SKILL.md) — real sync vs decorative MP, authority, lobby→play, reconnect
11. [game-audio-feedback-audit](../game-audio-feedback-audit/SKILL.md) — verb→SFX/music/juice map, silence gaps
12. [game-store-promise-audit](../game-store-promise-audit/SKILL.md) — title/thumb/description vs playable fantasy
13. [game-feature-expansion](../game-feature-expansion/SKILL.md) — ranked finish/expand backlog (follow its [CHECKLIST.md](../game-feature-expansion/CHECKLIST.md)); run last

If a relative path fails, resolve via absolute paths under the user's `~/.cursor/skills/` directory (or `%USERPROFILE%\.cursor\skills\` on Windows).

## Inputs

Same as the child skills: path, game name, description, build, screenshots, or logs after the command.

1. Identify one target project. Only ask if ambiguous.
2. Read the ENTIRE relevant codebase once (shared pass), then apply each audit lens.
3. Treat implementation as authoritative over docs.
4. Cite concrete files/systems as evidence.
5. Do not edit code unless the user separately asks for implementation.

## Execution

1. **Shared reconnaissance** — map folders, managers, UI/HUD layers, save, network/lobby, progression/currencies, hot loops, content systems, assets, audio hooks, and store/promise surfaces so you are not re-discovering the project thirteen times.
2. **Run all thirteen audits** — each must produce its full required output sections from its own SKILL.md (and AUDIT_PROMPT.md / CHECKLIST.md where linked). Prefer parallel subagents when the target is large, with one agent (or batch) per skill; each agent must read its skill file and return that skill's full output. Run **feature-expansion last** (or after ship lenses complete) so it can reuse findings.
3. **Do not skip a skill** because another overlaps (e.g. clarity vs UI vs performance vs pre-ship). Overlap is fine; keep each section in its home audit.
4. **Cross-link** — after all thirteen, synthesize conflicts and shared root causes (one bug that is P0 clarity, P0 robustness, P0 pre-ship, and a retention leak counts in all relevant audits, then once in the mega priority list).

## Mega output

Produce this structure, in order:

### A. Mega Executive Summary
One short verdict: ship readiness, top 3 blockers, top 3 strengths, and the top finish-or-expand opportunity. State that this combines all thirteen audits.

### B. Scorecard
| Audit | Score | One-line verdict |
|-------|------:|------------------|
| Clarity | (use that skill's severity model / top verdict) | |
| First 10 | Yes / Partial / No (+ why) | |
| Retention | (use scorecard avg or hours potential 1–5, plus verdict) | |
| Architecture | 0–100 | |
| Performance | 0–100 | |
| Robustness | 0–100 | |
| Pre-ship completeness | overall % + shippable Y/N + P0/P1 counts | |
| UI | Pass / Partial / Fail (+ weakest scorecard row) | |
| Economy | (avg 1–5 or verdict) | |
| Multiplayer | Real / Partial / Decorative / SP-only (+ readiness) | |
| Audio / feedback | (avg 1–5 or verdict) | |
| Store promise | (avg 1–5 or aligned/stretched/misleading) | |
| Feature expansion | top P0–P1 count + one-line “build next” | |

If a child skill does not define a 0–100 score, convert its verdict faithfully (do not invent false precision).

### C. Unified Priority List
Ordered by severity × player/ship impact. Tag each item with source skills (e.g. `clarity+ui+preship+retention`). Separate P0 / P1 / P2.

From **feature-expansion**, promote only **P0/P1** items into this mega list. Leave P2/P3 in the child report (and section E “Next features”).

### D. Full child reports
Paste or nest the complete outputs for:

1. Clarity Audit  
2. First 10 Audit  
3. Retention Audit  
4. Architecture Audit  
5. Performance Audit  
6. Robustness Audit  
7. Pre-Ship Completeness Audit (sections A–P from AUDIT_PROMPT.md)  
8. UI Audit  
9. Economy Audit  
10. Multiplayer Audit  
11. Audio / Feedback Audit  
12. Store Promise Audit  
13. Feature Expansion  

Keep each child's required section headings intact so nothing is lost.

### E. Cross-cutting roadmap
Single ordered plan:

1. Gameplay correctness / softlocks  
2. Activation (first 10)  
3. Retention leaks  
4. Presentation / UI / audio / store honesty  
5. Completeness (finish or hide)  
6. Architecture / performance debt  
7. **Next features** — feature-expansion P0/P1 only, then note P2+ as later  

Note effort and expected result. Call out what needs a profiler or playtest vs verified from code.

## Presentation

- For a single game: prefer a canvas when the combined report is large (scorecard + priority table + per-audit collapsibles); still keep the full structured findings available.
- For multiple games: ask whether to run sequentially per game; default to one game unless the user named several or said "every project."
- Separate verified facts, reasoned inferences, and unknowns. Never invent analytics or timings.

## Anti-patterns

- Do not run only the "interesting" audits.
- Do not merge all thirteen into a vague essay that drops required child sections.
- Do not start implementing fixes under this skill.
- Do not re-audit from memory without reading the child SKILL.md files in this session.
- Do not skip pre-ship completeness because other audits already mentioned polish — it has its own required A–P report format.
- Do not dump the entire feature-expansion P2/P3 backlog into the mega priority list — ship blockers and P0/P1 finishes first.
