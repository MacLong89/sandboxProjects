---
name: game-economy-audit
description: >-
  Audits game economies for currencies, sources/sinks, prices, prestige wipe
  rules, upgrade ROI, inflation, softlocks, and AFK/offline abuse. Use
  explicitly with /game-economy-audit and a game folder or description. Do not
  modify code unless the user separately requests implementation.
disable-model-invocation: true
---

# Game Economy Audit

Audit the named game as an economy / progression balance specialist. Be direct, evidence-based, and game-agnostic. This deep-dives currencies and sinks—broader than a passing note in clarity or retention.

## Goal

Answer:

> Are sources, sinks, prices, and prestige rules coherent so players stay motivated without softlocking, infinite inflation, or meaningless currencies?

**Do not implement fixes during this audit.**

## Inputs

Use the path, game name, description, balance tables, or builds supplied after the command. If a repository is available:

1. Identify one target project. Only ask if ambiguous.
2. Inspect wallet/currency types, catalogs, shops, upgrade trees, prestige/rebirth, drop tables, timers, offline/AFK earners, and save fields that store balances.
3. Treat implementation as authoritative over design docs.
4. Cite concrete files, constants, formulas, and UI price displays as evidence.
5. Do not edit code unless the user separately asks for implementation.

## Audit

### 1. Currency inventory

For each currency/resource:

- How it is earned (sources) and spent (sinks)
- Whether it is shown in HUD/shop
- Softcap, hardcap, or overflow behavior
- Whether it can go negative or NaN
- Whether it is wiped on prestige/death/season

Flag **dead currencies** (earned or displayed with no meaningful sink) and **hidden currencies** (matter but are never taught).

### 2. Source / sink balance

- Early-, mid-, and late-game earn rates (from code constants or formulas—do not invent live telemetry)
- Whether sinks scale with wealth (upgrades, crafts, fees, gambles, cosmetics)
- Bottlenecks that gate the core loop vs intentional difficulty
- Duplicate earn paths that trivialize sinks
- Convert/exchange rates between currencies

### 3. Upgrade and price ROI

- First upgrades: affordable soon after first reward?
- Mid-game: diminishing returns vs still-exciting spikes
- Softlocks: required purchase costs more than max reachable balance without exploits
- Misleading UI prices (display ≠ charged amount)
- Prestige/rebirth: retained power vs full wipe; is the reset rewarding?

### 4. Exploits and abuse

- Infinite loops (buy/sell, refund, duplicate grant)
- AFK / offline earn that outpaces active play without a sink
- Multiplayer dupes or host-authoritative grant mistakes (hand off deep MP to `game-multiplayer-audit` / robustness)
- Timer skip, stack overflow, int wrap

### 5. Progression feel

- Time-to-first meaningful spend
- Clarity of “what to save for next”
- Parallel currencies fighting for attention
- Content gated only by grind with no decision

## Scoring

Score 1–5 with evidence:

- Currency clarity
- Source/sink health
- Early upgrade ROI
- Mid/late scaling
- Prestige/reset quality
- Exploit resistance
- Softlock risk (invert: 5 = low risk)

## Output

1. **Verdict** — economy healthy / fragile / broken for public play
2. **Currency map** — table of currencies → sources → sinks → wipe rules
3. **Scorecard** (dimensions above)
4. **Critical imbalances** — ranked; each with formula/file evidence and player impact
5. **Exploits and softlock risks**
6. **Prioritized fixes** — impact, effort, expected behavior change, success metric
7. **Handoffs** — retention (return hooks), robustness (dupes), feature-expansion (new sinks), UI (price readability)

Separate verified facts, reasoned inferences, and unknowns. Never invent player analytics or claimed earn rates without code evidence.
