#!/usr/bin/env python3
"""Fit an unrigged mesh to an animated donor and transfer its skin weights."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--donor", required=True, help="Animated, skinned donor FBX")
    parser.add_argument("--target", required=True, help="Unrigged replacement mesh FBX")
    parser.add_argument("--out-fbx", required=True)
    parser.add_argument("--out-blend", required=True)
    parser.add_argument("--preview-dir", required=True)
    parser.add_argument(
        "--weight-method",
        choices=("automatic", "transfer"),
        default="automatic",
        help="Automatic bone heat or nearest-surface donor weight transfer",
    )
    parser.add_argument(
        "--target-z-rotation",
        type=float,
        default=-90.0,
        help="Z rotation mapping target forward to donor (-Y). Tear score must not override facing.",
    )
    return parser.parse_args(argv)


def clear_scene() -> None:
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path: Path) -> list:
    import bpy

    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    return [obj for obj in bpy.data.objects if obj not in before]


def world_bounds(obj) -> tuple:
    from mathutils import Vector

    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(min(corner[i] for corner in corners) for i in range(3))
    maximum = Vector(max(corner[i] for corner in corners) for i in range(3))
    return minimum, maximum


def conform_rear_to_donor(target, donor, strength: float = 0.55, front_y: float = -0.35) -> dict:
    """Pull rear/mid verts toward the donor surface so hip bones sit inside the mesh.

    Keeps the good front half. Donor anim bind pose stays unchanged.
    """

    import bmesh
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree

    bm = bmesh.new()
    bm.from_mesh(donor.data)
    bm.transform(donor.matrix_world)
    bvh = BVHTree.FromBMesh(bm, epsilon=0.0)

    moved = 0
    max_delta = 0.0
    for vertex in target.data.vertices:
        if vertex.co.y < front_y:
            continue
        # Soft ramp so the chest/shoulder transition isn't a hard seam.
        blend = min(max((vertex.co.y - front_y) / 0.7, 0.0), 1.0) * strength
        if blend <= 0.001:
            continue
        location, _normal, _index, distance = bvh.find_nearest(vertex.co)
        if location is None:
            continue
        before = Vector(vertex.co)
        vertex.co = before.lerp(Vector(location), blend)
        max_delta = max(max_delta, (vertex.co - before).length)
        moved += 1

    bm.free()
    target.data.update()
    return {"movedVertices": moved, "maxDelta": max_delta, "strength": strength, "frontY": front_y}


def fit_target_to_donor(target, donor, rotation_degrees: float) -> dict:
    import bpy
    from mathutils import Vector

    target.rotation_euler[2] += math.radians(rotation_degrees)
    bpy.context.view_layer.objects.active = target
    target.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    donor_min, donor_max = world_bounds(donor)
    target_min, target_max = world_bounds(target)
    donor_size = donor_max - donor_min
    target_size = target_max - target_min

    # Fill the donor volume so limb bones sit inside the mesh.
    target.scale = Vector(
        donor_size[i] / target_size[i] if target_size[i] else 1.0 for i in range(3)
    )
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    target_min, target_max = world_bounds(target)
    donor_center = (donor_min + donor_max) * 0.5
    target_center = (target_min + target_max) * 0.5
    target.location.x += donor_center.x - target_center.x
    target.location.y += donor_center.y - target_center.y
    target.location.z += donor_min.z - target_min.z
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    fitted_min, fitted_max = world_bounds(target)
    return {
        "rotationDegrees": rotation_degrees,
        "scaleMode": "anisotropic_aabb",
        "donorBoundsMin": list(donor_min),
        "donorBoundsMax": list(donor_max),
        "fittedBoundsMin": list(fitted_min),
        "fittedBoundsMax": list(fitted_max),
    }


def transfer_skin_weights(target, donor, armature) -> dict:
    import bpy

    for group in donor.vertex_groups:
        target.vertex_groups.new(name=group.name)

    bpy.context.view_layer.objects.active = target
    target.select_set(True)
    modifier = target.modifiers.new(name="DonorWeightTransfer", type="DATA_TRANSFER")
    modifier.object = donor
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.layers_vgroup_select_src = "ALL"
    modifier.layers_vgroup_select_dst = "NAME"
    modifier.mix_mode = "REPLACE"
    modifier.mix_factor = 1.0
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    armature_modifier = target.modifiers.new(name="AnimalArmature", type="ARMATURE")
    armature_modifier.object = armature
    target.parent = armature
    target.matrix_parent_inverse = armature.matrix_world.inverted()

    cleanup = clean_anatomical_extremities(target, armature)
    assign_unweighted_to_nearest_bone(target, armature)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    weighted_vertices = 0
    unweighted_vertices = 0
    for vertex in target.data.vertices:
        if any(group.weight > 0.0001 for group in vertex.groups):
            weighted_vertices += 1
        else:
            unweighted_vertices += 1
    return {
        "method": "transfer",
        "sourceGroups": len(donor.vertex_groups),
        "targetGroups": len(target.vertex_groups),
        "weightedVertices": weighted_vertices,
        "unweightedVertices": unweighted_vertices,
        "cleanup": cleanup,
    }


def calculate_automatic_weights(target, donor, armature) -> dict:
    import bpy

    donor_group_names = {group.name for group in donor.vertex_groups}
    control_tokens = ("IK", "FF.", "FFB.", "PoleTarget", "_end", "Ear")
    force_deform = {
        "Body", "Back", "Torso", "Torso2", "Torso3",
        "Neck1", "Neck2", "Neck3", "Head",
    }
    for bone in armature.data.bones:
        is_control = any(token in bone.name for token in control_tokens)
        bone.use_deform = (
            (bone.name in donor_group_names or bone.name in force_deform)
            and not is_control
        )

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    # More influences + stronger smooth → limbs deform as volumes, not spiked faces.
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=6)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    try:
        bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=0.35, repeat=3, expand=0.0)
    except RuntimeError:
        pass
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    cleanup = clean_anatomical_extremities(target, armature)
    assign_unweighted_to_nearest_bone(target, armature)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    try:
        bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=0.3, repeat=2, expand=0.0)
    except RuntimeError:
        pass
    # Final anti-cross + flank stabilize after smooth.
    side_pass = rebind_hind_legs(target, armature, bind_capsules=False)
    flank_pass = stabilize_torso_flanks(target)
    cleanup = {
        "leg": 0,
        "sideStrip": cleanup.get("sideStrip", 0) + side_pass.get("sideStrip", 0),
        "centerStrip": cleanup.get("centerStrip", 0) + side_pass.get("centerStrip", 0),
        "flank": cleanup.get("flank", 0) + flank_pass.get("flank", 0),
    }
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=5)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    weighted_vertices = 0
    unweighted_vertices = 0
    for vertex in target.data.vertices:
        if any(group.weight > 0.0001 for group in vertex.groups):
            weighted_vertices += 1
        else:
            unweighted_vertices += 1
    return {
        "method": "automatic",
        "sourceGroups": len(donor.vertex_groups),
        "targetGroups": len(target.vertex_groups),
        "weightedVertices": weighted_vertices,
        "unweightedVertices": unweighted_vertices,
        "cleanup": cleanup,
    }


def replace_vertex_weights(target, vertex_index: int, weights: dict[str, float]) -> None:
    for group in target.vertex_groups:
        try:
            group.remove([vertex_index])
        except RuntimeError:
            pass
    for name, weight in weights.items():
        target.vertex_groups[name].add([vertex_index], weight, "REPLACE")


def assign_unweighted_to_nearest_bone(target, armature) -> int:
    from mathutils import Vector

    deform_bones = [bone for bone in armature.data.bones if bone.use_deform]
    if not deform_bones:
        deform_bones = list(armature.data.bones)

    filled = 0
    for vertex in target.data.vertices:
        if any(group.weight > 0.0001 for group in vertex.groups):
            continue
        best_name = None
        best_distance = None
        for bone in deform_bones:
            head = armature.matrix_world @ bone.head_local
            distance = (Vector(vertex.co) - head).length
            if best_distance is None or distance < best_distance:
                best_distance = distance
                best_name = bone.name
        if best_name is None:
            continue
        group = target.vertex_groups.get(best_name) or target.vertex_groups.new(name=best_name)
        group.add([vertex.index], 1.0, "REPLACE")
        filled += 1
    return filled


def strip_groups(target, vertex_index: int, remove_names: set[str]) -> None:
    for name in remove_names:
        group = target.vertex_groups.get(name)
        if group is None:
            continue
        try:
            group.remove([vertex_index])
        except RuntimeError:
            pass


def scale_group_weights(target, vertex_index: int, scale_names: set[str], factor: float) -> None:
    for name in scale_names:
        group = target.vertex_groups.get(name)
        if group is None:
            continue
        try:
            current = group.weight(vertex_index)
        except RuntimeError:
            continue
        group.add([vertex_index], current * factor, "REPLACE")


def swap_side_name(name: str) -> str | None:
    if name.endswith(".L"):
        return name[:-2] + ".R"
    if name.endswith(".R"):
        return name[:-2] + ".L"
    return None


def fix_contralateral_weights(target) -> int:
    """Donor convention: +X is Left (.L). Move wrongly-sided limb weights across."""

    fixed = 0
    for vertex in target.data.vertices:
        x = vertex.co.x
        if abs(x) < 0.05:
            continue
        want_suffix = ".L" if x > 0.0 else ".R"
        wrong_suffix = ".R" if want_suffix == ".L" else ".L"
        owned = []
        for assignment in list(vertex.groups):
            name = target.vertex_groups[assignment.group].name
            if not name.endswith(wrong_suffix):
                continue
            swapped = swap_side_name(name)
            if swapped is None:
                continue
            weight = assignment.weight
            owned.append((name, swapped, weight))
        if not owned:
            continue
        for old_name, new_name, weight in owned:
            old_group = target.vertex_groups.get(old_name)
            if old_group is not None:
                try:
                    old_group.remove([vertex.index])
                except RuntimeError:
                    pass
            new_group = target.vertex_groups.get(new_name) or target.vertex_groups.new(name=new_name)
            try:
                existing = new_group.weight(vertex.index)
            except RuntimeError:
                existing = 0.0
            new_group.add([vertex.index], max(existing, weight), "REPLACE")
        fixed += 1
    return fixed


def redistribute_tail_weights(target) -> int:
    """Map raised-tail verts along Tail1..Tail8 by Y so Tip doesn't steal the whole bush."""

    tail_names = [f"Tail{index}" for index in range(1, 9)]
    if any(target.vertex_groups.get(name) is None for name in tail_names):
        return 0
    fixed = 0
    y_min, y_max = 1.1, 2.95
    for vertex in target.data.vertices:
        x, y, z = vertex.co
        if y < y_min or z < 1.25:
            continue
        factor = min(max((y - y_min) / (y_max - y_min), 0.0), 1.0)
        bone_position = factor * (len(tail_names) - 1)
        lower = int(math.floor(bone_position))
        upper = min(lower + 1, len(tail_names) - 1)
        upper_weight = bone_position - lower
        weights = {tail_names[lower]: 1.0 - upper_weight}
        if upper != lower and upper_weight > 0.0:
            weights[tail_names[upper]] = upper_weight
        replace_vertex_weights(target, vertex.index, weights)
        fixed += 1
    return fixed


