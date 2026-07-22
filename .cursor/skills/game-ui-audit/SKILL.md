---
name: game-ui-audit
description: >-
  Audits game HUD and UI for readability, contrast, layout, modal stacking,
  spacing, anti-generic styling, and popup/onboarding hide paths. Use explicitly
  with /game-ui-audit and a game folder or description. Do not modify code
  unless the user separately requests implementation.
disable-model-invocation: true
---

# Game UI Audit

Audit the named game as a UI/UX layout and readability specialist. Be direct, evidence-based, and game-agnostic. This is the visual/layout pass—separate from clarity (bugs/confusion) and first-10 (cold open pedagogy).

## Goal

Answer:

> Can a player read, parse, and dismiss every important UI surface without overlap, overflow, low contrast, or generic “AI dashboard” styling—and can they hide teaching/notification noise when they want?

**Do not implement fixes during this audit.**

## Inputs

Use the path, game name, description, build, or screenshots supplied after the command. If a repository is available:

1. Identify one target project. Only ask if ambiguous.
2. Inspect HUD entry points (`Code/UI/**`, Razor + SCSS, PanelComponent trees), overlay/modal classes, z-index / layer enums, tip/toast systems, and input bindings for tip hide/toggle.
3. Treat implementation as authoritative over docs or mockups.
4. Cite concrete files, classes, selectors, and screens as evidence.
5. Do not edit code unless the user separately asks for implementation.

## Full checklist

Read and apply **[CHECKLIST.md](CHECKLIST.md)** — core visual rules plus HUD-band, stacking, pointer-events, and onboarding-hide checks. Do not skip sections.

## Execution order

1. **Map UI surfaces** — root HUD, topbar/dock, toasts, tips, confirm modals, takeovers (shop/welcome/death), world-space labels.
2. **Layer / z-order pass** — tip vs overlay vs takeover; exclusive bands; single scrim.
3. **Readability pass** — contrast, overflow, truncate/wrap, spacing buffers, icon/text collisions.
4. **Interaction pass** — pointer-events, click theft, input blocking, Escape/H, destructive confirms.
5. **Style pass** — anti-generic / anti-AI look; respect the game’s existing tokens when present.
6. **Hide path** — tips, notifications, onboarding: persistent hide + restore (H or equivalent).
7. **Report** — findings + per-screen pass/fail.

For s&box games that use Final Outpost–style tips, cross-check Hide tips / Got it / H against the project’s `sbox-onboarding` skill when available (workspace or personal).

## Severity

| Tag | Meaning |
|-----|---------|
| **Critical** | Unreadable, blocking overlap, broken dismiss, or UI that softlocks/misleads core actions |
| **Should-fix** | Clear contrast/spacing/stacking issues players will hit often |
| **Nit** | Polish, minor buffer, stylistic consistency |

## Output

Produce:

1. **Verdict** — one paragraph: UI readiness for public play.
2. **Surface inventory** — major screens/overlays found (with file paths).
3. **Scorecard** — Pass / Partial / Fail for: Contrast, Stacking, Overflow, Spacing, Style, Hide-popups, Interaction.
4. **Findings** — Critical → Should-fix → Nit; each with evidence path, repro/condition, and fix hint.
5. **Per-screen checklist** — short Pass/Fail table for each major surface.
6. **Handoffs** — clarity (confusion), first-10 (teaching), onboarding skill (tip semantics), preship (missing UI assets).

Prefer a canvas when findings are large. Separate verified facts, inferences, and unknowns. Do not invent screenshot evidence.
