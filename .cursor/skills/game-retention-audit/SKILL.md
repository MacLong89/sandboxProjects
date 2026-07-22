---
name: game-retention-audit
description: Audits any game for onboarding, engagement, replayability, return behavior, and sustainable player-hours. Use explicitly with /game-retention-audit and a game folder or description.
disable-model-invocation: true
---

# Game Retention Audit

Audit the named game as a retention and engagement specialist. Be direct, evidence-based, and game-agnostic.

## Goal

Find changes that maximize engaged, returning player-hours—not AFK padding, manipulative dark patterns, or raw content volume.

If this is an s&box Play Fund game, account for the fund being based roughly on clamped individual player-hours: prioritize unique engaged players, return days, and meaningful session depth.

## Inputs

Use the path, game name, design description, build, screenshots, or metrics supplied after the command. If a repository is available:

1. Inspect the actual code, scenes, UI, configuration, save systems, and relevant documentation.
2. Infer the pitch and game archetype.
3. Treat implementation as authoritative when it conflicts with documentation.
4. Cite concrete files and systems as evidence.
5. Do not edit the game unless the user separately asks for implementation.

Only ask a question if the target game cannot be identified.

## Audit

### 1. Product promise

- State the one-sentence player fantasy.
- Identify the target player and game archetype.
- Judge whether the title, thumbnail, description, and opening deliver the same promise.

### 2. First-session onboarding

Walk through the literal new-player experience:

- **0–90 seconds:** Can the player perform the core fantasy verb immediately?
- **2–5 minutes:** Do they understand the loop and receive a meaningful reward?
- **5–10 minutes:** Does the first upgrade visibly or mechanically increase power?
- **10–20 minutes:** Is there a clear next goal, new capability, or compelling choice?
- **End of session:** Does the game create an unfinished goal worth returning for?

Flag welcome walls, mode-selection friction, setup chores, empty hubs, unclear controls, premature complexity, silent softlocks, dead time, and currencies with no obvious use.

### 3. Core loop

- Express it as a verb chain: `act → reward → spend → unlock → repeat`.
- Estimate the duration of each loop.
- Evaluate clarity, agency, challenge, feedback, audiovisual juice, pacing, and goal visibility.
- Identify where the loop stalls or becomes repetitive without a new decision or payoff.

### 4. Engagement

Evaluate:

- Immediate competence and early wins
- Meaningful decisions and player agency
- Skill mastery and challenge progression
- Curiosity, discovery, surprise, and variable rewards
- Social presence, cooperation, competition, and comparison
- "One more goal" pacing

Do not recommend chance mechanics or friction merely to exploit players. Distinguish satisfying anticipation from harmful compulsion.

### 5. Replayability and long-term investment

Check for:

- Persistent progress and player identity
- Builds, loadouts, skill trees, jobs, or playstyle choices
- Collections, achievements, secrets, and mastery goals
- Prestige/rebirth that retains meaningful power
- Alternate modes, maps, procedural variation, or emergent play
- Leaderboards, seasons, events, and community goals
- Sufficient content depth without overwhelming the first session

### 6. Return systems

Categorize existing and missing hooks:

- **Habit:** dailies, streak alternatives, scheduled events, update cadence
- **Investment:** collections, upgrades, persistent builds, offline progress
- **Social:** friends, shared lobbies, guilds, co-op, rivalry, leaderboards
- **Reset:** prestige, seasons, new worlds, soft resets with retained value

Judge whether each hook adds play or only obligation.

### 7. Player-hours model

Assess separately:

- **Acquisition:** click appeal and clarity of promise
- **Activation:** reaching the first fun/reward/upgrade
- **Session depth:** reasons to continue this session
- **Return rate:** reasons to return tomorrow and next week
- **Social multiplier:** reasons to invite or follow friends
- **Content/update leverage:** how cheaply the game can generate fresh goals
- **Technical loss:** loading, errors, performance, save failures, and confusing UI

Identify the weakest multiplier. Do not assume longer sessions alone produce better retention.

## Scoring

Score each dimension from 1–5 and justify it with evidence:

- Promise clarity
- First 90 seconds
- First 10 minutes
- Core-loop strength
- Feedback and game feel
- Goal and upgrade pacing
- Player agency/mastery
- Replayability
- Return systems
- Social retention
- Sustainable content depth
- Player-hours potential

## Output

Produce:

1. **Verdict:** Is this currently an effective hour engine? Why?
2. **Inferred pitch and archetype**
3. **Minute-by-minute first session:** 90 seconds, 5, 10, and 20 minutes
4. **Core-loop diagnosis**
5. **Scorecard**
6. **Three strongest retention mechanisms**
7. **Five largest retention leaks**, ranked by severity and supported by file/system evidence
8. **Prioritized fixes:** impact, effort, expected player behavior change, and success metric
9. **30-day experiment plan:** smallest testable changes first
10. **Instrumentation plan:** events and funnels needed to verify the conclusions

Use these minimum metrics where applicable:

- Start → core verb completion
- Time to first fun/reward/upgrade
- Tutorial completion and step-by-step abandonment
- Session length distribution, not only average
- D1, D7, and D30 retention
- Sessions per player and active days per player
- Return after first prestige/reset
- Progression wall abandonment
- Party/friend impact on session length and retention

Separate verified facts, reasoned inferences, and unknowns. Never invent analytics. If no telemetry exists, state that behavioral claims are hypotheses and define how to test them.

Be specific. Avoid generic advice such as “add more content,” “improve polish,” or “add dailies” without explaining the exact mechanic, placement, expected behavior change, and measurement.