def blend_chain_weights(names: list[str], factor: float) -> dict[str, float]:
    """Soft two-bone blend along a named chain. factor in [0, 1]."""

    factor = min(max(factor, 0.0), 1.0)
    position = factor * (len(names) - 1)
    lower = int(math.floor(position))
    upper = min(lower + 1, len(names) - 1)
    upper_weight = position - lower
    weights = {names[lower]: 1.0 - upper_weight}
    if upper != lower and upper_weight > 0.0:
        weights[names[upper]] = upper_weight
    return weights


def ensure_vertex_groups(target, names: list[str]) -> None:
    for name in names:
        if target.vertex_groups.get(name) is None:
            target.vertex_groups.new(name=name)


def soften_axial_deformation(target, body_rigidity: float = 0.85) -> dict:
    """Nearly-rigid torso/neck. Legs and tail still animate; waist stops V-pinching."""

    counts = {"spine": 0, "neck": 0, "tail": 0, "head": 0, "reclaimedLegs": 0}
    spine_names = ["Torso2", "Torso", "Back"]
    neck_names = ["Neck1", "Neck2", "Neck3"]
    tail_names = [f"Tail{index}" for index in range(1, 9)]
    ensure_vertex_groups(target, spine_names + neck_names + tail_names + ["Body", "Head", "Torso3"])

    def dominant_name(vertex) -> str | None:
        best_name = None
        best_weight = -1.0
        for assignment in vertex.groups:
            if assignment.weight > best_weight:
                best_weight = assignment.weight
                best_name = target.vertex_groups[assignment.group].name
        return best_name

    for vertex in target.data.vertices:
        x, y, z = vertex.co
        dominant = dominant_name(vertex)

        # Reclaim center/upper verts heat-glued to hind legs (main V-pinch cause).
        if (
            dominant
            and any(token in dominant for token in ("BackUpperLeg", "BackLeg", "BackShoulder"))
            and abs(x) < 0.28
            and -0.5 <= y <= 1.0
            and z > 0.95
        ):
            replace_vertex_weights(
                target,
                vertex.index,
                {"Body": body_rigidity, "Torso": 0.1, "Back": 0.05},
            )
            counts["reclaimedLegs"] += 1
            counts["spine"] += 1
            continue

        if y < -2.05 and z > 1.4:
            replace_vertex_weights(target, vertex.index, {"Head": 0.7, "Neck3": 0.3})
            counts["head"] += 1
            continue

        if -1.7 <= y < -0.8 and abs(x) < 0.25 and z > 1.1:
            factor = (y - (-1.7)) / (-0.8 - (-1.7))
            weights = blend_chain_weights(neck_names, factor)
            weights["Body"] = body_rigidity
            total = sum(weights.values())
            weights = {name: value / total for name, value in weights.items()}
            replace_vertex_weights(target, vertex.index, weights)
            counts["neck"] += 1
            continue

        if -0.6 <= y <= 0.85 and abs(x) < 0.14 and z > 1.0:
            factor = (y - (-0.6)) / (0.85 - (-0.6))
            weights = blend_chain_weights(spine_names, factor)
            weights["Body"] = body_rigidity
            total = sum(weights.values())
            weights = {name: value / total for name, value in weights.items()}
            replace_vertex_weights(target, vertex.index, weights)
            counts["spine"] += 1
            continue

        if y > 1.2 and z > 1.35:
            factor = min(max((y - 1.2) / (2.95 - 1.2), 0.0), 1.0)
            weights = blend_chain_weights(tail_names, factor)
            replace_vertex_weights(target, vertex.index, weights)
            counts["tail"] += 1
            continue

    return counts


