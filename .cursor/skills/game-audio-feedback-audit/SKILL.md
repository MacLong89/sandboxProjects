---
name: game-audio-feedback-audit
description: >-
  Audits game audio and juice feedback: SFX/music coverage for core verbs,
  silence on success/fail, music state changes, and missing feedback maps. Use
  explicitly with /game-audio-feedback-audit and a game folder or description.
  Do not modify code unless the user separately requests implementation.
disable-model-invocation: true
---

# Game Audio and Feedback Audit

Audit the named game as an audio / game-feel feedback specialist. Be direct and evidence-based. Pre-ship may note “missing audio”; this pass builds a full **verb → feedback** map.

## Goal

Answer:

> Do core player actions and state changes produce clear, timely SFX/music/VFX feedback—or do success, failure, and danger happen in silence?

**Do not implement fixes during this audit.**

## Inputs

Use the path, game name, description, or builds supplied after the command. If a repository is available:

1. Identify one target project. Only ask if ambiguous.
2. Inspect sound call sites, audio resources, music state machines, UI click sounds, combat/hit feedback, and VFX tied to player verbs.
3. Treat implementation as authoritative over asset folders that exist but are never played.
4. Cite concrete files, events, and asset paths as evidence.
5. Do not edit code unless the user separately asks for implementation.

## Audit

### 1. Core verb feedback map

List the game’s primary verbs (catch, sell, wash, shoot, draft, dive, etc.). For each:

| Verb / event | SFX | Music sting | VFX/juice | UI confirm | Gap? |
|--------------|-----|-------------|-----------|------------|------|
| … | Y/N/partial | … | … | … | … |

Include: success, failure, blocked/invalid, damage/death, reward grant, upgrade purchase, objective complete, round start/end.

### 2. Coverage and silence

- Critical path actions with **no** audio or visual confirm
- Spammy audio on high-frequency actions (needs cooldown/variety)
- Missing UI click/hover on primary menus (if the game otherwise uses UI sounds)
- Placeholder/debug beeps left in shipping paths

### 3. Music and ambience

- States covered: menu, gameplay, danger, victory, defeat, shop, night/day if applicable
- Transitions: abrupt cut vs intentional crossfade; silent gaps on load
- Music that never starts or never stops

### 4. Mix and clarity (code/config level)

- Ducking or priority when many one-shots fire
- 2D UI vs 3D world attenuation mistakes that make feedback inaudible
- Volume defaults that bury SFX under music (if configurable in project)

### 5. Juice beyond audio

- Hit stop, screen shake, flashes, particles on key moments—present, excessive, or absent
- Feedback that lies (success sound on failed action)

## Scoring

Score 1–5 with evidence:

- Core-verb SFX coverage
- Success/fail clarity
- Music state coverage
- UI audio consistency
- Danger/reward stingers
- Overall game-feel juice

## Output

1. **Verdict** — feedback readiness for public play
2. **Verb → feedback map** (table)
3. **Scorecard**
4. **Silence and gap list** — ranked by how often players hit the verb
5. **Spam / mix risks**
6. **Prioritized fixes** — smallest assets/hooks first; impact and effort
7. **Handoffs** — preship (missing assets), clarity (lied feedback), UI (silent modals), retention (weak reward juice)

Separate verified facts, inferences, and unknowns. Do not invent asset quality opinions without hearing/play evidence; code absence is enough to flag a gap.
