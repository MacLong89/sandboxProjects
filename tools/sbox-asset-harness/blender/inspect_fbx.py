#!/usr/bin/env python3
"""Inspect meshes, armatures, and animation takes in an FBX file."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", required=True)
    parser.add_argument("--out", required=True)
    return parser.parse_args(argv)


def rounded(values) -> list[float]:
    return [round(float(value), 6) for value in values]


def main() -> int:
    import bpy
    from mathutils import Vector

    args = parse_args()
    source = Path(args.fbx).resolve()
    output = Path(args.out).resolve()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Blender 5.2's FBX importer still assigns the removed Cycles light
    # cast_shadow property when an FBX contains lights. Restore that RNA
    # property at runtime so geometry inspection is not blocked by scene lights.
    probe = bpy.data.lights.new("__fbx_import_probe__", "POINT")
    cycles_settings = type(probe.cycles)
    if not hasattr(cycles_settings, "cast_shadow"):
        cycles_settings.cast_shadow = bpy.props.BoolProperty(default=True)
    bpy.data.lights.remove(probe)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True)

    meshes = []
    armatures = []
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            world_corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
            meshes.append(
                {
                    "name": obj.name,
                    "vertices": len(obj.data.vertices),
                    "polygons": len(obj.data.polygons),
                    "location": rounded(obj.location),
                    "rotationEuler": rounded(obj.rotation_euler),
                    "scale": rounded(obj.scale),
                    "dimensions": rounded(obj.dimensions),
                    "boundsMin": rounded(
                        [min(corner[axis] for corner in world_corners) for axis in range(3)]
                    ),
                    "boundsMax": rounded(
                        [max(corner[axis] for corner in world_corners) for axis in range(3)]
                    ),
                    "materials": [
                        slot.material.name if slot.material else None
                        for slot in obj.material_slots
                    ],
                    "vertexGroups": len(obj.vertex_groups),
                    "armatureModifiers": [
                        modifier.object.name
                        for modifier in obj.modifiers
                        if modifier.type == "ARMATURE" and modifier.object
                    ],
                }
            )
        elif obj.type == "ARMATURE":
            armatures.append(
                {
                    "name": obj.name,
                    "bones": len(obj.data.bones),
                    "boneNames": [bone.name for bone in obj.data.bones],
                    "boneRest": {
                        bone.name: {
                            "head": rounded(bone.head_local),
                            "tail": rounded(bone.tail_local),
                            "parent": bone.parent.name if bone.parent else None,
                        }
                        for bone in obj.data.bones
                    },
                    "location": rounded(obj.location),
                    "rotationEuler": rounded(obj.rotation_euler),
                    "scale": rounded(obj.scale),
                    "dimensions": rounded(obj.dimensions),
                    "activeAction": (
                        obj.animation_data.action.name
                        if obj.animation_data and obj.animation_data.action
                        else None
                    ),
                    "nlaTracks": (
                        [track.name for track in obj.animation_data.nla_tracks]
                        if obj.animation_data
                        else []
                    ),
                }
            )

    actions = []
    for action in bpy.data.actions:
        actions.append(
            {
                "name": action.name,
                "frameRange": rounded(action.frame_range),
                "slots": len(action.slots) if hasattr(action, "slots") else None,
            }
        )

    result = {
        "source": source.as_posix(),
        "sceneFrameRange": [bpy.context.scene.frame_start, bpy.context.scene.frame_end],
        "meshes": meshes,
        "armatures": armatures,
        "actions": actions,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"Wrote {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