def soften_rear_half(target, armature) -> dict:
    """Keep auto hind-leg volume; strip BackUpperLeg bleed from spine/tail."""

    from mathutils import Vector

    counts = {"spine": 0, "hindLegs": 0, "tail": 0, "reclaimedLegs": 0}
    spine_names = ["Torso2", "Torso", "Back"]
    tail_names = [f"Tail{index}" for index in range(1, 9)]
    ensure_vertex_groups(
        target,
        spine_names
        + tail_names
        + [
            "Body",
            "Torso3",
            "BackLeg.L",
            "BackLeg.R",
            "BackUpperLeg.L",
            "BackUpperLeg.R",
            "BackLowerLeg.L",
            "BackLowerLeg.R",
        ],
    )

    bone_heads: dict[str, Vector] = {}
    for name in (
        "BackUpperLeg.L",
        "BackUpperLeg.R",
        "BackLeg.L",
        "BackLeg.R",
        "BackShoulder.L",
        "BackShoulder.R",
        "Back",
        "Torso",
        "Tail1",
    ):
        bone = armature.data.bones.get(name)
        if bone is not None:
            bone_heads[name] = armature.matrix_world @ bone.head_local

    def dominant_name(vertex) -> str | None:
        best_name = None
        best_weight = -1.0
        for assignment in vertex.groups:
            if assignment.weight > best_weight:
                best_weight = assignment.weight
                best_name = target.vertex_groups[assignment.group].name
        return best_name

    def near_hind_leg(co: Vector, side: str) -> bool:
        head = bone_heads.get(f"BackUpperLeg.{side}")
        if head is None:
            return False
        # Anatomical thigh capsule around the donor bone — not the bushy tail.
        return (
            (co - head).length < 0.65
            and -0.15 <= co.y <= 0.95
            and co.z < 1.55
            and ((side == "L" and co.x > 0.08) or (side == "R" and co.x < -0.08))
        )

    for vertex in target.data.vertices:
        co = Vector(vertex.co)
        x, y, z = co.x, co.y, co.z
        dominant = dominant_name(vertex)

        # Raised rear / bush: must be Tail*, never BackUpperLeg.
        if y > 0.95 and z > 1.05:
            factor = min(max((y - 0.95) / (2.95 - 0.95), 0.0), 1.0)
            weights = blend_chain_weights(tail_names, factor)
            if factor < 0.18:
                weights["Back"] = 0.45 * (1.0 - factor / 0.18)
                total = sum(weights.values())
                weights = {name: value / total for name, value in weights.items()}
            replace_vertex_weights(target, vertex.index, weights)
            counts["tail"] += 1
            continue

        # Dorsal corridor: spine blend (covers V-pinch when heat glued waist to thighs).
        if abs(x) < 0.22 and -0.9 <= y <= 0.95 and z > 1.05:
            glued = dominant is not None and any(
                token in dominant
                for token in ("BackUpperLeg", "BackLeg", "BackShoulder", "FrontUpperLeg")
            )
            if glued or y > -0.2:
                factor = min(max((y - (-0.9)) / (0.95 - (-0.9)), 0.0), 1.0)
                weights = blend_chain_weights(spine_names, factor)
                weights["Body"] = 0.42
                total = sum(weights.values())
                weights = {name: value / total for name, value in weights.items()}
                replace_vertex_weights(target, vertex.index, weights)
                counts["spine"] += 1
                if glued:
                    counts["reclaimedLegs"] += 1
                continue

        # Far / wrong-side BackUpperLeg ownership → nearest sensible bone.
        if dominant and "BackUpperLeg" in dominant:
            side = "L" if dominant.endswith(".L") else "R"
            if not near_hind_leg(co, side):
                if abs(x) < 0.18 and z > 0.9:
                    factor = min(max((y - (-0.5)) / (1.0 - (-0.5)), 0.0), 1.0)
                    weights = blend_chain_weights(spine_names, max(0.0, min(1.0, factor)))
                    weights["Body"] = 0.4
                    total = sum(weights.values())
                    weights = {name: value / total for name, value in weights.items()}
                    replace_vertex_weights(target, vertex.index, weights)
                    counts["reclaimedLegs"] += 1
                    counts["spine"] += 1
                elif z < 0.55:
                    # Keep as lower leg if it's a foot-ish vert on the correct side.
                    other = "L" if side == "R" else "R"
                    use = side if ((side == "L" and x >= 0) or (side == "R" and x < 0)) else other
                    replace_vertex_weights(
                        target,
                        vertex.index,
                        {f"BackLowerLeg.{use}": 1.0},
                    )
                    counts["hindLegs"] += 1
                else:
                    use = "L" if x >= 0.0 else "R"
                    replace_vertex_weights(
                        target,
                        vertex.index,
                        {
                            f"BackUpperLeg.{use}": 0.55,
                            f"BackLeg.{use}": 0.3,
                            "Back": 0.15,
                        },
                    )
                    counts["hindLegs"] += 1

    return counts


