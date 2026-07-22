#!/usr/bin/env python3
"""Author an s&box .vmat from a basecolor image (Thorns convention).

Point it at any basecolor texture already sitting inside a project's Assets tree
(from the Tripo pipeline or a manual web download) and it writes a sibling
`<name>_basecolor.vmat` referencing that image with content-relative paths.

Standard-library only.

Examples:
    python tools/tripo_make_vmat.py thorns/Assets/models/fox/fox_basecolor.jpeg
    python tools/tripo_make_vmat.py fox_basecolor.png --normal fox_normal.png
"""

import argparse
from pathlib import Path

DEFAULT_NORMAL = "materials/default/default_normal.tga"
DEFAULT_AO = "materials/default/default_ao.tga"
DEFAULT_ROUGH = "materials/default/default_rough.tga"


def asset_relative_path(path: Path) -> str:
    """Return the s&box content-relative path (everything under .../Assets/)."""
    parts = path.resolve().parts
    assets_index = next(
        (i for i, part in enumerate(parts) if part.lower() == "assets"),
        None,
    )
    if assets_index is None:
        raise SystemExit(
            f"Texture must live inside an s&box 'Assets' folder: {path}"
        )
    return Path(*parts[assets_index + 1 :]).as_posix()


def write_vmat(
    basecolor: Path,
    *,
    normal: Path | None = None,
    dest: Path | None = None,
    name: str | None = None,
) -> Path:
    """Write a complex.shader .vmat next to (or at dest) the basecolor image."""
    if not basecolor.is_file():
        raise SystemExit(f"Basecolor image not found: {basecolor}")

    out_dir = dest or basecolor.parent
    # Derive material name: strip a trailing _basecolor if present.
    stem = name or basecolor.stem
    if stem.endswith("_basecolor"):
        stem = stem[: -len("_basecolor")]
    material_path = out_dir / f"{stem}_basecolor.vmat"

    color_resource = asset_relative_path(basecolor)
    normal_resource = asset_relative_path(normal) if normal else DEFAULT_NORMAL

    material_path.write_text(
        "\n".join(
            [
                "Layer0",
                "{",
                '\tshader "shaders/complex.shader_c"',
                "",
                f'\tTextureColor "{color_resource}"',
                f'\tTextureAmbientOcclusion "{DEFAULT_AO}"',
                f'\tTextureNormal "{normal_resource}"',
                f'\tTextureRoughness "{DEFAULT_ROUGH}"',
                "",
                "}",
                "",
            ]
        ),
        encoding="utf-8",
    )
    return material_path


def main() -> None:
    ap = argparse.ArgumentParser(description="Make an s&box .vmat from a basecolor image")
    ap.add_argument("basecolor", help="Path to the basecolor image (inside Assets/)")
    ap.add_argument("--normal", default="", help="Optional normal map path")
    ap.add_argument("--name", default="", help="Material base name (default: from image)")
    ap.add_argument("--out", default="", help="Output dir (default: alongside basecolor)")
    args = ap.parse_args()

    material = write_vmat(
        Path(args.basecolor),
        normal=Path(args.normal) if args.normal else None,
        dest=Path(args.out) if args.out else None,
        name=args.name or None,
    )
    print(f"Wrote {material}")


if __name__ == "__main__":
    main()
