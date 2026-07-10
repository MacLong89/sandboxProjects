# Thorns UI Icons

Place **inventory item** PNGs here (`Assets/ui/icons/`). **Each PNG creates an in-game item** on load (filename = item id).

**Naming:** `{itemId}.png` — e.g. `wood.png`, `stone_pick.png`, `m4.png`.

**Also accepted:** `wood_icon.png`, `StonePick.png`, `stone-pick.png` (case/underscore/dash insensitive).

`ThornsItemCatalog` entries with the same id add recipes/stats on top of icon-discovered items.

Mounted path in code: `ui/icons/{id}.png`

## Creature portraits (Tames tab)

Animal icons: **`Assets/ui/icons/creatures/`** — see [creatures/README.md](creatures/README.md).

Examples: `wolf.png`, `deer.png`, `panther.png`, `moose.png` (must match species `Key` in code).

Mounted path: `ui/icons/creatures/{speciesKey}.png`

## Skills

Skill icons: `Assets/ui/icons/skills/` — filename (without `.png`) is the skill id.

Your set (`hydration.png`, `irongut.png`, `luckychamber.png`, etc.) is supported. Underscore ids still work via aliases (`iron_gut` → `irongut.png`, `lucky_chamber` → `luckychamber.png`, `scavenger_skill` → `scavenger.png`).

## Verify in-game

After play, check the log:

`[Thorns UI] Icon cache: X loaded, Y missing — scanned N item PNG(s), M skill PNG(s)`

`Y missing` should be 0 for every file you added. Warnings list any ids still without a PNG.