def soften_rear_half(target, armature) -> dict:
    """Minimal fix: BackUpperLeg heat-bleed owns the bushy tail — reclaim only that."""

    from mathutils import Vector

    counts = {"spine": 0, "hindLegs": 0, "tail": 0, "reclaimedLegs": 0}
    spine_names = ["Torso2", "Torso", "Back"]
    tail_names = [f"Tail{index}" for index in range(1, 9)]
    ensure_vertex_groups(
        target,
        spine_names + tail_names + ["Body", "BackUpperLeg.L", "BackUpperLeg.R"],
    )

    bone_heads: dict[str, Vector] = {}
    for name in ("BackUpperLeg.L", "BackUpperLeg.R"):
        bone = armature.data.bones.get(name)
        if bone is not None:
            bone_heads[name] = armature.matrix_world @ bone.head_local

    def dominant_name(vertex) -> str | None:
        best_name = None
        best_weight = -1.0
        for assignment in vertex.groups:
            if assignment.weight > best_weight:
                best_weight = assignment.weight
                best_name = target.vertex_groups[assignment.group].name
        return best_name

    for vertex in target.data.vertices:
        co = Vector(vertex.co)
        x, y, z = co.x, co.y, co.z
        dominant = dominant_name(vertex)
        if dominant is None:
            continue

        # Tail bush glued to a thigh bone.
        if "BackUpperLeg" in dominant and y > 1.05 and z > 1.1:
            factor = min(max((y - 1.05) / (2.95 - 1.05), 0.0), 1.0)
            weights = blend_chain_weights(tail_names, factor)
            if factor < 0.2:
                weights["Back"] = 0.4 * (1.0 - factor / 0.2)
                total = sum(weights.values())
                weights = {name: value / total for name, value in weights.items()}
            replace_vertex_weights(target, vertex.index, weights)
            counts["tail"] += 1
            counts["reclaimedLegs"] += 1
            continue

        # Waist ridge glued to thigh → V-pinch.
        if (
            any(token in dominant for token in ("BackUpperLeg", "BackLeg", "BackShoulder"))
            and abs(x) < 0.2
            and -0.7 <= y <= 0.85
            and z > 1.15
        ):
            factor = min(max((y - (-0.7)) / (0.85 - (-0.7)), 0.0), 1.0)
            weights = blend_chain_weights(spine_names, factor)
            weights["Body"] = 0.5
            total = sum(weights.values())
            weights = {name: value / total for name, value in weights.items()}
            replace_vertex_weights(target, vertex.index, weights)
            counts["spine"] += 1
            counts["reclaimedLegs"] += 1
            continue

        # Far from the actual thigh bone but still owned by it.
        if "BackUpperLeg" in dominant:
            side = "L" if dominant.endswith(".L") else "R"
            head = bone_heads.get(f"BackUpperLeg.{side}")
            if head is not None and (co - head).length > 0.85:
                if y > 0.9:
                    factor = min(max((y - 0.9) / 2.0, 0.0), 1.0)
                    weights = blend_chain_weights(tail_names, factor)
                    replace_vertex_weights(target, vertex.index, weights)
                    counts["tail"] += 1
                else:
                    factor = min(max((y + 0.5) / 1.5, 0.0), 1.0)
                    weights = blend_chain_weights(spine_names, factor)
                    weights["Body"] = 0.45
                    total = sum(weights.values())
                    weights = {name: value / total for name, value in weights.items()}
                    replace_vertex_weights(target, vertex.index, weights)
                    counts["spine"] += 1
                counts["reclaimedLegs"] += 1

    return counts


