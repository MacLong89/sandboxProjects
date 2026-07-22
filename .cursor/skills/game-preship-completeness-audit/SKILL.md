---
name: game-preship-completeness-audit
description: >-
  Pre-release completeness and finishing audit: half-implemented features,
  placeholders, missing assets/audio/UI/feedback, disconnected systems, core
  loop gaps, and ship blockers. Use explicitly with
  /game-preship-completeness-audit (or when the user asks for a pre-ship /
  completeness / finishing audit). Do not modify code unless the user
  separately requests implementation.
disable-model-invocation: true
---

# Pre-Ship Completeness and Finishing Audit

Audit whether the named game is actually finished and shippable—not whether systems merely exist. Be evidence-based. **Do not implement fixes during this audit.**

## Goal

Answer one practical question:

> What exactly must be completed, connected, replaced, clarified, or removed before this game is safe to release to real players?

A feature is complete only if it is fully connected, functional, understandable, purposeful, polished enough for release, and handles expected gameplay conditions.

## Inputs

Use the path, game name, description, build, screenshots, or logs supplied after the command.

1. Identify one target project. Only ask if ambiguous.
2. Inspect the entire project: code, scenes, UI, assets, audio, config, save, networking.
3. Treat implementation as authoritative over docs or comments.
4. Cite exact files, classes, methods, scenes, prefabs, and resources.
5. Do not edit code unless the user separately asks for implementation.

## Full audit specification

Read and follow **[AUDIT_PROMPT.md](AUDIT_PROMPT.md) verbatim** — it is the complete checklist (sections 1–23) and required final report format (A–P). Do not skip sections. Do not paraphrase the completion criteria away.

## Execution order

1. **Reconnaissance** — map folders, managers, modes, UI, save, network, content.
2. **Keyword + behavioral scan** — TODO/FIXME/HACK/TEMP/PLACEHOLDER/WIP plus unlabeled stubs (empty methods, unconditional returns, debug-only paths, dead buttons).
3. **Chain every feature** — begin → discover → interact → feedback → state → success/fail → end → reward → loop link → reset → multiplayer → repeat → death/reconnect.
4. **Core loop** — describe what players can actually do today, then list breaks.
5. **Assets / audio / animation / UI / world / interactables** — inventory and gaps.
6. **Severity** — classify P0–P3 honestly; do not demote ship blockers.
7. **Report** — produce sections **A through P** exactly as specified in AUDIT_PROMPT.md.

## Hard rules (from the prompt)

- Do not fix code during this audit.
- Do not mark a feature complete solely because a class, method, UI, asset reference, or compile exists.
- Do not treat console output or debug controls as player feedback / real gameplay.
- Do not assume multiplayer or round reset works because the first single-player path works.
- Clearly distinguish confirmed issues from suspected risks; include repro steps when possible.
- Prefer completing/connecting core blockers over recommending optional content.

## Presentation

- Prefer a canvas for the executive summary + feature inventory + ship blockers when the report is large; still keep full A–P findings available in chat or a linked artifact.
- For multiple games: one game unless the user named several.
