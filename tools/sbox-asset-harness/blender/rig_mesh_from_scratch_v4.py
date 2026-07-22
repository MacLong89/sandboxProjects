"""From-scratch Tripo wolf: custom deform rig + procedural animations.

No donor FBX. Only the target mesh is imported. Skeleton is built from mesh
landmarks; Idle / Walk / Gallop are authored with low-amplitude math so the
rear spine stays stable.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import math
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", required=True)
    parser.add_argument("--out-fbx", required=True)
    parser.add_argument("--out-anims", required=True)
    parser.add_argument("--out-blend", required=True)
    parser.add_argument("--preview-dir", required=True)
    # Tripo length is on X; +90 puts snout at -Y (matches our bone layout).
    parser.add_argument("--target-z-rotation", type=float, default=90.0)
    parser.add_argument("--target-height", type=float, default=1.15)
    return parser.parse_args(argv)


def load_base_module():
    path = Path(__file__).with_name("rig_mesh_from_donor.py")
    spec = importlib.util.spec_from_file_location("wolf_scratch_base", path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def bounds(obj):
    from mathutils import Vector

    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector(min(point[i] for point in points) for i in range(3)),
        Vector(max(point[i] for point in points) for i in range(3)),
    )


def prepare_target(target, rotation_degrees: float, target_height: float) -> dict:
    import bpy
    from mathutils import Vector

    target.rotation_euler[2] += math.radians(rotation_degrees)
    bpy.context.view_layer.objects.active = target
    target.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    minimum, maximum = bounds(target)
    size = maximum - minimum
    scale = target_height / size.z if size.z else 1.0
    target.scale = Vector((scale, scale, scale))
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    minimum, maximum = bounds(target)
    center = (minimum + maximum) * 0.5
    target.location.x -= center.x
    target.location.y -= center.y
    target.location.z -= minimum.z
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    fitted_min, fitted_max = bounds(target)
    return {
        "rotationDegrees": rotation_degrees,
        "targetHeight": target_height,
        "scale": scale,
        "boundsMin": list(fitted_min),
        "boundsMax": list(fitted_max),
    }


def region_mean(target, *, side: str | None, y_frac, z_frac, x_min_abs: float = 0.04):
    from mathutils import Vector

    minimum, maximum = bounds(target)
    size = maximum - minimum
    y0 = minimum.y + size.y * y_frac[0]
    y1 = minimum.y + size.y * y_frac[1]
    z0 = minimum.z + size.z * z_frac[0]
    z1 = minimum.z + size.z * z_frac[1]
    selected = []
    for vertex in target.data.vertices:
        if not (y0 <= vertex.co.y <= y1 and z0 <= vertex.co.z <= z1):
            continue
        if side == "L" and vertex.co.x < x_min_abs:
            continue
        if side == "R" and vertex.co.x > -x_min_abs:
            continue
        if side is None and abs(vertex.co.x) > size.x * 0.22:
            continue
        selected.append(vertex.co.copy())
    if not selected:
        raise RuntimeError(f"No landmark verts side={side} y={y_frac} z={z_frac}")
    point = sum(selected, Vector()) / len(selected)
    if side == "L":
        point.x = abs(point.x)
    elif side == "R":
        point.x = -abs(point.x)
    else:
        point.x = 0.0
    return point, len(selected)


def jaw_landmarks(target, head, neck):
    """Lower-muzzle tip + hinge for a Jaw deform bone (wolf and panther safe)."""

    from mathutils import Vector

    fallback_used = False
    try:
        jaw_tip, n_jaw = region_mean(
            target, side=None, y_frac=(0.0, 0.12), z_frac=(0.50, 0.76)
        )
    except RuntimeError:
        fallback_used = True
        try:
            # Broader band — panther snouts can sit outside the wolf window.
            jaw_tip, n_jaw = region_mean(
                target, side=None, y_frac=(0.0, 0.18), z_frac=(0.38, 0.88)
            )
        except RuntimeError:
            n_jaw = 0
            jaw_tip = Vector((0.0, head.y - 0.02, min(head.z, neck.z) - 0.05))

    # Hinge under/rear of muzzle; tip toward lower snout (more -Y).
    hinge_z = min(neck.z, head.z) * 0.55 + jaw_tip.z * 0.45
    jaw_hinge = Vector(
        (
            0.0,
            max(jaw_tip.y + 0.05, neck.y * 0.35 + head.y * 0.65),
            hinge_z,
        )
    )
    if jaw_hinge.z < jaw_tip.z:
        jaw_hinge.z = jaw_tip.z + 0.02
    if (jaw_tip - jaw_hinge).length < 0.04:
        jaw_tip = jaw_hinge + Vector((0.0, -0.08, -0.03))
    return jaw_hinge, jaw_tip, n_jaw, fallback_used


def add_bone(edit_bones, name: str, head, tail, parent=None, *, connect: bool = False):
    bone = edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    if parent is not None:
        bone.parent = parent
        bone.use_connect = connect
    return bone


def build_scratch_armature(target) -> tuple[object, dict]:
    import bpy
    from mathutils import Vector

    bpy.ops.object.armature_add(enter_editmode=True, location=(0.0, 0.0, 0.0))
    rig = bpy.context.object
    rig.name = "ScratchWolfArmature"
    rig.data.name = "ScratchWolfArmature"
    edit = rig.data.edit_bones
    # Remove the default single bone.
    for bone in list(edit):
        edit.remove(bone)

    hips, n_hips = region_mean(target, side=None, y_frac=(0.62, 0.78), z_frac=(0.55, 0.78))
    mid, n_mid = region_mean(target, side=None, y_frac=(0.40, 0.58), z_frac=(0.52, 0.78))
    chest, n_chest = region_mean(target, side=None, y_frac=(0.22, 0.38), z_frac=(0.55, 0.82))
    withers, n_with = region_mean(target, side=None, y_frac=(0.12, 0.24), z_frac=(0.62, 0.88))
    neck, n_neck = region_mean(target, side=None, y_frac=(0.04, 0.14), z_frac=(0.70, 0.92))
    head, n_head = region_mean(target, side=None, y_frac=(0.0, 0.08), z_frac=(0.72, 0.98))
    tail_root, n_tr = region_mean(target, side=None, y_frac=(0.78, 0.90), z_frac=(0.55, 0.82))
    tail_tip, n_tt = region_mean(target, side=None, y_frac=(0.90, 1.0), z_frac=(0.55, 0.95))

    root = add_bone(edit, "Root", hips + Vector((0, 0.02, -0.08)), hips)
    spine1 = add_bone(edit, "Spine1", hips, mid, root)
    spine2 = add_bone(edit, "Spine2", mid, chest, spine1, connect=True)
    spine3 = add_bone(edit, "Spine3", chest, withers, spine2, connect=True)
    neck1 = add_bone(edit, "Neck1", withers, neck, spine3, connect=True)
    head_bone = add_bone(edit, "Head", neck, head, neck1, connect=True)
    # Tiny tip so Head has length.
    if (head_bone.tail - head_bone.head).length < 0.05:
        head_bone.tail = head + Vector((0.0, -0.08, 0.02))

    jaw_hinge, jaw_tip, n_jaw, jaw_fallback = jaw_landmarks(target, head, neck)
    jaw_bone = add_bone(edit, "Jaw", jaw_hinge, jaw_tip, head_bone)
    if (jaw_bone.tail - jaw_bone.head).length < 0.04:
        jaw_bone.tail = jaw_bone.head + Vector((0.0, -0.08, -0.03))
    jaw_length = (jaw_bone.tail - jaw_bone.head).length

    tail_points = [
        tail_root.lerp(tail_tip, t) for t in (0.0, 0.33, 0.66, 1.0)
    ]
    prev = spine1
    for index, (a, b) in enumerate(zip(tail_points, tail_points[1:] + [tail_tip])):
        if index == 3:
            b = tail_tip + Vector((0.0, 0.06, 0.04))
        bone = add_bone(edit, f"Tail{index + 1}", a, b, prev, connect=(index > 0))
        prev = bone

    landmarks = {"centerline": {"hips": n_hips, "mid": n_mid, "chest": n_chest}}
    for side, sign in (("L", 1.0), ("R", -1.0)):
        f_shoulder, ns = region_mean(target, side=side, y_frac=(0.10, 0.28), z_frac=(0.58, 0.82))
        f_elbow, ne = region_mean(target, side=side, y_frac=(0.08, 0.32), z_frac=(0.34, 0.58))
        f_wrist, nw = region_mean(target, side=side, y_frac=(0.06, 0.34), z_frac=(0.14, 0.34))
        f_paw, np_ = region_mean(target, side=side, y_frac=(0.04, 0.36), z_frac=(0.0, 0.14))

        h_hip, nh = region_mean(target, side=side, y_frac=(0.58, 0.78), z_frac=(0.55, 0.82))
        h_knee, nk = region_mean(target, side=side, y_frac=(0.55, 0.82), z_frac=(0.32, 0.58))
        h_hock, nho = region_mean(target, side=side, y_frac=(0.52, 0.88), z_frac=(0.12, 0.34))
        h_paw, nhp = region_mean(target, side=side, y_frac=(0.50, 0.92), z_frac=(0.0, 0.14))

        # Keep limbs laterally clear of the spine.
        for point in (f_shoulder, f_elbow, f_wrist, f_paw, h_hip, h_knee, h_hock, h_paw):
            point.x = sign * max(abs(point.x), 0.09)

        sh = add_bone(edit, f"FrontShoulder.{side}", f_shoulder, f_elbow, spine3)
        fu = add_bone(edit, f"FrontUpper.{side}", f_elbow, f_wrist, sh, connect=True)
        fl = add_bone(edit, f"FrontLower.{side}", f_wrist, f_paw, fu, connect=True)
        _ = (fu, fl)

        hu = add_bone(edit, f"HindUpper.{side}", h_hip, h_knee, spine1)
        hl = add_bone(edit, f"HindLower.{side}", h_knee, h_hock, hu, connect=True)
        hf = add_bone(edit, f"HindFoot.{side}", h_hock, h_paw, hl, connect=True)
        _ = hf

        landmarks[side] = {
            "frontSamples": [ns, ne, nw, np_],
            "hindSamples": [nh, nk, nho, nhp],
        }

    bpy.ops.object.mode_set(mode="OBJECT")
    for bone in rig.data.bones:
        bone.use_deform = bone.name != "Root"
    rig.show_in_front = True
    return rig, {
        "boneCount": len(rig.data.bones),
        "deformBones": sum(1 for bone in rig.data.bones if bone.use_deform),
        "landmarks": landmarks,
        "samples": {
            "withers": n_with,
            "neck": n_neck,
            "head": n_head,
            "jaw": n_jaw,
            "tailRoot": n_tr,
            "tailTip": n_tt,
        },
        "jaw": {
            "samples": n_jaw,
            "fallbackUsed": jaw_fallback,
            "length": jaw_length,
        },
    }


def bind_mesh(target, rig) -> dict:
    import bpy

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    try:
        bpy.ops.object.vertex_group_smooth(
            group_select_mode="ALL", factor=0.22, repeat=1, expand=0.0
        )
    except RuntimeError:
        pass
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    # Soften opposite-side limb bleed.
    limb_tokens = ("Front", "Hind")
    stripped = 0
    for vertex in target.data.vertices:
        if abs(vertex.co.x) < 0.04:
            continue
        wrong = ".R" if vertex.co.x > 0.0 else ".L"
        changed = False
        for assignment in list(vertex.groups):
            name = target.vertex_groups[assignment.group].name
            if name.endswith(wrong) and any(token in name for token in limb_tokens):
                target.vertex_groups[name].remove([vertex.index])
                changed = True
        if changed:
            stripped += 1
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    weighted = sum(
        1 for vertex in target.data.vertices
        if any(group.weight > 0.0001 for group in vertex.groups)
    )
    return {
        "method": "scratch heat bind",
        "weightedVertices": weighted,
        "unweightedVertices": len(target.data.vertices) - weighted,
        "oppositeSideStripped": stripped,
        "groups": len(target.vertex_groups),
    }


def stabilize_head_neck_weights(target) -> dict:
    """Isolate upper-front mesh from limb drag; assign Head/Jaw/Neck1/Spine3."""

    import bpy

    minimum, maximum = bounds(target)
    size = maximum - minimum
    if size.y < 1.0e-6 or size.z < 1.0e-6:
        return {"stabilizedVertices": 0, "jawWeightedVertices": 0}

    front_y_max = minimum.y + size.y * 0.25
    # Keep lower front legs out of this pass (paws / lower limbs).
    leg_z_max = minimum.z + size.z * 0.38

    needed = ("Head", "Jaw", "Neck1", "Spine3")
    groups = {name: target.vertex_groups.get(name) for name in needed}
    for name in needed:
        if groups[name] is None:
            groups[name] = target.vertex_groups.new(name=name)

    limb_tokens = ("Front", "Hind")
    stabilized = 0
    jaw_weighted = 0

    for vertex in target.data.vertices:
        if vertex.co.y > front_y_max or vertex.co.z <= leg_z_max:
            continue

        # Drop limb influences that drag the snout with front legs.
        for assignment in list(vertex.groups):
            name = target.vertex_groups[assignment.group].name
            if any(token in name for token in limb_tokens):
                target.vertex_groups[name].remove([vertex.index])

        y_t = (vertex.co.y - minimum.y) / (size.y * 0.25)  # 0 snout → 1 withers band
        z_t = (vertex.co.z - minimum.z) / size.z
        y_t = max(0.0, min(1.0, y_t))
        z_t = max(0.0, min(1.0, z_t))

        # Soft region blends; keep at most 3 contributors.
        weights = {
            "Head": 0.0,
            "Jaw": 0.0,
            "Neck1": 0.0,
            "Spine3": 0.0,
        }
        if y_t < 0.42:
            # Snout / skull: upper → Head, lower → Jaw + Head.
            if z_t >= 0.72:
                weights["Head"] = 1.0
            elif z_t >= 0.58:
                jaw_w = (0.72 - z_t) / 0.14
                weights["Jaw"] = 0.55 * jaw_w + 0.15
                weights["Head"] = 1.0 - weights["Jaw"]
            else:
                weights["Jaw"] = 0.72
                weights["Head"] = 0.28
        elif y_t < 0.72:
            # Neck column; slight Head bleed near the skull.
            neck_w = 0.55 + 0.35 * ((y_t - 0.42) / 0.30)
            head_bleed = max(0.0, 0.28 * (1.0 - (y_t - 0.42) / 0.30))
            weights["Neck1"] = neck_w
            weights["Head"] = head_bleed
            if z_t < 0.62 and y_t < 0.55:
                weights["Jaw"] = 0.18 * (1.0 - (y_t - 0.42) / 0.20)
        else:
            # Mane / withers transition into Spine3.
            blend = (y_t - 0.72) / 0.28
            weights["Spine3"] = 0.35 + 0.55 * blend
            weights["Neck1"] = 1.0 - weights["Spine3"]

        ranked = sorted(
            ((name, weight) for name, weight in weights.items() if weight > 0.02),
            key=lambda item: item[1],
            reverse=True,
        )[:3]
        total = sum(weight for _, weight in ranked)
        if total <= 1.0e-6:
            continue

        # Clear remaining axial groups we own, then write the blend.
        for name in needed:
            groups[name].remove([vertex.index])
        for name, weight in ranked:
            groups[name].add([vertex.index], weight / total, "REPLACE")

        stabilized += 1
        if any(name == "Jaw" for name, _ in ranked):
            jaw_weighted += 1

    bpy.context.view_layer.objects.active = target
    try:
        bpy.ops.object.vertex_group_smooth(
            group_select_mode="ALL", factor=0.18, repeat=1, expand=0.0
        )
    except RuntimeError:
        pass
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=3)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    # Recount Jaw after smooth/limit (smooth can redistribute slightly).
    jaw_group = target.vertex_groups.get("Jaw")
    jaw_index = jaw_group.index if jaw_group else -1
    jaw_weighted = 0
    if jaw_index >= 0:
        for vertex in target.data.vertices:
            for assignment in vertex.groups:
                if assignment.group == jaw_index and assignment.weight > 0.02:
                    jaw_weighted += 1
                    break

    return {
        "stabilizedVertices": stabilized,
        "jawWeightedVertices": jaw_weighted,
        "frontYMax": front_y_max,
        "legZMax": leg_z_max,
        "maxGroups": 3,
    }


def quat_axis_angle(axis: str, radians: float):
    from mathutils import Quaternion

    axes = {"x": (1.0, 0.0, 0.0), "y": (0.0, 1.0, 0.0), "z": (0.0, 0.0, 1.0)}
    return Quaternion(axes[axis], radians)


def set_bone_rotation(pose_bone, quat) -> None:
    pose_bone.rotation_mode = "QUATERNION"
    pose_bone.rotation_quaternion = quat


def key_pose(rig, frame: int, names: list[str]) -> None:
    for name in names:
        bone = rig.pose.bones.get(name)
        if bone is None:
            continue
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=name)
        bone.keyframe_insert("location", frame=frame, group=name)


def reset_pose(rig) -> None:
    from mathutils import Quaternion

    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion()
        bone.location = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def create_action(rig, name: str, start: int, end: int):
    import bpy

    action = bpy.data.actions.new(name=name)
    action.use_fake_user = True
    action.frame_start = start
    action.frame_end = end
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = action
    return action


def smoothstep(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def blend_amount(value: float, mode: str = "smoothstep") -> float:
    value = max(0.0, min(1.0, value))
    if mode == "linear":
        return value
    if mode == "ease_in":
        return value * value
    if mode == "ease_out":
        return 1.0 - (1.0 - value) * (1.0 - value)
    if mode == "ease_in_out":
        return smoothstep(value)
    return smoothstep(value)


def sample_action_pose(frame: int, frames: int, poses, blend_mode: str = "smoothstep"):
    """Interpolate scalar rotations and Root locations between poses."""

    progress = (frame - 1) / max(1, frames - 1)
    for index in range(len(poses) - 1):
        start_t, start_rotations, start_location = poses[index]
        end_t, end_rotations, end_location = poses[index + 1]
        if progress <= end_t:
            blend = blend_amount(
                (progress - start_t) / max(1.0e-6, end_t - start_t),
                blend_mode,
            )
            rotations = {}
            for name in set(start_rotations) | set(end_rotations):
                start_rotation = start_rotations.get(name)
                end_rotation = end_rotations.get(name)
                start_axis, start_angle = start_rotation or (end_rotation[0], 0.0)
                end_axis, end_angle = end_rotation or (start_axis, 0.0)
                if start_axis != end_axis:
                    raise ValueError(f"Rotation axis changed for {name}")
                rotations[name] = (
                    start_axis,
                    start_angle + (end_angle - start_angle) * blend,
                )
            location = tuple(
                start + (end - start) * blend
                for start, end in zip(start_location, end_location)
            )
            return rotations, location
    return poses[-1][1], poses[-1][2]


def author_idle(rig) -> dict:
    """Subtle breathing / weight shift — almost no rear thrash."""

    from mathutils import Quaternion

    frames = 60
    action = create_action(rig, "Scratch|Idle", 1, frames)
    names = [bone.name for bone in rig.pose.bones]
    for frame in range(1, frames + 1):
        t = (frame - 1) / frames
        wave = math.sin(t * math.tau)
        reset_pose(rig)
        set_bone_rotation(rig.pose.bones["Spine2"], quat_axis_angle("x", 0.025 * wave))
        set_bone_rotation(rig.pose.bones["Spine3"], quat_axis_angle("x", 0.02 * wave))
        set_bone_rotation(rig.pose.bones["Neck1"], quat_axis_angle("z", 0.03 * math.sin(t * math.tau * 0.5)))
        set_bone_rotation(rig.pose.bones["Head"], quat_axis_angle("x", 0.02 * math.sin(t * math.tau + 0.4)))
        set_bone_rotation(rig.pose.bones["Tail2"], quat_axis_angle("z", 0.04 * wave))
        set_bone_rotation(rig.pose.bones["Tail3"], quat_axis_angle("z", 0.05 * wave))
        # Tiny root settle on Z only.
        rig.pose.bones["Root"].location = (0.0, 0.0, 0.004 * wave)
        key_pose(rig, frame, names)
    return {"name": action.name, "frames": frames}


def author_walk(rig) -> dict:
    """Diagonal quadruped walk with deliberately quiet spine."""

    frames = 32
    action = create_action(rig, "Scratch|Walk", 1, frames)
    names = [bone.name for bone in rig.pose.bones]
    # Amplitudes in radians — keep spine tiny so rear doesn't thrash.
    thigh = 0.38
    shin = 0.32
    shoulder = 0.30
    front_upper = 0.34
    front_lower = 0.28
    spine = 0.035
    for frame in range(1, frames + 1):
        t = (frame - 1) / frames
        phase = t * math.tau
        # Diagonal pairs: FL+HR vs FR+HL
        fl = math.sin(phase)
        fr = math.sin(phase + math.pi)
        hl = math.sin(phase + math.pi)
        hr = math.sin(phase)
        reset_pose(rig)

        set_bone_rotation(rig.pose.bones["Spine1"], quat_axis_angle("x", spine * math.sin(phase)))
        set_bone_rotation(rig.pose.bones["Spine2"], quat_axis_angle("x", -0.5 * spine * math.sin(phase)))
        set_bone_rotation(rig.pose.bones["Spine3"], quat_axis_angle("z", 0.03 * math.sin(phase)))
        # Quiet head/neck — phase offset so it does not lock to a leg pair.
        set_bone_rotation(rig.pose.bones["Neck1"], quat_axis_angle("x", 0.003 * math.sin(phase * 1.35 + 0.9)))
        set_bone_rotation(rig.pose.bones["Head"], quat_axis_angle("x", 0.002 * math.sin(phase * 1.55 + 1.7)))
        set_bone_rotation(rig.pose.bones["Tail2"], quat_axis_angle("z", 0.06 * math.sin(phase)))
        set_bone_rotation(rig.pose.bones["Tail3"], quat_axis_angle("z", 0.08 * math.sin(phase + 0.3)))

        for side, front_s, hind_s in (("L", fl, hl), ("R", fr, hr)):
            set_bone_rotation(
                rig.pose.bones[f"FrontShoulder.{side}"],
                quat_axis_angle("x", shoulder * front_s),
            )
            set_bone_rotation(
                rig.pose.bones[f"FrontUpper.{side}"],
                quat_axis_angle("x", front_upper * front_s),
            )
            set_bone_rotation(
                rig.pose.bones[f"FrontLower.{side}"],
                quat_axis_angle("x", -front_lower * max(front_s, 0.0)),
            )
            set_bone_rotation(
                rig.pose.bones[f"HindUpper.{side}"],
                quat_axis_angle("x", thigh * hind_s),
            )
            set_bone_rotation(
                rig.pose.bones[f"HindLower.{side}"],
                quat_axis_angle("x", -shin * max(hind_s, 0.0)),
            )
            set_bone_rotation(
                rig.pose.bones[f"HindFoot.{side}"],
                quat_axis_angle("x", 0.18 * hind_s),
            )

        rig.pose.bones["Root"].location = (
            0.0,
            -0.01 * math.sin(phase),
            0.012 * abs(math.sin(phase * 2.0)),
        )
        key_pose(rig, frame, names)
    return {"name": action.name, "frames": frames}


def author_gallop(rig) -> dict:
    """Simple gathered/extended gallop; spine still restrained."""

    frames = 20
    action = create_action(rig, "Scratch|Gallop", 1, frames)
    names = [bone.name for bone in rig.pose.bones]
    for frame in range(1, frames + 1):
        t = (frame - 1) / frames
        phase = t * math.tau
        gather = math.sin(phase)
        reset_pose(rig)
        set_bone_rotation(rig.pose.bones["Spine1"], quat_axis_angle("x", 0.06 * gather))
        set_bone_rotation(rig.pose.bones["Spine2"], quat_axis_angle("x", -0.05 * gather))
        set_bone_rotation(rig.pose.bones["Spine3"], quat_axis_angle("x", 0.04 * gather))
        set_bone_rotation(rig.pose.bones["Neck1"], quat_axis_angle("x", -0.005 * gather))
        set_bone_rotation(rig.pose.bones["Head"], quat_axis_angle("x", -0.003 * gather))
        set_bone_rotation(rig.pose.bones["Tail2"], quat_axis_angle("x", 0.08 * gather))
        set_bone_rotation(rig.pose.bones["Tail3"], quat_axis_angle("x", 0.1 * gather))

        front = math.sin(phase)
        hind = math.sin(phase + math.pi)
        for side in ("L", "R"):
            # Slight L/R offset so legs aren't perfectly synced.
            lag = 0.12 if side == "R" else 0.0
            f = math.sin(phase + lag)
            h = math.sin(phase + math.pi + lag)
            set_bone_rotation(rig.pose.bones[f"FrontShoulder.{side}"], quat_axis_angle("x", 0.42 * f))
            set_bone_rotation(rig.pose.bones[f"FrontUpper.{side}"], quat_axis_angle("x", 0.48 * f))
            set_bone_rotation(rig.pose.bones[f"FrontLower.{side}"], quat_axis_angle("x", -0.35 * max(f, 0.0)))
            set_bone_rotation(rig.pose.bones[f"HindUpper.{side}"], quat_axis_angle("x", 0.5 * h))
            set_bone_rotation(rig.pose.bones[f"HindLower.{side}"], quat_axis_angle("x", -0.4 * max(h, 0.0)))
            set_bone_rotation(rig.pose.bones[f"HindFoot.{side}"], quat_axis_angle("x", 0.22 * h))

        rig.pose.bones["Root"].location = (0.0, -0.02 * front, 0.03 * abs(gather))
        key_pose(rig, frame, names)
    return {"name": action.name, "frames": frames, "note": "front/hind gather-extend"}


def author_attack(rig) -> dict:
    """Anticipation, bilateral forward lunge + bite, then recover (non-loop)."""

    frames = 42
    action = create_action(rig, "Scratch|Attack", 1, frames)
    names = [bone.name for bone in rig.pose.bones]
    rest = {}
    # Strike peak ~frame 21 (t≈0.49): Root -Y ~0.25, both fronts reach, jaw snaps.
    poses = [
        (0.0, rest, (0.0, 0.0, 0.0)),
        (
            0.18,
            {
                "Spine2": ("x", -0.08),
                "Spine3": ("x", -0.12),
                "Neck1": ("x", 0.14),
                "Head": ("x", 0.10),
                "Jaw": ("x", 0.18),
                "FrontShoulder.L": ("x", -0.16),
                "FrontShoulder.R": ("x", -0.20),
                "FrontUpper.L": ("x", -0.22),
                "FrontUpper.R": ("x", -0.26),
                "FrontLower.L": ("x", 0.18),
                "FrontLower.R": ("x", 0.22),
                "HindUpper.L": ("x", -0.10),
                "HindUpper.R": ("x", -0.12),
                "Tail1": ("x", -0.06),
            },
            (0.0, 0.035, 0.012),
        ),
        (
            0.36,
            {
                "Spine2": ("x", -0.04),
                "Spine3": ("x", 0.06),
                "Neck1": ("x", -0.16),
                "Head": ("x", -0.12),
                "Jaw": ("x", 0.48),
                "FrontShoulder.L": ("x", 0.28),
                "FrontShoulder.R": ("x", 0.34),
                "FrontUpper.L": ("x", 0.36),
                "FrontUpper.R": ("x", 0.42),
                "FrontLower.L": ("x", -0.16),
                "FrontLower.R": ("x", -0.20),
                "HindUpper.L": ("x", 0.10),
                "HindUpper.R": ("x", 0.12),
                "HindLower.L": ("x", -0.08),
                "HindLower.R": ("x", -0.10),
                "Tail1": ("x", 0.06),
            },
            (0.0, -0.12, 0.02),
        ),
        (
            0.50,
            {
                "Root": ("x", -0.10),
                "Spine2": ("x", 0.06),
                "Spine3": ("x", 0.14),
                "Neck1": ("x", -0.32),
                "Head": ("x", -0.22),
                "Jaw": ("x", 0.04),
                "FrontShoulder.L": ("x", 0.40),
                "FrontShoulder.R": ("x", 0.46),
                "FrontUpper.L": ("x", 0.48),
                "FrontUpper.R": ("x", 0.54),
                "FrontLower.L": ("x", -0.22),
                "FrontLower.R": ("x", -0.26),
                "HindUpper.L": ("x", 0.16),
                "HindUpper.R": ("x", 0.18),
                "HindLower.L": ("x", -0.12),
                "HindLower.R": ("x", -0.14),
                "Tail1": ("x", 0.10),
            },
            (0.0, -0.25, 0.022),
        ),
        (
            0.60,
            {
                "Root": ("x", -0.06),
                "Spine2": ("x", 0.04),
                "Spine3": ("x", 0.10),
                "Neck1": ("x", -0.22),
                "Head": ("x", -0.14),
                "Jaw": ("x", 0.16),
                "FrontShoulder.L": ("x", 0.28),
                "FrontShoulder.R": ("x", 0.32),
                "FrontUpper.L": ("x", 0.30),
                "FrontUpper.R": ("x", 0.34),
                "FrontLower.L": ("x", -0.14),
                "FrontLower.R": ("x", -0.16),
                "HindUpper.L": ("x", 0.10),
                "HindUpper.R": ("x", 0.12),
            },
            (0.0, -0.16, 0.014),
        ),
        (
            0.78,
            {
                "Spine2": ("x", 0.02),
                "Spine3": ("x", 0.04),
                "Neck1": ("x", -0.08),
                "Head": ("x", -0.04),
                "Jaw": ("x", 0.06),
                "FrontShoulder.L": ("x", 0.10),
                "FrontShoulder.R": ("x", 0.12),
                "FrontUpper.L": ("x", 0.10),
                "FrontUpper.R": ("x", 0.12),
                "HindUpper.L": ("x", 0.04),
                "HindUpper.R": ("x", 0.04),
            },
            (0.0, -0.06, 0.006),
        ),
        (1.0, rest, (0.0, 0.0, 0.0)),
    ]
    for frame in range(1, frames + 1):
        reset_pose(rig)
        rotations, root_location = sample_action_pose(frame, frames, poses)
        for name, (axis, angle) in rotations.items():
            bone = rig.pose.bones.get(name)
            if bone is None:
                continue
            set_bone_rotation(bone, quat_axis_angle(axis, angle))
        rig.pose.bones["Root"].location = root_location
        key_pose(rig, frame, names)
    return {
        "name": action.name,
        "frames": frames,
        "note": "windup-bilateral lunge-bite-recover",
        "strikeRootY": -0.25,
        "jawOpenPeak": 0.48,
    }


def author_death(rig) -> dict:
    """Stagger, sideways fall via Root local-Z roll, fold, and hold side-lying."""

    frames = 70
    action = create_action(rig, "Scratch|Death", 1, frames)
    names = [bone.name for bone in rig.pose.bones]
    rest = {}
    # Root bone local Y is along its vertical shaft; rotate local Z for a
    # world-space sideways tip (validated against mesh AABB after build).
    root_axis = "z"
    stagger = {
        "Root": (root_axis, -0.12),
        "Spine2": ("x", -0.05),
        "Spine3": ("x", -0.06),
        "Neck1": ("x", 0.10),
        "Head": ("x", 0.08),
        "Jaw": ("x", 0.06),
        "FrontUpper.L": ("x", -0.10),
        "HindUpper.R": ("x", 0.10),
    }
    falling = {
        "Root": (root_axis, 0.85),
        "Spine2": ("x", -0.06),
        "Spine3": ("x", -0.08),
        "Neck1": ("x", -0.16),
        "Head": ("x", -0.12),
        "Jaw": ("x", 0.14),
        "FrontUpper.L": ("x", -0.24),
        "FrontUpper.R": ("x", -0.20),
        "FrontLower.L": ("x", 0.20),
        "FrontLower.R": ("x", 0.18),
        "HindUpper.L": ("x", 0.20),
        "HindUpper.R": ("x", 0.18),
        "HindLower.L": ("x", -0.24),
        "HindLower.R": ("x", -0.20),
        "HindFoot.L": ("x", 0.12),
        "HindFoot.R": ("x", 0.10),
        "Tail1": ("x", -0.08),
    }
    settled = {
        "Root": (root_axis, 1.42),
        "Spine2": ("x", -0.08),
        "Spine3": ("x", -0.12),
        "Neck1": ("x", -0.28),
        "Head": ("x", -0.22),
        "Jaw": ("x", 0.18),
        "FrontShoulder.L": ("x", -0.12),
        "FrontShoulder.R": ("x", -0.08),
        "FrontUpper.L": ("x", -0.36),
        "FrontUpper.R": ("x", -0.30),
        "FrontLower.L": ("x", 0.32),
        "FrontLower.R": ("x", 0.28),
        "HindUpper.L": ("x", 0.34),
        "HindUpper.R": ("x", 0.28),
        "HindLower.L": ("x", -0.38),
        "HindLower.R": ("x", -0.32),
        "HindFoot.L": ("x", 0.20),
        "HindFoot.R": ("x", 0.16),
        "Tail1": ("x", -0.14),
        "Tail2": ("x", -0.10),
    }
    poses = [
        (0.0, rest, (0.0, 0.0, 0.0)),
        (0.14, stagger, (0.04, 0.02, 0.0)),
        (0.42, falling, (0.22, 0.02, -0.18)),
        # Side-lying reached by ~65%, then hold.
        (0.65, settled, (0.38, 0.02, -0.36)),
        (1.0, settled, (0.40, 0.02, -0.38)),
    ]
    for frame in range(1, frames + 1):
        reset_pose(rig)
        rotations, root_location = sample_action_pose(frame, frames, poses)
        for name, (axis, angle) in rotations.items():
            bone = rig.pose.bones.get(name)
            if bone is None:
                continue
            set_bone_rotation(bone, quat_axis_angle(axis, angle))
        rig.pose.bones["Root"].location = root_location
        key_pose(rig, frame, names)
    return {
        "name": action.name,
        "frames": frames,
        "note": "stagger-side fall-fold-settle",
        "rootAxis": root_axis,
        "finalRootRotation": 1.42,
    }


def orientation_report(rig, target) -> dict:
    root = rig.data.bones.get("Spine1")
    head = rig.data.bones.get("Head")
    ys = [vertex.co.y for vertex in target.data.vertices]
    y_min, y_max = min(ys), max(ys)
    front_band = y_min + (y_max - y_min) * 0.2
    rear_band = y_max - (y_max - y_min) * 0.2
    front_verts = [v for v in target.data.vertices if v.co.y <= front_band]
    rear_verts = [v for v in target.data.vertices if v.co.y >= rear_band]
    front_z = sum(v.co.z for v in front_verts) / max(1, len(front_verts))
    rear_z = sum(v.co.z for v in rear_verts) / max(1, len(rear_verts))
    mesh_snout_at_neg_y = front_z > rear_z
    bones_face_neg_y = bool(root and head and head.head_local.y < root.head_local.y)
    return {
        "facingOk": mesh_snout_at_neg_y and bones_face_neg_y,
        "expected": "-Y",
        "rootY": root.head_local.y if root else None,
        "headY": head.head_local.y if head else None,
        "meshSnoutAtNegY": mesh_snout_at_neg_y,
        "bonesFaceNegY": bones_face_neg_y,
        "frontThirdAvgZ": front_z,
        "rearThirdAvgZ": rear_z,
    }


def export_mesh(path: Path, target, rig) -> None:
    import bpy

    target.name = "tripo_wolf_scratch_v4"
    target.data.name = "tripo_wolf_scratch_v4"
    rig.name = "ScratchWolfArmature"
    rig.data.name = "ScratchWolfArmature"
    rig.data.pose_position = "REST"
    if rig.animation_data:
        rig.animation_data.action = None
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="AUTO",
    )


def export_anims(path: Path, rig) -> None:
    import bpy

    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.data.pose_position = "POSE"
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE"},
        apply_unit_scale=True,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        path_mode="AUTO",
    )
    rig.data.pose_position = "REST"


def main() -> int:
    import bpy

    args = parse_args()
    base = load_base_module()
    target_path = Path(args.target).resolve()
    out_fbx = Path(args.out_fbx).resolve()
    out_anims = Path(args.out_anims).resolve()
    out_blend = Path(args.out_blend).resolve()
    preview_dir = Path(args.preview_dir).resolve()

    base.clear_scene()
    imported = base.import_fbx(target_path)
    target = max(
        (obj for obj in imported if obj.type == "MESH"),
        key=lambda obj: len(obj.data.vertices),
    )
    # Drop any armature Tripo may have shipped; we build our own.
    for obj in list(imported):
        if obj.type == "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)

    bpy.context.scene.render.fps = 30
    alignment = prepare_target(target, args.target_z_rotation, args.target_height)
    rig, rig_report = build_scratch_armature(target)
    orientation = orientation_report(rig, target)
    skin = bind_mesh(target, rig)
    stabilize = stabilize_head_neck_weights(target)
    skin["stabilize"] = stabilize
    animations = {
        "idle": author_idle(rig),
        "walk": author_walk(rig),
        "gallop": author_gallop(rig),
        "attack": author_attack(rig),
        "death": author_death(rig),
    }
    if rig.animation_data:
        rig.animation_data.action = None

    # Keep the animated target visible; use the rig as the hide_render dummy.
    previews = base.render_previews(rig, target, rig, preview_dir)
    export_mesh(out_fbx, target, rig)
    export_anims(out_anims, rig)
    out_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))

    report = {
        "pipeline": "scratch_landmark_rig_procedural_anims_v4",
        "donorUsed": False,
        "target": target_path.as_posix(),
        "outFbx": out_fbx.as_posix(),
        "outAnims": out_anims.as_posix(),
        "outBlend": out_blend.as_posix(),
        "alignment": alignment,
        "rig": rig_report,
        "orientation": orientation,
        "skin": skin,
        "stabilize": stabilize,
        "animations": animations,
        "previews": previews,
        "amplitudes": {
            "walkNeck1": 0.003,
            "walkHead": 0.002,
            "gallopNeck1": 0.005,
            "gallopHead": 0.003,
            "attackStrikeRootY": -0.25,
            "attackJawOpenPeak": 0.48,
            "deathRootAxis": animations["death"].get("rootAxis"),
            "deathFinalRootRotation": animations["death"].get("finalRootRotation"),
        },
    }
    out_blend.with_suffix(".json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    if not orientation["facingOk"]:
        print("WARNING: facingOk is false — inspect mesh/bone Y before installing")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