def sample_upper_centerline(target, bands: int = 36) -> list[tuple[float, float]]:
    """Return (y, upper_z) samples along the mesh for spine placement."""

    ys = [vertex.co.y for vertex in target.data.vertices]
    y_min, y_max = min(ys), max(ys)
    samples: list[tuple[float, float]] = []
    for index in range(bands):
        y0 = y_min + (y_max - y_min) * (index + 0.5) / bands
        half = (y_max - y_min) / bands
        zs = sorted(
            vertex.co.z
            for vertex in target.data.vertices
            if abs(vertex.co.y - y0) <= half and abs(vertex.co.x) < 0.28
        )
        if len(zs) < 4:
            continue
        upper = zs[int(len(zs) * 0.78)]
        samples.append((y0, upper))
    return samples


def interpolate_centerline_z(samples: list[tuple[float, float]], y: float) -> float | None:
    if not samples:
        return None
    if y <= samples[0][0]:
        return samples[0][1]
    if y >= samples[-1][0]:
        return samples[-1][1]
    for index in range(len(samples) - 1):
        y0, z0 = samples[index]
        y1, z1 = samples[index + 1]
        if y0 <= y <= y1:
            t = 0.0 if y1 == y0 else (y - y0) / (y1 - y0)
            return z0 + (z1 - z0) * t
    return samples[-1][1]


