# Main menu images

## Full-screen backdrop (required for menu)

Place a cinematic still here:

- `menu_background.png` — full-screen main menu **and join/load** background (recommended 1920×1080, PNG or JPG)

Path on disk: `Assets/ui/menu/menu_background.png` (mounted as `/ui/menu/menu_background.png`).

Until this exists, the UI uses a CSS gradient only.

**MainMenuHost** loads this image by default (`Load Backdrop Image` is on). Uncheck it only if a very large PNG causes boot issues.

## Server browser biome cards

Optional stills for the server detail panel:

- `biome_forest.png`
- `biome_snow.png`
- `biome_mountain.png`
- `biome_lake.png`
- `biome_plains.png`

Until these exist, biome cards fall back to `map/co_height.png`.
