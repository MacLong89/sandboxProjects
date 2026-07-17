"""Process newly generated world/creature art into true-alpha PNGs under Assets/textures."""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

# Reuse shared helpers from make_transparent.py
sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_transparent import flood_clear_background, trim_transparent  # noqa: E402


SRC = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-deep\assets")
OUT = Path(r"C:\Users\Macra\Projects\sandboxProjects\deep\Assets\textures")


def process(src: Path, dst: Path) -> None:
	im = Image.open(src)
	out = flood_clear_background(im)
	out = trim_transparent(out)
	dst.parent.mkdir(parents=True, exist_ok=True)
	out.save(dst, "PNG")
	alphas = out.getchannel("A")
	amin, amax = alphas.getextrema()
	data = list(alphas.get_flattened_data())
	opaque = sum(1 for p in data if p > 200)
	clear = sum(1 for p in data if p < 10)
	print(f"{dst.name}: size={out.size} alpha=({amin},{amax}) opaque~{opaque} clear~{clear}")


def split_loot_strip(src: Path, out_dir: Path) -> None:
	"""Split a horizontal 4-icon strip into seashell/coin/scrap/pearl."""
	im = flood_clear_background(Image.open(src))
	w, h = im.size
	names = ["seashell.png", "old_coin.png", "scrap_metal.png", "pearl.png"]
	slice_w = w // 4
	out_dir.mkdir(parents=True, exist_ok=True)
	for i, name in enumerate(names):
		left = i * slice_w
		right = (i + 1) * slice_w if i < 3 else w
		crop = trim_transparent(im.crop((left, 0, right, h)))
		dst = out_dir / name
		crop.save(dst, "PNG")
		print(f"loot {name}: size={crop.size}")


def main() -> int:
	mapping = {
		"seabed_chunk.png": OUT / "world" / "seabed_chunk.png",
		"seabed_fill.png": OUT / "world" / "seabed_fill.png",
		"seabed_ridge.png": OUT / "world" / "seabed_ridge.png",
		"abyss_silhouette.png": OUT / "world" / "abyss_silhouette.png",
		"coral_cluster.png": OUT / "world" / "coral_cluster.png",
		"cave_overhang.png": OUT / "world" / "cave_overhang.png",
		"creature_jelly.png": OUT / "creatures" / "jellyfish.png",
		"creature_fish.png": OUT / "creatures" / "reef_fish.png",
		"creature_mine.png": OUT / "creatures" / "mine.png",
		"creature_puffer.png": OUT / "creatures" / "puffer.png",
		"creature_angler.png": OUT / "creatures" / "angler.png",
	}
	missing = [n for n in mapping if not (SRC / n).exists()]
	if missing:
		print("MISSING:", ", ".join(missing))
		return 1

	for name, dst in mapping.items():
		process(SRC / name, dst)

	loot = SRC / "loot_icons.png"
	if loot.exists():
		split_loot_strip(loot, OUT / "loot")
	else:
		print("MISSING loot_icons.png")

	return 0


if __name__ == "__main__":
	sys.exit(main())