def fit_skeleton_to_mesh(armature, target) -> dict:
    """Move rear/axial rest bones into the Tripo volume before heat weights.

    Donor anim local rotations stay valid; re-export anim FBX from this armature
    so bind pose matches the skinned mesh.
    """

    import bpy
    from mathutils import Vector

    samples = sample_upper_centerline(target)
    axial_names = [
        "Torso3",
        "Torso2",
        "Torso",
        "Back",
        "Tail1",
        "Tail2",
        "Tail3",
        "Tail4",
        "Tail5",
        "Tail6",
        "Tail7",
        "Tail8",
    ]
    moved = []

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")

    for name in axial_names:
        bone = armature.data.edit_bones.get(name)
        if bone is None:
            continue
        head = bone.head.copy()
        target_z = interpolate_centerline_z(samples, head.y)
        if target_z is None:
            continue
        # Sit just under the upper surface so heat weights see bone inside flesh.
        desired_z = target_z - 0.18
        delta_z = desired_z - head.z
        # Only correct large mismatches — don't disturb a good fit.
        if abs(delta_z) < 0.06:
            continue
        # Soft clamp so we don't yank the chain wildly.
        delta_z = max(-0.55, min(0.55, delta_z * 0.85))
        bone.head.z += delta_z
        bone.tail.z += delta_z
        moved.append({"bone": name, "deltaZ": round(delta_z, 4)})

    # Do not translate hip bones in XY — that changes animation arcs and makes
    # hind legs swing across the midline. Capsule weight rebind handles binding.
    bpy.ops.object.mode_set(mode="OBJECT")
    return {"movedBones": len(moved), "edits": moved[:24], "centerlineSamples": len(samples)}


def export_armature_anims(path, armature) -> None:
    """Export animation takes from the (possibly fitted) armature bind pose."""

    import bpy

    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    armature.data.pose_position = "POSE"
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE"},
        apply_unit_scale=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        path_mode="AUTO",
    )
    armature.data.pose_position = "REST"


def _dist_point_to_segment(point, a, b):
    from mathutils import Vector

    point = Vector(point)
    a = Vector(a)
    b = Vector(b)
    ab = b - a
    length_sq = ab.length_squared
    if length_sq < 1e-10:
        return (point - a).length, 0.0
    t = max(0.0, min(1.0, (point - a).dot(ab) / length_sq))
    return (a + ab * t - point).length, t


def stiffen_front_legs(target, armature) -> dict:
    """Soft-mix front limbs toward Shoulder/Upper/Lower (no hard replace)."""

    from mathutils import Vector

    counts = {"front": 0}
    ensure_vertex_groups(
        target,
        [
            "Body",
            "FrontShoulder.L",
            "FrontShoulder.R",
            "FrontUpperLeg.L",
            "FrontUpperLeg.R",
            "FrontLowerLeg.L",
            "FrontLowerLeg.R",
        ],
    )

    chains: dict[str, list[tuple[str, Vector, Vector]]] = {"L": [], "R": []}
    for side in ("L", "R"):
        for token in ("FrontShoulder", "FrontUpperLeg", "FrontLowerLeg"):
            name = f"{token}.{side}"
            bone = armature.data.bones.get(name)
            if bone is None:
                continue
            chains[side].append(
                (
                    name,
                    armature.matrix_world @ bone.head_local,
                    armature.matrix_world @ bone.tail_local,
                )
            )

    def nearest(co: Vector, side: str):
        best = None
        for name, head, tip in chains[side]:
            dist, t = _dist_point_to_segment(co, head, tip)
            if best is None or dist < best[0]:
                best = (dist, name, t)
        return best

    for vertex in target.data.vertices:
        co = Vector(vertex.co)
        x, y, z = co.x, co.y, co.z
        if y > -0.4 or abs(x) < 0.14:
            continue
        side = "L" if x >= 0.0 else "R"
        if not chains[side]:
            continue
        hit = nearest(co, side)
        if hit is None or hit[0] > 0.4:
            continue

        dist, bone_name, t = hit
        shoulder = f"FrontShoulder.{side}"
        upper = f"FrontUpperLeg.{side}"
        lower = f"FrontLowerLeg.{side}"
        if "Lower" in bone_name or z < 0.9:
            desired = {lower: 0.65, upper: 0.35}
        elif "Upper" in bone_name or z < 1.3:
            desired = {upper: 0.5, lower: 0.25, shoulder: 0.25}
        else:
            desired = {shoulder: 0.4, upper: 0.35, "Body": 0.25}

        # Blend with existing weights so we don't tear (60% keep / 40% desired).
        kept: dict[str, float] = {}
        for assignment in vertex.groups:
            if assignment.weight > 0.001:
                kept[target.vertex_groups[assignment.group].name] = assignment.weight
        if not kept:
            kept = dict(desired)
        else:
            for name in list(kept.keys()):
                kept[name] *= 0.6
            for name, value in desired.items():
                kept[name] = kept.get(name, 0.0) + 0.4 * value
        total = sum(kept.values())
        kept = {name: value / total for name, value in kept.items()}
        replace_vertex_weights(target, vertex.index, kept)
        counts["front"] += 1

    return counts


