# Game UI Audit Checklist

Use every section. Cite files/selectors for fails.

## 1. Contrast and readability

- [ ] Text contrasts with its background (light on dark and dark on light where used)
- [ ] Muted/secondary text still readable on glass/scrim panels
- [ ] Accent colors used for emphasis do not reduce body-text legibility
- [ ] Icons remain visible against panel and world backgrounds
- [ ] Busy world/combat under HUD does not make critical text unreadable (move to reserved bands if needed)

## 2. Modal and overlay stacking

- [ ] Modals do not incorrectly cover each other (wrong z-order / same layer fight)
- [ ] Tip overlays do not appear over shop/welcome/mission/death takeovers
- [ ] Only one full-screen dim/scrim at a time for a given takeover
- [ ] Critical confirms sit above normal dialogs
- [ ] Closing one overlay does not leave a stuck invisible blocker

## 3. Overflow and containment

- [ ] Text stays inside containers (no clip through edges, no runaway width)
- [ ] Long titles/numbers truncate or wrap intentionally (`.truncate`, max-width, etc.)
- [ ] Modal bodies scroll when content exceeds height; primary actions remain reachable
- [ ] World-space labels readable at typical camera distance (if present)

## 4. Overlap and spacing

- [ ] Text and icons do not overlap each other
- [ ] Reasonable buffers between adjacent text objects and icons
- [ ] Exclusive HUD bands: tips, toasts, objective chips, kill-feed do not share the same vertical/corner slot
- [ ] Safe margins from screen edges / letterboxing

## 5. Interaction and hit targets

- [ ] Root HUD uses `pointer-events: none` (or equivalent) so world input is not blocked
- [ ] Interactive chrome re-enables pointer events only where intended
- [ ] Nested icon/text children do not steal clicks from parent buttons
- [ ] Tip/modal blocks gameplay input when that is the game’s design
- [ ] Escape / back dismisses the topmost dismissible surface consistently
- [ ] Destructive actions (reset/wipe/sell-all) require confirmation

## 6. Hide popups / onboarding / notifications

- [ ] Player can hide tips/onboarding (persistent), not only soft-dismiss one tip
- [ ] Restore path exists (e.g. H key / settings) and is communicated (toast or settings copy)
- [ ] Notification/toast spam can be reduced or does not permanently obscure HUD
- [ ] Got it (or equivalent) soft-dismisses current tip only; does not dump the next tip immediately
- [ ] Button copy for tip systems stays consistent when the project uses the shared onboarding pattern (**Hide tips** / **Got it**)

## 7. HUD truthfulness and early clutter

- [ ] Hints and button labels match real controls/state
- [ ] Secondary systems (prestige, leaderboards, huge currencies) do not dominate the first minutes
- [ ] FPS/cursor-lock modes do not leave stuck overlays or pointer-events

## 8. Anti–“AI created” look

Flag generic AI-default styling unless it is already the game’s established identity:

- [ ] No purple-to-indigo gradient themes as default chrome
- [ ] No warm cream + terracotta serif “AI brochure” look forced onto a different art direction
- [ ] No glow stacks, pill clusters, emoji chrome, or multi-layer shadow noise as primary HUD language
- [ ] No SaaS dashboard clutter (stat strips, badge piles, card grids) in the core play HUD
- [ ] Prefer the game’s existing tokens (fonts, glass, accents, icon set) for consistency
- [ ] One clear visual hierarchy per surface; one job per panel/section

## Severity quick guide

- **Critical:** cannot read core info, modal trap, overlap blocking actions, no way out of tip spam that blocks play
- **Should-fix:** frequent low contrast, band collisions, overflow on common strings, weak hide path
- **Nit:** uneven padding, minor style inconsistency, rare long-string wrap issues
