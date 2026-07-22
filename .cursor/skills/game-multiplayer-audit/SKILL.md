---
name: game-multiplayer-audit
description: >-
  Audits multiplayer and lobby systems for real sync vs decorative MP, authority,
  lobby-to-play flow, reconnect, and host/client edge cases. Use explicitly with
  /game-multiplayer-audit and a game folder or description. Do not modify code
  unless the user separately requests implementation.
disable-model-invocation: true
---

# Game Multiplayer Audit

Audit the named game as a multiplayer systems specialist (including s&box networking patterns when present). Be direct and evidence-based. This asks whether co-op/lobby **actually works**—not only whether exploits exist (`game-robustness-audit`).

## Goal

Answer:

> Is multiplayer real, authoritative, and playable end-to-end—or decorative (lobby/UI without synced gameplay)?

**Do not implement fixes during this audit.**

## Inputs

Use the path, game name, description, or network logs supplied after the command. If a repository is available:

1. Identify one target project. Only ask if ambiguous.
2. Inspect `MaxPlayers` / session config, lobby UI, RPCs/network objects, host authority, ownership, reconnect/disconnect handlers, and any “multiplayer” menu that never syncs state.
3. Treat implementation as authoritative over store copy claiming multiplayer.
4. Cite concrete files, RPCs, components, and flows as evidence.
5. Do not edit code unless the user separately asks for implementation.

If the game is explicitly single-player only, say so, score N/A where appropriate, and flag any leftover MP UI that should be removed or disabled.

## Audit

### 1. Promise vs reality

- What does the project/config/UI claim (MaxPlayers, lobby, co-op, versus)?
- What state is actually replicated to clients?
- **Decorative MP:** lobby created, player list shown, but simulation stays local/host-only with no meaningful shared play

### 2. Session lifecycle

- Boot → lobby/browser → join → load → spawn → play → leave/reconnect
- Host migration or clean fail if host leaves
- Late join: supported, rejected clearly, or broken mid-round
- Disconnect: soft error vs softlock vs desync

### 3. Authority and sync

- Who owns score, inventory, world entities, RNG?
- Client-trusted grants or actions that should be host/server authoritative
- Prediction/reconciliation mistakes visible as rubber-banding or double-apply
- Race conditions on round start/end

### 4. Gameplay fairness and roles

- Role assignment (party/deception games) consistent across peers
- Shared objectives vs per-player private state leakage
- Grief vectors that are design vs bugs

### 5. Solo / practice paths

- Can a player practice without a full lobby when the fantasy needs it?
- Does empty-lobby or 1-player mode break assumptions (`players.Count` divides, etc.)?

## Scoring

| Dimension | Score guidance |
|-----------|----------------|
| Promise alignment | 0–100: claims match playable MP |
| Session reliability | 0–100: lobby→play→leave |
| Authority correctness | 0–100 |
| Sync completeness | 0–100: what matters is replicated |
| Reconnect / host-leave | 0–100 or N/A |
| Overall MP readiness | Shippable MP / Partial / Decorative / SP-only |

## Output

1. **Verdict** — Real MP / Partial / Decorative / Single-player
2. **Session flow diagram** (text or mermaid) of the actual path
3. **Replicated state inventory** — what syncs vs local-only
4. **Scorecard**
5. **Findings** — P0–P3 with evidence and repro when possible
6. **Decorative MP callouts** — hide, finish, or rewrite
7. **Handoffs** — robustness (exploits), store-promise (marketing mismatch), feature-expansion (finish MP), architecture (ownership model)

Separate verified facts, inferences, and unknowns. Do not claim packet-level behavior without code or log evidence.