def stabilize_torso_flanks(target) -> dict:
    """Keep side-torso faces from riding up with shoulder/hind-leg bones."""

    counts = {"flank": 0}
    spine_names = ["Torso3", "Torso2", "Torso", "Back"]
    limb_tokens = (
        "FrontUpperLeg",
        "FrontShoulder",
        "FrontLowerLeg",
        "BackUpperLeg",
        "BackLowerLeg",
        "BackLeg",
        "BackShoulder",
    )
    ensure_vertex_groups(target, spine_names + ["Body"])

    for vertex in target.data.vertices:
        x, y, z = vertex.co
        # Side wall of the ribcage / flank — leave real limb volume alone.
        if abs(x) < 0.18 or z < 0.95 or y < -1.05 or y > 0.75:
            continue
        # Skip deep limb capsules (far outboard + lower).
        if abs(x) > 0.42 and z < 1.15:
            continue

        limb_weight = 0.0
        for assignment in vertex.groups:
            name = target.vertex_groups[assignment.group].name
            if any(token in name for token in limb_tokens):
                limb_weight += assignment.weight
        if limb_weight < 0.08:
            continue

        factor = min(max((y - (-0.95)) / (0.75 - (-0.95)), 0.0), 1.0)
        weights = blend_chain_weights(spine_names, factor)
        # Heavy root so flank faces track the torso volume, not a lifting leg.
        weights["Body"] = 0.7
        total = sum(weights.values())
        weights = {name: value / total for name, value in weights.items()}
        replace_vertex_weights(target, vertex.index, weights)
        counts["flank"] += 1

    return counts


def rebind_hind_legs(target, armature, *, bind_capsules: bool = False) -> dict:
    """Keep heat-weight volume; only strip opposite-side hind-leg bleed (anti-cross)."""

    counts = {"leg": 0, "sideStrip": 0, "centerStrip": 0}
    leg_tokens = ("BackUpperLeg", "BackLowerLeg", "BackLeg", "BackShoulder")
    ensure_vertex_groups(target, ["Back", "Body"])

    for vertex in target.data.vertices:
        x, y = vertex.co.x, vertex.co.y
        if y < 0.05:
            continue

        kept = {}
        stripped = False
        for assignment in vertex.groups:
            name = target.vertex_groups[assignment.group].name
            weight = assignment.weight
            if weight <= 0.001:
                continue
            is_leg = any(token in name for token in leg_tokens)
            if is_leg:
                if abs(x) < 0.12:
                    stripped = True
                    continue
                if name.endswith(".L") and x < -0.02:
                    stripped = True
                    continue
                if name.endswith(".R") and x > 0.02:
                    stripped = True
                    continue
            kept[name] = weight

        if not stripped:
            continue
        if not kept:
            kept = {"Back": 0.55, "Body": 0.45}
            counts["centerStrip"] += 1
        else:
            counts["sideStrip"] += 1
        total = sum(kept.values())
        replace_vertex_weights(
            target,
            vertex.index,
            {name: weight / total for name, weight in kept.items()},
        )

    return counts


def clean_anatomical_extremities(target, armature) -> dict:
    flanks = stabilize_torso_flanks(target)
    sides = rebind_hind_legs(target, armature, bind_capsules=False)
    return {**sides, **flanks}


def orientation_check(target, armature) -> dict:
    """Verify mesh faces the same way as the donor (head toward -Y)."""

    head = armature.data.bones.get("Head")
    tail = armature.data.bones.get("Tail1") or armature.data.bones.get("Tail8")
    ys = [vertex.co.y for vertex in target.data.vertices]
    y_min, y_max = min(ys), max(ys)
    # Front third vs rear third average Z — snout is usually higher than feet.
    front = [v for v in target.data.vertices if v.co.y < y_min + (y_max - y_min) * 0.2]
    rear = [v for v in target.data.vertices if v.co.y > y_max - (y_max - y_min) * 0.2]
    front_z = sum(v.co.z for v in front) / max(len(front), 1)
    rear_z = sum(v.co.z for v in rear) / max(len(rear), 1)
    head_y = float(head.head_local.y) if head else None
    tail_y = float(tail.head_local.y) if tail else None
    # Correct: mesh front (most -Y verts) should be on the head side of the armature.
    facing_ok = True
    if head_y is not None and tail_y is not None:
        facing_ok = (y_min < 0 and head_y < tail_y)
    return {
        "meshYMin": y_min,
        "meshYMax": y_max,
        "frontThirdAvgZ": front_z,
        "rearThirdAvgZ": rear_z,
        "donorHeadY": head_y,
        "donorTailY": tail_y,
        "facingOk": facing_ok,
        "note": "Donor AnimalArmature faces -Y (head). Prefer --target-z-rotation -90 for Tripo FBX.",
    }


