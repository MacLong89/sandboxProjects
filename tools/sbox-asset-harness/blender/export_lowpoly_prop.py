#!/usr/bin/env python3
"""
Headless Blender exporter for simple low-poly props.

Usage:
  blender --background --python export_lowpoly_prop.py -- --id tree_pine_01 --kind tree --height 4.0

Writes:
  tools/sbox-asset-harness/out/<id>/model.glb
  tools/sbox-asset-harness/out/<id>/meta.json
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path


def _parse_args(argv: list[str]) -> argparse.Namespace:
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = argv[1:]

    p = argparse.ArgumentParser(description="Export a low-poly prop to GLB")
    p.add_argument("--id", required=True, help="Catalog id / folder name")
    p.add_argument("--kind", default="tree", choices=["tree", "bush", "crate", "rock"])
    p.add_argument("--height", type=float, default=4.0, help="Height in meters")
    p.add_argument("--out-root", default="", help="Override out root directory")
    return p.parse_args(argv)


def _out_dir(prop_id: str, out_root: str) -> Path:
    if out_root:
        root = Path(out_root)
    else:
        root = Path(__file__).resolve().parents[1] / "out"
    path = root / prop_id
    path.mkdir(parents=True, exist_ok=True)
    return path


def _clear_scene() -> None:
    import bpy

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)


def _add_cube(name: str, loc, scale, color) -> None:
    import bpy

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    mat = bpy.data.materials.new(name=f"{name}_mat")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    obj.data.materials.append(mat)


def _build_tree(height: float) -> None:
    trunk_h = height * 0.45
    canopy_h = height * 0.65
    trunk_w = max(0.12, height * 0.06)
    canopy_w = height * 0.35

    _add_cube(
        "Trunk",
        loc=(0.0, 0.0, trunk_h * 0.5),
        scale=(trunk_w, trunk_w, trunk_h),
        color=(0.62, 0.38, 0.14),
    )
    # Three stacked canopy boxes tapering upward
    for i, factor in enumerate((1.0, 0.75, 0.5)):
        w = canopy_w * factor
        h = canopy_h * 0.34
        z = trunk_h * 0.85 + h * 0.5 + i * h * 0.72
        _add_cube(
            f"Canopy_{i}",
            loc=(0.0, 0.0, z),
            scale=(w, w, h),
            color=(0.28 + i * 0.05, 0.78 - i * 0.06, 0.08),
        )


def _build_bush(height: float) -> None:
    for i, offset in enumerate(((-0.2, 0.0), (0.2, 0.1), (0.0, -0.15))):
        _add_cube(
            f"Bush_{i}",
            loc=(offset[0] * height, offset[1] * height, height * 0.45),
            scale=(height * 0.7, height * 0.7, height * 0.9),
            color=(0.25 + i * 0.04, 0.7 - i * 0.05, 0.12),
        )


def _build_crate(height: float) -> None:
    _add_cube(
        "Body",
        loc=(0.0, 0.0, height * 0.45),
        scale=(height, height, height * 0.9),
        color=(0.77, 0.60, 0.42),
    )
    _add_cube(
        "Lid",
        loc=(0.0, 0.0, height * 0.95),
        scale=(height * 1.02, height * 1.02, height * 0.12),
        color=(0.65, 0.49, 0.32),
    )


def _build_rock(height: float) -> None:
    for i in range(3):
        ang = i * (2.0 * math.pi / 3.0)
        _add_cube(
            f"Rock_{i}",
            loc=(math.cos(ang) * height * 0.15, math.sin(ang) * height * 0.15, height * 0.35),
            scale=(height * (0.7 - i * 0.1), height * 0.55, height * (0.7 - i * 0.08)),
            color=(0.45, 0.45, 0.48),
        )


def main() -> int:
    try:
        import bpy  # noqa: F401
    except ImportError:
        print(
            "ERROR: This script must be run inside Blender:\n"
            "  blender --background --python export_lowpoly_prop.py -- --id tree_pine_01 --kind tree",
            file=sys.stderr,
        )
        return 2

    import bpy

    args = _parse_args(sys.argv)
    out = _out_dir(args.id, args.out_root)

    _clear_scene()
    builders = {
        "tree": _build_tree,
        "bush": _build_bush,
        "crate": _build_crate,
        "rock": _build_rock,
    }
    builders[args.kind](args.height)

    # Join for a single export object
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    if len(bpy.context.selected_objects) > 1:
        bpy.ops.object.join()

    glb_path = out / "model.glb"
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB")

    meta = {
        "id": args.id,
        "kind": args.kind,
        "heightMeters": args.height,
        "glb": str(glb_path).replace("\\", "/"),
        "origin": "ground",
        "up": "+Z",
        "notes": "Import into s&box ModelDoc; set catalog vmdl when ready.",
    }
    (out / "meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print(f"Wrote {glb_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
