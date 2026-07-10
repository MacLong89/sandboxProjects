# Thorns UI — Visual Target

Authoritative UI look target. Reference concept art lives in [Docs/concept/ui/](concept/ui/).

## Dual visual language

| Layer | Style | Reference |
|-------|-------|-----------|
| **HUD** (in-world) | Minimal, semi-transparent, edge-docked | `concept-ui-hud.png` |
| **Menus** (Tab, overlays, main menu) | Parchment interior + dark wood chrome | `concept-ui-inventory.png`, `concept-ui-tames.png`, `concept-ui-guild.png` |

Typography: **default s&box font only** — no custom font files.

## Color tokens (Classic skin)

| Role | Hex | Notes |
|------|-----|-------|
| Parchment center | `#E3D9C6` | Column / body fill |
| Parchment edge stain | `#C4B59A` | Vignette on panel edges |
| Wood frame | `#2B231D` | Outer border, tab rail |
| Wood highlight | `#4A3A2A` | Active tab plank |
| Body text (on parchment) | `#281C10` | Primary labels |
| Body text (on wood) | `#F0EDE5` | Tab labels, footer on dark |
| Slot background | `#2B2622` | Inventory / hotbar tiles (charcoal-brown) |
| Slot border | `#3A322C` | Thin warm edge |
| Accent gold | `#D4AF5A` | Selected hotbar, selected tame card |
| Health | `#CC4444` | HUD bar |
| Thirst | `#5599DD` | HUD bar |
| Hunger | `#DD8833` | HUD bar |
| Stamina | `#55AA44` | HUD bar |
| Durability / progress green | `#4A8A3A` | Slot durability, weight bar |
| Moss accent | `#4A5D23` | Optional corner overlay (v2) |

## Menu shell

```
┌─ wood tab rail ────────────────────────────────────────────────┐
│ ┌─ parchment body ───────────────────────────────────────────┐ │
│ │  col A  │  col B  │  col C                                 │ │
│ │         │         │                                        │ │
│ └─────────┴─────────┴────────────────────────────────────────┘ │
│ footer bar (weight, status)                                      │
└─ outer wood frame ─────────────────────────────────────────────┘
```

- **One** outer wood frame on the overlay — not nested frames per column.
- Columns separated by thin dividers on shared parchment backdrop.
- Section titles: centered caps + diamond rule (not nested wood boxes).

## Screen layouts

### Inventory (`concept-ui-inventory.png`)

- **Left:** ARMOR — head / chest / legs slots + armor / cold / heat resist stats
- **Center:** INVENTORY grid + HOTBAR row below
- **Right:** CRAFTING — filter icons, recipe list, CRAFT button
- **Footer:** weight bar (`23.4 / 50 KG`)

### Tames (`concept-ui-tames.png`)

- **Left:** MY TAMES list (portrait cards, gold selected border, empty + slot)
- **Center:** hero name/species, creature preview, level + XP, stats bars
- **Right:** overview grid, abilities, Heal / Follow / Dismiss actions

### Guild (`concept-ui-guild.png`)

- **Left sidebar:** banner, name, level, XP, influence, members, alliances, activity
- **Main:** VICTORY PATHS — four vertical path cards with progress + milestones
- **Footer:** path advancement + VIEW REWARDS

### HUD (`concept-ui-hud.png`)

- **Top-left:** CURRENT GOAL + diamond rule + objective bullet
- **Bottom-left:** 4 horizontal vitals (icon + bar + value)
- **Bottom-center:** 8 hotbar slots, gold selected border
- **Bottom-right:** circular minimap + time row
- **No parchment or wood on HUD**

## Asset paths

Generated chrome under `Assets/ui/menu/chrome/`:

| File | Use |
|------|-----|
| `parchment_clean.png` | Parchment tile inside framed panels |
| `menu_backdrop.png` | Full menu body fill |
| `menu_backdrop_vignette.png` | Body fill with edge stain |
| `frame_panel_9.png` | Outer / column 9-slice frame |
| `frame_section_9.png` | Section sub-frame |
| `frame_card_9.png` | Card / compact frame |
| `frame_slot_9.png` | Item slot frame |
| `tab_rail.png` | Top tab bar |
| `tab_plank_normal.png` / `tab_plank_selected.png` | Tab states |
| `slot_dark.png` | Dark inventory slot tile |
| `vine_corner_tl.png` | Moss/vine corner (optional) |

## Implementation rules

1. Colors and textures in tokens + SCSS — avoid inline `Style.*` for colors on new work.
2. HUD and menus use different surface helpers (`ThornsHudTheme` vs `ThornsMenuChrome`).
3. World overlays (container, radio, research) reuse menu parchment shell.
4. In-game confirms use `ThornsModal` on parchment shell.