def look_at(obj, target) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_preview(target, donor) -> tuple:
    import bpy
    from mathutils import Vector

    donor.hide_render = True
    material = bpy.data.materials.new("RigPreview")
    material.diffuse_color = (0.22, 0.32, 0.48, 1.0)
    target.data.materials.clear()
    target.data.materials.append(material)

    minimum, maximum = world_bounds(target)
    center = (minimum + maximum) * 0.5
    size = maximum - minimum

    bpy.ops.object.camera_add(location=(max(size.y * 1.4, 8.0), center.y, center.z + size.z * 0.15))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = max(size.y * 1.18, size.z * 1.8)
    look_at(camera, center)
    bpy.context.scene.camera = camera

    bpy.ops.object.light_add(type="AREA", location=(4.0, center.y - 3.0, maximum.z + 5.0))
    key = bpy.context.object
    key.data.energy = 1200
    key.data.shape = "DISK"
    key.data.size = 5.0
    look_at(key, center)

    bpy.ops.object.light_add(type="AREA", location=(-3.0, center.y + 2.0, center.z + 2.0))
    fill = bpy.context.object
    fill.data.energy = 700
    fill.data.size = 4.0
    look_at(fill, center)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = scene.world or bpy.data.worlds.new("PreviewWorld")
    scene.world.color = (0.035, 0.035, 0.035)
    return camera, center


def render_previews(armature, target, donor, preview_dir: Path) -> list[str]:
    import bpy

    preview_dir.mkdir(parents=True, exist_ok=True)
    setup_preview(target, donor)
    scene = bpy.context.scene
    rendered = []

    actions = list(bpy.data.actions)
    selected = [None]
    for keyword in ("Idle", "Walk", "Gallop", "Attack", "Death"):
        action = next((candidate for candidate in actions if candidate.name.endswith(keyword)), None)
        if action and action not in selected:
            selected.append(action)

    armature.animation_data_create()
    for action in selected:
        armature.animation_data.action = action
        if action is None:
            armature.data.pose_position = "REST"
            frame = 1
            label = "rest"
        else:
            armature.data.pose_position = "POSE"
            start, end = action.frame_range
            frame = round((start + end) * 0.5)
            label = action.name.rsplit("|", 1)[-1].lower()
        scene.frame_set(frame)
        scene.render.filepath = str(preview_dir / f"{label}.png")
        bpy.ops.render.render(write_still=True)
        rendered.append(scene.render.filepath)

    armature.data.pose_position = "REST"
    scene.frame_set(1)
    return rendered


def export_fbx(path: Path, target, armature) -> None:
    import bpy

    # Unique mesh name for ModelDoc import_filter (avoid Wolf / Wolf.001 collisions).
    target.name = "tripo_wolf"
    target.data.name = "tripo_wolf"
    armature.name = "AnimalArmature"
    armature.data.name = "AnimalArmature"
    armature.data.pose_position = "REST"
    if armature.animation_data:
        armature.animation_data.action = None

    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    # Mesh FBX is skin + bind pose only. Animation takes come from the donor FBX.
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=False,
        path_mode="AUTO",
    )


def main() -> int:
    import bpy

    args = parse_args()
    donor_path = Path(args.donor).resolve()
    target_path = Path(args.target).resolve()
    out_fbx = Path(args.out_fbx).resolve()
    out_blend = Path(args.out_blend).resolve()
    preview_dir = Path(args.preview_dir).resolve()

    clear_scene()
    donor_objects = import_fbx(donor_path)
    donor_meshes = [obj for obj in donor_objects if obj.type == "MESH"]
    armatures = [obj for obj in donor_objects if obj.type == "ARMATURE"]
    if len(donor_meshes) != 1 or len(armatures) != 1:
        raise RuntimeError(
            f"Expected one donor mesh and armature; got {len(donor_meshes)} meshes "
            f"and {len(armatures)} armatures"
        )
    donor = donor_meshes[0]
    armature = armatures[0]
    armature.data.pose_position = "REST"
    bpy.context.scene.frame_set(1)

    target_objects = import_fbx(target_path)
    target_meshes = [obj for obj in target_objects if obj.type == "MESH"]
    if len(target_meshes) != 1:
        raise RuntimeError(f"Expected one target mesh; got {len(target_meshes)}")
    target = target_meshes[0]
    target.name = "tripo_wolf_rigged"
    target.data.name = "tripo_wolf_rigged"

    alignment = fit_target_to_donor(target, donor, args.target_z_rotation)
    skeleton_fit = fit_skeleton_to_mesh(armature, target)
    facing = orientation_check(target, armature)
    if args.weight_method == "automatic":
        weights = calculate_automatic_weights(target, donor, armature)
    else:
        weights = transfer_skin_weights(target, donor, armature)
    previews = render_previews(armature, target, donor, preview_dir)

    export_fbx(out_fbx, target, armature)
    anim_fbx = out_fbx.parent / "wolf_fitted_anims.fbx"
    export_armature_anims(anim_fbx, armature)
    out_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))

    report = {
        "donor": donor_path.as_posix(),
        "target": target_path.as_posix(),
        "outFbx": out_fbx.as_posix(),
        "outAnimFbx": anim_fbx.as_posix(),
        "outBlend": out_blend.as_posix(),
        "actions": [action.name for action in bpy.data.actions],
        "alignment": alignment,
        "skeletonFit": skeleton_fit,
        "orientation": facing,
        "weights": weights,
        "previews": previews,
    }
    report_path = out_blend.with_suffix(".json")
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
