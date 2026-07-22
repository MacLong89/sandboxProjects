#!/usr/bin/env python3
"""Headless skin-deformation QA for a rigged Blender file.

Plays every action, compares each mesh edge length against the bind pose, and
reports where the skin tears or collapses. Attributes each bad edge to the
dominant vertex group (bone) so weight fixes can be targeted.

Usage:
  blender --background <rigged.blend> --python measure_deformation.py -- \
      --out report.json [--frames 6] [--stretch 1.8] [--squash 0.45] [--top 20]

Exit code is 0 always (report via JSON); parse "TEAR_SCORE=" from stdout.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import defaultdict

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--frames", type=int, default=6, help="Samples per action")
    parser.add_argument("--stretch", type=float, default=1.8, help="Tear ratio threshold")
    parser.add_argument("--squash", type=float, default=0.45, help="Collapse ratio threshold")
    parser.add_argument("--top", type=int, default=20, help="Worst edges to list")
    return parser.parse_args(argv)


def find_rig() -> tuple:
    armature = next((obj for obj in bpy.data.objects if obj.type == "ARMATURE"), None)
    skinned = [
        obj
        for obj in bpy.data.objects
        if obj.type == "MESH" and any(m.type == "ARMATURE" for m in obj.modifiers)
    ]
    if not skinned or armature is None:
        raise RuntimeError(f"Need skinned mesh + armature; got {skinned}, arm={armature}")

    # The .blend keeps the donor (hidden) alongside our target. Prefer, in order:
    # explicit tripo name, then render-visible, then the highest-poly mesh.
    named = [obj for obj in skinned if "tripo" in obj.name.lower()]
    visible = [obj for obj in skinned if not obj.hide_render]
    mesh = (named or visible or sorted(skinned, key=lambda o: len(o.data.vertices)))[-1]
    if named:
        mesh = named[0]
    elif visible:
        mesh = max(visible, key=lambda o: len(o.data.vertices))
    else:
        mesh = max(skinned, key=lambda o: len(o.data.vertices))
    return mesh, armature


def dominant_group(mesh, vertex_index: int) -> str:
    best_name = "?"
    best_weight = -1.0
    vertex = mesh.data.vertices[vertex_index]
    for assignment in vertex.groups:
        if assignment.weight > best_weight:
            best_weight = assignment.weight
            best_name = mesh.vertex_groups[assignment.group].name
    return best_name


def edge_lengths(mesh, depsgraph) -> list:
    evaluated = mesh.evaluated_get(depsgraph)
    eval_mesh = evaluated.to_mesh()
    coords = [Vector(v.co) for v in eval_mesh.vertices]
    lengths = []
    for edge in mesh.data.edges:
        a, b = edge.vertices
        lengths.append((coords[a] - coords[b]).length)
    evaluated.to_mesh_clear()
    return lengths


def main() -> int:
    args = parse_args()
    mesh, armature = find_rig()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    scene = bpy.context.scene

    # Bind-pose reference edge lengths.
    armature.data.pose_position = "REST"
    if armature.animation_data:
        armature.animation_data.action = None
    scene.frame_set(1)
    depsgraph.update()
    rest = edge_lengths(mesh, depsgraph)

    edge_verts = [tuple(edge.vertices) for edge in mesh.data.edges]

    worst_edge = defaultdict(float)   # edge_index -> worst |log ratio|
    worst_ratio = {}                  # edge_index -> signed ratio at worst
    worst_action = {}                 # edge_index -> action label
    torn_by_bone = defaultdict(int)
    per_action = {}

    armature.animation_data_create()
    for action in bpy.data.actions:
        armature.data.pose_position = "POSE"
        armature.animation_data.action = action
        start, end = action.frame_range
        label = action.name.rsplit("|", 1)[-1]

        action_tears = 0
        action_max = 1.0
        frames = args.frames
        for i in range(frames):
            frame = round(start + (end - start) * (i / max(frames - 1, 1)))
            scene.frame_set(int(frame))
            depsgraph.update()
            current = edge_lengths(mesh, depsgraph)
            for index, (now, base) in enumerate(zip(current, rest)):
                if base < 1e-6:
                    continue
                ratio = now / base
                severity = abs((ratio if ratio >= 1 else 1 / ratio) - 1.0)
                if ratio > action_max:
                    action_max = ratio
                if ratio >= args.stretch or ratio <= args.squash:
                    action_tears += 1
                    if severity > worst_edge[index]:
                        worst_edge[index] = severity
                        worst_ratio[index] = ratio
                        worst_action[index] = label
        per_action[label] = {
            "maxStretch": round(action_max, 3),
            "tornEdgeSamples": action_tears,
        }

    for index in worst_edge:
        a, b = edge_verts[index]
        bone = dominant_group(mesh, a)
        torn_by_bone[bone] += 1

    ranked = sorted(worst_edge.items(), key=lambda kv: kv[1], reverse=True)[: args.top]
    worst_list = []
    for index, severity in ranked:
        a, b = edge_verts[index]
        worst_list.append(
            {
                "edge": index,
                "ratio": round(worst_ratio[index], 3),
                "action": worst_action[index],
                "boneA": dominant_group(mesh, a),
                "boneB": dominant_group(mesh, b),
            }
        )

    tear_score = len(worst_edge)
    report = {
        "meshVertices": len(mesh.data.vertices),
        "meshEdges": len(mesh.data.edges),
        "thresholds": {"stretch": args.stretch, "squash": args.squash, "frames": args.frames},
        "tearScore": tear_score,
        "tornEdgesByBone": dict(sorted(torn_by_bone.items(), key=lambda kv: kv[1], reverse=True)),
        "perAction": per_action,
        "worstEdges": worst_list,
    }
    with open(args.out, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2)

    print(f"TEAR_SCORE={tear_score}")
    print("TORN_BY_BONE=" + json.dumps(report["tornEdgesByBone"]))
    print(f"Wrote {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
