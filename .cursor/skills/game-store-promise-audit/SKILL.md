---
name: game-store-promise-audit
description: >-
  Audits alignment between store/marketing promise and playable fantasy: title,
  thumbnail, description, trailer claims vs first session reality. Use explicitly
  with /game-store-promise-audit and a game folder or description. Do not modify
  code unless the user separately requests implementation.
disable-model-invocation: true
---

# Game Store Promise Audit

Audit the named game as a product / store-page specialist. Be direct and evidence-based. Retention may touch “promise clarity”; this pass is a full **marketing ↔ gameplay** alignment review.

## Goal

Answer:

> Do the title, thumbnail, description, and other public claims match what a new player can actually do in the first session—without overselling unfinished modes or decorative multiplayer?

**Do not implement fixes during this audit.**

## Inputs

Use the path, game name, store copy, thumbnails, trailer notes, or screenshots supplied after the command. If a repository is available:

1. Identify one target project. Only ask if ambiguous.
2. Inspect project title/ident (`*.sbproj` or equivalent), README, in-game title screens, mode lists, and any packaged store/description assets.
3. Cross-check against the real boot→5-minute play path in code/scenes.
4. Cite concrete files and claim strings as evidence.
5. Do not edit code or store copy unless the user separately asks for implementation.

## Audit

### 1. Promise surfaces

Collect every public-facing claim:

- Title / subtitle
- Thumbnail / key art fantasy (describe what it implies if image present)
- Short and long description
- Tags/genre labels
- In-game main menu copy and mode names
- Trailer or screenshot captions if provided

### 2. First-session reality

From code and content, state what a new player can **actually** do in ~0–5 and ~5–15 minutes. Compare:

| Claim | Supported in first session? | Evidence | Risk if mismatched |
|-------|----------------------------|----------|--------------------|
| … | Yes / Partial / No | file/flow | refund/trust |

### 3. Oversell patterns

Flag:

- Modes in menus marked WIP but still presented as equal choices
- Multiplayer advertised when MP is decorative (cross-check `game-multiplayer-audit`)
- Art style in thumb that the shipped meshes/UI do not deliver
- Feature list items with no player-facing path
- “Coming soon” that has been soon for a long time in UI

### 4. Undersell / clarity

- Strong fantasy buried under a generic title
- Core verb unclear from title+thumb alone
- Cozy vs hardcore tone mismatch that attracts the wrong audience

### 5. Trust repairs

For each mismatch: **change the claim**, **hide the feature**, or **finish the feature**—pick one recommendation. Prefer honest store copy over shipping unfinished fantasy.

## Scoring

Score 1–5 with evidence:

- Title clarity
- Thumbnail ↔ gameplay match
- Description honesty
- Mode list honesty
- First-5-minute promise delivery
- Overall trust / refund-risk (5 = low risk)

## Output

1. **Verdict** — promise aligned / stretched / misleading
2. **Promise inventory** — claims found
3. **Reality summary** — first session capabilities
4. **Mismatch table**
5. **Scorecard**
6. **Prioritized repairs** — rewrite claim vs finish feature vs remove UI entry; effort and impact
7. **Handoffs** — first-10, retention, multiplayer, feature-expansion, UI (menu presentation)

Separate verified facts, inferences, and unknowns. Do not invent Steam review scores or CTR metrics.
