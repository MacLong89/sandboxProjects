---
name: game-clarity-audit
description: Audits any game for bugs, broken systems, player confusion, UI/UX clutter, information noise, and optimization targets. Use explicitly with /game-clarity-audit and a game folder or description.
disable-model-invocation: true
---

# Game Clarity Audit

Audit the named game as a QA and player-experience specialist. Be direct, evidence-based, and game-agnostic. This is the "what's broken, confusing, cluttered, or unoptimized" pass—separate from retention/engagement design.

## Goal

Find everything that prevents a player from understanding, trusting, and smoothly playing the game: defects, confusing systems, visual and informational noise, friction, and performance problems. Prioritize by how many players hit it and how badly it hurts the experience.

## Inputs

Use the path, game name, description, build, screenshots, or logs supplied after the command. If a repository is available:

1. Inspect the actual code, scenes, UI, HUD, config, input maps, save systems, and relevant documentation.
2. Treat implementation as authoritative when it conflicts with documentation.
3. Trace real control, state, and UI flows rather than assuming intended behavior.
4. Cite concrete files, systems, and screens as evidence.
5. Do not edit the game unless the user separately asks for implementation.

Only ask a question if the target game cannot be identified.

## Audit

### 1. Broken and buggy

Look for:

- Crashes, exceptions, null references, and error spam
- Softlocks, dead ends, unreachable states, and stuck flows
- Broken or unfinished features exposed to players (placeholder buttons, "coming soon", dead links)
- Save/load corruption, progress loss, and desync in multiplayer
- Incorrect math, economy exploits, and progression that stalls or breaks
- Input that does nothing, double-fires, or conflicts
- Physics, collision, navmesh, and spawn failures

Label each: reproducible defect, likely defect, or risk. Note trigger conditions.

### 2. Confusing to players

Identify where a new or returning player cannot tell what to do or what happened:

- Unclear goals, next steps, or win/lose conditions
- Unlabeled or ambiguous buttons, icons, and stats
- Hidden mechanics with no teaching or feedback
- Actions without visible/audible confirmation of success or failure
- Currencies, resources, and numbers whose meaning or use is unclear
- Controls that are undiscoverable or unexplained
- Inconsistent terminology, iconography, or interaction patterns
- Menus and flows that bury the primary action

For each, state the wrong assumption the player is likely to make.

### 3. Clutter and noise

Find what competes for attention and dilutes understanding:

- HUD overload: too many elements, meters, and counters on screen at once
- Everything presented at equal visual weight, so nothing reads as primary
- Information shown before it is relevant or actionable
- Redundant, duplicated, or stale UI
- Excessive popups, toasts, tooltips, modals, and blocking dialogs
- Visual effects, particles, and motion that obscure gameplay
- Text walls where a label, icon, or example would do

Recommend what to hide, defer, merge, shrink, or remove, and what to promote as primary. Apply progressive disclosure: show each thing only when the player needs it.

### 4. Friction and flow

Check the paths players take most:

- Steps, clicks, and load screens between intent and action
- Repeated actions that should be batched, held, or automated
- Backtracking, unnecessary travel, and menu depth
- Missing quality-of-life affordances (confirm, undo, quick-buy, remembered choices)
- First-time setup chores blocking the core activity

### 5. Feedback and readability

- Does every meaningful action produce clear feedback?
- Are state changes (level up, unlock, damage, reward, error) legible and timed well?
- Is text readable at size/contrast, and is the camera/framing showing what matters?
- Are audio cues supporting or fighting comprehension?

### 6. Performance and technical health

- Frame rate, hitches, load times, and memory concerns evident in code or scene setup
- Asset weight, draw calls, tick/update cost, and allocation in hot paths
- Network cost, latency sensitivity, and error rates
- Startup time and time-to-first-interaction

## Severity model

Rate each finding:

- **P0 Blocker:** crash, progress loss, or unplayable state
- **P1 Critical:** most players hit it and it breaks understanding or trust
- **P2 Major:** frequent confusion, friction, or noise that dampens the experience
- **P3 Minor:** polish, edge cases, and small inconsistencies

Also tag: frequency (how many players hit it) and blast radius (how much it hurts when hit).

## Output

Produce:

1. **Verdict:** the top few things hurting comprehension and playability right now.
2. **Findings table:** issue, category (broken / confusing / clutter / friction / feedback / performance), severity, frequency, evidence (file or screen), and player impact.
3. **Broken systems:** defects and risks with reproduction/trigger conditions.
4. **Confusion map:** each confusing element and the wrong assumption it creates.
5. **Clutter and noise cuts:** what to remove, defer, merge, shrink, or promote—with a proposed HUD/menu priority order.
6. **Optimization targets:** performance and flow wins, ranked by cost/benefit.
7. **Prioritized fix list:** ordered by severity × frequency, each with effort estimate and expected result.
8. **Quick wins:** low-effort, high-clarity changes to ship first.
9. **Verification plan:** how to confirm each fix (repro steps, playtest task, or metric).

Separate verified facts, reasoned inferences, and unknowns. Never invent bug reports or analytics; if behavior can only be confirmed at runtime, say so and give the exact test.

Be specific. Avoid generic advice such as "clean up the UI," "fix bugs," or "improve UX" without naming the exact element, screen, or system, the change, and how to verify it.
