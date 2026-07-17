# Art source ledger

All runtime sprites under `Assets/textures/art` are original project assets.

| Source | Role |
|--------|------|
| `Tools/ArtPipeline/generate_sprites.py` | Procedural cozy pixel sheets + icons + anim strips |
| `Tools/ArtPipeline/import_hero_assets.py` | Imports Cursor-generated hero images, nearest-neighbor downscales, strips mattes |
| `Tools/ArtPipeline/validate_alpha.py` | Enforces true alpha on cutouts |

Reference screenshots used only as layout/style guides; they are not shipped in `Assets/`.
