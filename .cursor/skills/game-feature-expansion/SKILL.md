---
name: game-feature-expansion
description: >-
  Discovers and ranks features to finish, connect, or expand in a game: half-wired
  systems, content cliffs, peer gaps, and high-ROI backlog ideas. Use explicitly
  with /game-feature-expansion (or when the user asks for feature ideas, what to
  build next, or expansion backlog). Do not modify code unless the user
  separately requests implementation.
disable-model-invocation: true
---

# Game Feature Expansion

Audit the named game as a design/product strategist focused on **what to build next**. Be evidence-based. Prefer finishing and connecting systems that already exist over inventing greenfield features.

## Goal

Answer:

> What should we implement or expand next, ranked by player impact and wiring cost—and what should we explicitly not chase yet?

This is **ideation + triage**, not ship-readiness. Hand off blockers to `game-preship-completeness-audit`, hours/return hooks to `game-retention-audit`, and structural debt to `game-architecture-audit`.

**Do not implement fixes during this pass.**

## Inputs

Use the path, game name, description, build, or prior audits supplied after the command.

1. Identify one target project. Only ask if ambiguous. Skip tooling/look-dev shells unless asked (`scene_lab`, asset libraries).
2. Inspect code, catalogs, UI, save/progression, networking, README/docs, and existing audits.
3. Treat implementation as authoritative over docs.
4. Cite concrete files and systems as evidence.
5. Do not edit code unless the user separately asks for implementation.

## Full checklist

Read and apply **[CHECKLIST.md](CHECKLIST.md)** for scan categories and peer-gap heuristics.

## Execution order

1. **Pitch + real loop** — one-line fantasy; verb chain players can actually do today (`act → reward → spend → unlock → return`).
2. **Mine existing signals** — prior audits; TODO/FIXME/WIP/PLACEHOLDER/stub; UI with no handler; data written never read; menu modes that are decorative.
3. **Map expandable systems** — economy, combat, crafting, progression, content catalogs, MP/social, modes, UI, persistence.
4. **Peer gap check** — compare archetype peers without cargo-culting unfit features.
5. **Score and rank** — P0–P3 with effort S/M/L.
6. **Report** — structured backlog; prefer a canvas when large.

## Scoring

| Priority | Meaning |
|----------|---------|
| **P0** | Unblocks core loop, softlock, or advertised broken path — finish or hide |
| **P1** | Content cliff after strong early loop, or half-wired system with scaffolding already in code |
| **P2** | Solid loop missing peer-class return hooks or a clear second fantasy beat |
| **P3** | Nice-to-have greenfield with little existing scaffolding |

Per idea require: **evidence path**, **category**, **effort (S/M/L)**, **expected player behavior change**, **success metric**, **do-not** (no dark-pattern padding, decorative MP, “add dailies” with no sink).

## Output

```markdown
# Feature expansion — {game}

## Pitch (1 line)
## Real loop today
## Finish first (P0–P1)
- Idea — evidence — effort — why now — success metric
## Expand next (P2)
## Later / greenfield (P3)
## Explicit non-goals
## Handoffs
- Preship / retention / architecture if relevant
```

Also include a short **system map** (what exists and how complete it feels: wired / half-wired / stub / missing).

Separate verified facts, reasoned inferences, and unknowns. Never invent analytics.
