"""Process magenta-keyed seabed / abyss PNGs into true-alpha Assets/textures/world."""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_transparent import process  # noqa: E402


SRC = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects-deep\assets")
OUT = Path(r"C:\Users\Macra\Projects\sandboxProjects\deep\Assets\textures\world")

MAPPING = {
	"seabed_fill.png": OUT / "seabed_fill.png",
	"seabed_ridge.png": OUT / "seabed_ridge.png",
	"abyss_silhouette.png": OUT / "abyss_silhouette.png",
}


def main() -> int:
	missing = [n for n in MAPPING if not (SRC / n).exists()]
	if missing:
		print("MISSING sources:", ", ".join(missing))
		return 1

	for name, dst in MAPPING.items():
		process(SRC / name, dst)
		print(f"  exists={dst.exists()} path={dst}")

	return 0


if __name__ == "__main__":
	sys.exit(main())
