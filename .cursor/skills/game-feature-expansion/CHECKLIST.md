# Feature Expansion Checklist

Scan every category. Prefer **finish/connect** over invent.

## 1. Pitch and loop

- [ ] One-sentence player fantasy (from README, docs, store copy, onboarding, or HUD brand)
- [ ] Real verb chain today: act → reward → spend → unlock → return
- [ ] Where the loop stalls, cliffs, or becomes chore-only

## 2. Existing signal mines

- [ ] Prior audits under `.cursor/audits/`, `*AUDIT*.md`, architecture docs
- [ ] TODO / FIXME / HACK / TEMP / PLACEHOLDER / WIP / stub
- [ ] Empty methods, unconditional returns, debug-only paths
- [ ] UI buttons/menus with no handler or “coming soon”
- [ ] Fields/catalogs written but never read in gameplay
- [ ] Modes shown in menu but incomplete or gated forever

## 3. Gap categories

- [ ] **Stub / content cliff** — campaign levels, biomes, modes as data/story only
- [ ] **Dead contracts** — timers, combat flags, tools declared but unread
- [ ] **Half-wired systems** — UI/data/save already present (highest ROI)
- [ ] **Advertised unfinished modes** — visible WIP that undercuts trust
- [ ] **Decorative multiplayer** — lobby created, no synced gameplay
- [ ] **Return-hook asymmetry** — missing daily/prestige/offline/social vs peers *only if* core loop is solid
- [ ] **Second fantasy / vertical content** — deepens the proven pitch
- [ ] **Mode/shell generalization** — multi-mode games still bound to one mode’s stats/UI
- [ ] **Onboarding gaps** — missing goal-gated tips / Hide tips / H toggle when peers have them
- [ ] **Presentation placeholders** — icons, weapons, vitals that undercut the store promise
- [ ] **Session vs meta save** — meta persists but mid-run state is lost unexpectedly

## 4. Peer gap heuristics (examples, not mandates)

| Archetype | Peer signals in a multi-game repo |
|-----------|-----------------------------------|
| Tycoon / collection sim | Dailies, prestige/rebirth, offline or sanctuary progress, social buff |
| Cozy incremental | Clear depth ladder, gear sinks, biome unlock cadence |
| Party / deception MP | Role clarity, lobby→round integrity, practice/solo path |
| Survival / base | Crafting→raid→rebuild loop, wildlife/AI pressure, loot sinks |
| Management / GM | Season/week structure, meaningful decisions, sim feedback |

Do not recommend a peer feature that fights the game’s fantasy or adds obligation without play.

## 5. Per-idea quality bar

Each backlog item must state:

- Evidence (file/system)
- Category (from §3)
- Effort S / M / L
- Player behavior change
- Success metric
- Do-not / anti-pattern to avoid

## 6. Explicit non-goals

List ideas that are tempting but should wait (no scaffolding, vanity MP, retention padding, content volume without a decision).
