# OFFSHORE

A cozy 2D side-view fishing game for s&box.

## Play

1. Open `offshore.sbproj` in the s&box editor.
2. Open `scenes/offshore.scene` and press Play.

## Look-dev (one system at a time)

Work in isolated scenes so the main game stays untouched:

| Scene | Focus |
|-------|--------|
| `scenes/sky_lab.scene` | Sky, day/night, sun/moon, stars, clouds |

**Sky lab controls:** A/D scrub time · Space pause · 1–4 jump dawn/noon/sunset/night · 5/6 speed

Sky art refresh: `python tools/install_sky.py`

## Regenerate art (hero PNGs → game textures)

1. Drop / regenerate AI plates in `Assets/Art/heroes/` (`hero_shop.png`, `hero_player.png`, boats, etc.)
2. Install real transparent sprites:

```bash
python tools/install_heroes.py
```

Do **not** run `paint_stardew_kit.py` or `generate_assets.py` for the shipping look — those overwrite heroes with blocky placeholders.

## Controls

- A/D — walk / steer
- E — interact (shop, board, dock, hook)
- F — dock fish / retrieve / refuel
- LMB — cast (hold for power)
- R / RMB — reel
- Tab — fish log
- M — objectives
- Esc — pause / close
