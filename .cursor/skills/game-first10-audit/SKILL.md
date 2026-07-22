---
name: game-first10-audit
description: Audits the first ten minutes of any game—cold-open experience, onboarding, tutorial need, and whether the opening should be simpler. Use explicitly with /game-first10-audit and a game folder or description.
disable-model-invocation: true
---

# Game First 10 Minutes Audit

Audit only the opening ten minutes of the named game, as experienced by a brand-new player with zero prior context. Be direct, evidence-based, and game-agnostic. This is the "cold open" pass: what actually happens, whether it teaches, whether it needs a tutorial, and whether it should start simpler.

## Goal

Determine whether a first-time player reaches understanding and their first real moment of fun inside ten minutes—and prescribe the smallest changes that get them there faster. The opening decides whether players stay at all, so treat every second before the first fun as expensive.

## Inputs

Use the path, game name, description, build, or screenshots supplied after the command. If a repository is available:

1. Trace the real boot-to-play path: launch, menus, mode/save selection, loading, spawn, first controllable moment, first objective, first reward.
2. Inspect scenes, HUD, first-run flags, tutorial/onboarding logic, input maps, and default state.
3. Treat implementation as authoritative when it conflicts with documentation.
4. Cite concrete files, screens, and systems as evidence.
5. Do not edit the game unless the user separately asks for implementation.

Only ask a question if the target game cannot be identified.

## Audit

### 1. Cold-open walkthrough

Narrate the literal first-time experience as discrete beats with rough timing:

- **Launch → menu:** What is shown before the player can do anything? Mode pickers, save slots, settings, lore, updates?
- **Load → spawn:** How long until the player controls something? What is on screen at spawn?
- **0–90 seconds:** Can the player perform the core fantasy verb immediately, with no chores? What do they most likely try, and does it work?
- **90 sec–3 min:** Do they understand the goal and the loop? Do they get a first reward or visible result?
- **3–6 min:** Does a first upgrade, unlock, or new capability change what they can do?
- **6–10 min:** Is there a clear next goal and a reason the session is worth continuing?

Mark the exact moment (or absence) of: first control, first understood goal, first reward, first fun, first meaningful choice.

### 2. Onboarding present today

Identify what onboarding actually exists:

- Explicit teaching: tutorials, coach marks, prompts, tooltips, guided steps
- Implicit teaching: level/scene design, constraints, affordances, discoverable controls
- First-run defaults: prebuilt state, starter gear, safe sandbox vs full complexity
- Feedback that confirms the player learned something

State what is taught, when, how, and whether it is skippable or forced.

### 3. Friction and confusion in the opening

Flag anything delaying or blocking the first fun:

- Menus, modals, and setup chores before the verb
- Empty hubs, unclear spawn, "where do I go / what do I do"
- Unlabeled controls, currencies, or objectives
- Complexity or systems dumped before they are usable
- Blocking tutorial text walls; unskippable slow intros
- Silent failures and softlocks in the tutorial chain
- Broken or placeholder elements a new player hits immediately

For each, name the wrong assumption the new player likely makes.

### 4. Tutorial need decision

Decide explicitly which path fits this game, with reasoning tied to complexity, control scheme, and how self-evident the loop is:

- **Needs a tutorial:** the loop or controls are not self-evident; specify the minimum viable teaching (what to teach, in what order, when, and how much can be learned by doing vs told).
- **Needs a simpler start, not a tutorial:** the game is over-front-loaded; specify what to hide, defer, prebuild, or remove so the verb teaches itself.
- **Opening is fine:** justify why, and name the one thing to protect.

Prefer teaching by doing over text. Prefer a simpler opening over a longer tutorial when both would work. Progressive disclosure: introduce one system at a time, only when it becomes usable.

### 5. Simplification pass

Propose the leanest possible opening that still conveys the fantasy:

- What can be cut, delayed, or auto-completed for the first session
- What should be prebuilt or granted so the player acts immediately
- Which single verb the first 90 seconds should sell
- What to promote as the one clear next goal

## Output

Produce:

1. **Verdict:** Does a new player reach understanding and first fun within ten minutes? Yes/No and why.
2. **Minute-by-minute beats:** launch, spawn, 90 sec, 3 min, 6 min, 10 min—what the player experiences at each.
3. **Time-to markers:** time (or step count) to first control, first understood goal, first reward, first fun, first choice.
4. **Onboarding inventory:** what teaching exists today and its quality.
5. **Opening friction list:** ranked blockers and confusions, each with the player's wrong assumption and evidence (file/screen).
6. **Tutorial decision:** tutorial / simpler start / fine—with the recommended minimum design.
7. **Recommended opening:** the leanest first 10 minutes, as an ordered beat sheet.
8. **Quick wins:** low-effort changes that shorten time-to-fun immediately.
9. **Verification plan:** playtest task and metrics (e.g., % reaching first fun by minute 10, tutorial step drop-off, first-session abandonment point).

Separate verified facts, reasoned inferences, and unknowns. Never invent analytics; if behavior can only be confirmed at runtime, state that and give the exact playtest.

Be specific. Avoid generic advice such as "add a tutorial" or "simplify the UI" without naming the exact beat, what is taught or cut, when, and how to verify it.
