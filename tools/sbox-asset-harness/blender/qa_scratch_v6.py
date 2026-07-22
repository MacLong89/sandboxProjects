#!/usr/bin/env python3
"""Runtime-pose QA for donor-free scratch-v6 creature packages."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path


EXPECTED = {"Attack", "Death", "Gallop", "Idle", "Trot", "Walk"}
LOOPS = {"Gallop", "Idle", "Trot", "Walk"}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--samples", type=int, default=7)
    return parser.parse_args(argv)


def label(action) -> str:
    return action.name.rsplit("|", 1)[-1]


def quaternion_distance(left, right) -> float:
    delta = left.rotation_difference(right)
    return abs(float(delta.angle))


def evaluated_bounds(target, depsgraph):
    evaluated = target.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        points = [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        values = [coordinate for point in points for coordinate in point]
        finite = all(math.isfinite(float(value)) for value in values)
        minimum = [min(point[index] for point in points) for index in range(3)]
        maximum = [max(point[index] for point in points) for index in range(3)]
        dimensions = [maximum[index] - minimum[index] for index in range(3)]
        return {
            "finite": finite,
            "minimum": [float(value) for value in minimum],
            "maximum": [float(value) for value in maximum],
            "dimensions": [float(value) for value in dimensions],
        }
    finally:
        evaluated.to_mesh_clear()


def pose_snapshot(rig):
    return {
        bone.name: {
            "rotation": bone.rotation_quaternion.copy(),
            "location": bone.location.copy(),
        }
        for bone in rig.pose.bones
    }


def main() -> int:
    import bpy

    args = parse_args()
    output = Path(args.out).resolve()
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(armatures) != 1 or not meshes:
        raise RuntimeError(f"Expected one armature and at least one mesh; got {len(armatures)}, {len(meshes)}")
    rig = armatures[0]
    target = max(meshes, key=lambda obj: len(obj.data.vertices))
    rig.data.pose_position = "POSE"
    rig.animation_data_create()
    scene = bpy.context.scene
    depsgraph = bpy.context.evaluated_depsgraph_get()

    actions = {
        label(action): action
        for action in bpy.data.actions
        if action.name.startswith("ScratchV6_")
    }
    errors = []
    missing = sorted(EXPECTED - set(actions))
    unexpected = sorted(set(actions) - EXPECTED)
    if missing:
        errors.append(f"missing actions: {missing}")
    if unexpected:
        errors.append(f"unexpected actions: {unexpected}")

    scene.frame_set(1)
    rig.animation_data.action = None
    rig.data.pose_position = "REST"
    depsgraph.update()
    rest = evaluated_bounds(target, depsgraph)
    rest_height = max(rest["dimensions"][2], 1.0e-6)
    rest_max_dimension = max(rest["dimensions"])
    rig.data.pose_position = "POSE"
    action_reports = {}

    for action_label, action in sorted(actions.items()):
        rig.animation_data.action = action
        start, end = [int(round(value)) for value in action.frame_range]
        frames = sorted(
            {
                round(start + (end - start) * index / max(1, args.samples - 1))
                for index in range(args.samples)
            }
        )
        samples = []
        first_pose = None
        last_pose = None
        head_angles = []
        limb_angles = []
        root_locations = []
        max_dimension_ratio = 0.0
        minimum_z = float("inf")
        all_finite = True

        for frame in frames:
            scene.frame_set(frame)
            depsgraph.update()
            pose = pose_snapshot(rig)
            if frame == start:
                first_pose = pose
            if frame == end:
                last_pose = pose
            head = rig.pose.bones.get("Head")
            if head:
                head_angles.append(abs(float(head.rotation_quaternion.angle)))
            neck = rig.pose.bones.get("Neck1")
            if neck:
                head_angles.append(abs(float(neck.rotation_quaternion.angle)))
            # Jaw counts for attack QA (gators/fish snap) but not locomotion head-bob.
            if action_label == "Attack":
                jaw = rig.pose.bones.get("Jaw")
                if jaw:
                    head_angles.append(abs(float(jaw.rotation_quaternion.angle)))
            limb_angles.extend(
                abs(float(bone.rotation_quaternion.angle))
                for bone in rig.pose.bones
                if bone.name.startswith(("Front", "Hind"))
            )
            root_locations.append(tuple(float(value) for value in rig.pose.bones["Root"].location))
            bounds = evaluated_bounds(target, depsgraph)
            all_finite = all_finite and bounds["finite"]
            minimum_z = min(minimum_z, bounds["minimum"][2])
            max_dimension_ratio = max(
                max_dimension_ratio,
                max(bounds["dimensions"]) / max(rest_max_dimension, 1.0e-6),
            )
            samples.append({"frame": frame, "bounds": bounds})

        seam_rotation = 0.0
        seam_location = 0.0
        if first_pose and last_pose and action_label in LOOPS:
            for bone_name in first_pose:
                seam_rotation = max(
                    seam_rotation,
                    quaternion_distance(
                        first_pose[bone_name]["rotation"],
                        last_pose[bone_name]["rotation"],
                    ),
                )
                seam_location = max(
                    seam_location,
                    (
                        first_pose[bone_name]["location"]
                        - last_pose[bone_name]["location"]
                    ).length,
                )
            if seam_rotation > 1.0e-4 or seam_location > 1.0e-4:
                errors.append(
                    f"{action_label} loop seam rotation={seam_rotation:.6g} "
                    f"location={seam_location:.6g}"
                )

        limb_motion = max(limb_angles, default=0.0)
        if action_label in {"Walk", "Trot", "Gallop"} and limb_motion < 0.08:
            errors.append(f"{action_label} limb motion too small: {limb_motion:.4f}")
        head_motion = max(head_angles, default=0.0)
        if action_label in {"Walk", "Trot", "Gallop"} and head_motion > 0.18:
            errors.append(f"{action_label} head motion too large: {head_motion:.4f}")
        if not all_finite:
            errors.append(f"{action_label} produced non-finite evaluated vertices")
        if max_dimension_ratio > 2.75:
            errors.append(f"{action_label} AABB ratio too large: {max_dimension_ratio:.3f}")
        if action_label in {"Walk", "Trot", "Gallop", "Idle"} and minimum_z < -0.18 * rest_height:
            errors.append(f"{action_label} penetrates ground excessively: z={minimum_z:.4f}")

        root_travel = 0.0
        if root_locations:
            origin = root_locations[0]
            root_travel = max(
                math.dist(origin, location) for location in root_locations
            )
        if action_label == "Attack":
            # Attacks stay planted so gameplay position does not bounce.
            if root_travel > 0.02:
                errors.append(f"Attack root travel too large (should be planted): {root_travel:.4f}")
            if head_motion < 0.12 and limb_motion < 0.18:
                errors.append(
                    f"Attack motion too small: head={head_motion:.4f} limbs={limb_motion:.4f}"
                )
        if action_label == "Death":
            scene.frame_set(end)
            depsgraph.update()
            root_angle = abs(float(rig.pose.bones["Root"].rotation_quaternion.angle))
            if root_angle < 1.1:
                errors.append(f"Death does not reach side fall: root angle={root_angle:.4f}")

        action_reports[action_label] = {
            "frames": [start, end],
            "sampleFrames": frames,
            "loopSeamMaxRotation": seam_rotation,
            "loopSeamMaxLocation": seam_location,
            "maxHeadRotation": head_motion,
            "maxLimbRotation": limb_motion,
            "rootTravel": root_travel,
            "minimumZ": minimum_z,
            "maxDimensionRatio": max_dimension_ratio,
            "finite": all_finite,
            "samples": samples,
        }

    result = {
        "passed": not errors,
        "errors": errors,
        "armature": rig.name,
        "mesh": target.name,
        "boneCount": len(rig.data.bones),
        "vertexCount": len(target.data.vertices),
        "actions": action_reports,
        "restBounds": rest,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0 if result["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
