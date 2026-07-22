"""Separate v3 wolf prototype: mesh-fitted Rigify skeleton + retargeted motion.

The Rigify wolf metarig is used as an engine-friendly deform skeleton blueprint.
It is fitted to the Tripo mesh, skinned independently, and receives newly baked
actions from donor-bone rotation deltas. Nothing is written to game Assets.
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
    parser.add_argument("--donor", required=True)
    parser.add_argument("--target", required=True)
    parser.add_argument("--out-fbx", required=True)
    parser.add_argument("--out-anims", required=True)
    parser.add_argument("--out-blend", required=True)
    parser.add_argument("--preview-dir", required=True)
    # Tripo wolf length is on X; +90 puts snout at -Y to match Rigify/donor.
    # -90 leaves snout at +Y while head bones sit at -Y (looks head/tail flipped).
    parser.add_argument("--target-z-rotation", type=float, default=90.0)
    return parser.parse_args(argv)


def load_base_module():
    path = Path(__file__).with_name("rig_mesh_from_donor.py")
    spec = importlib.util.spec_from_file_location("wolf_rig_base_v3", path)
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


def fit_target_for_comparison(target, donor, rotation_degrees: float) -> dict:
    """Use v2's scene scale/facing, but do not reuse any donor bones."""
    import bpy
    from mathutils import Vector

    target.rotation_euler[2] += math.radians(rotation_degrees)
    bpy.context.view_layer.objects.active = target
    target.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    donor_min, donor_max = bounds(donor)
    target_min, target_max = bounds(target)
    donor_size = donor_max - donor_min
    target_size = target_max - target_min
    target.scale = Vector(
        donor_size[i] / target_size[i] if target_size[i] else 1.0 for i in range(3)
    )
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    target_min, target_max = bounds(target)
    donor_center = (donor_min + donor_max) * 0.5
    target_center = (target_min + target_max) * 0.5
    target.location.x += donor_center.x - target_center.x
    target.location.y += donor_center.y - target_center.y
    target.location.z += donor_min.z - target_min.z
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    fitted_min, fitted_max = bounds(target)
    return {
        "rotationDegrees": rotation_degrees,
        "scaleMode": "comparison_aabb_only",
        "boundsMin": list(fitted_min),
        "boundsMax": list(fitted_max),
    }


CORE_BONES = {
    *(f"spine.{index:03d}" for index in range(1, 12)),
    "spine",
    "spine.004",
}
for _side in ("L", "R"):
    CORE_BONES.update(
        {
            f"shoulder.{_side}",
            f"front_thigh.{_side}",
            f"front_shin.{_side}",
            f"front_foot.{_side}",
            f"front_toe.{_side}",
            f"thigh.{_side}",
            f"shin.{_side}",
            f"foot.{_side}",
            f"toe.{_side}",
        }
    )


def create_and_fit_metarig(target) -> tuple[object, dict]:
    import bpy
    from mathutils import Vector

    result = bpy.ops.preferences.addon_enable(module="rigify")
    if "FINISHED" not in result:
        raise RuntimeError(f"Could not enable Rigify: {result}")
    bpy.ops.object.armature_wolf_metarig_add()
    rig = bpy.context.object
    rig.name = "TripoWolfRigifyV3"
    rig.data.name = "TripoWolfRigifyV3"

    target_min, target_max = bounds(target)
    target_size = target_max - target_min
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    editable = rig.data.edit_bones

    points = []
    for bone in editable:
        points.extend((bone.head.copy(), bone.tail.copy()))
    source_min = Vector(min(point[i] for point in points) for i in range(3))
    source_max = Vector(max(point[i] for point in points) for i in range(3))
    source_size = source_max - source_min

    def remap(point):
        normalized = Vector(
            (point[i] - source_min[i]) / source_size[i] if source_size[i] else 0.5
            for i in range(3)
        )
        return target_min + Vector(
            (
                normalized.x * target_size.x * 0.82 + target_size.x * 0.09,
                normalized.y * target_size.y,
                normalized.z * target_size.z,
            )
        )

    original = {
        bone.name: (bone.head.copy(), bone.tail.copy())
        for bone in editable
    }
    for bone in editable:
        head, tail = original[bone.name]
        bone.head = remap(head)
        bone.tail = remap(tail)

    bpy.ops.object.mode_set(mode="OBJECT")
    refine = refine_limb_landmarks(rig, target)
    for bone in rig.data.bones:
        bone.use_deform = bone.name in CORE_BONES
    rig.show_in_front = True
    return rig, {
        "template": "Rigify wolf metarig",
        "templateBones": len(rig.data.bones),
        "deformBones": sum(1 for bone in rig.data.bones if bone.use_deform),
        "limbLandmarks": refine,
    }


def region_mean(target, side: str, y_range, z_range):
    from mathutils import Vector

    sign = 1.0 if side == "L" else -1.0
    selected = [
        vertex.co
        for vertex in target.data.vertices
        if vertex.co.x * sign > 0.055
        and y_range[0] <= vertex.co.y <= y_range[1]
        and z_range[0] <= vertex.co.z <= z_range[1]
    ]
    if not selected:
        raise RuntimeError(
            f"No {side} landmark in y={tuple(y_range)}, z={tuple(z_range)}"
        )
    point = sum(selected, Vector()) / len(selected)
    point.x = sign * max(abs(point.x), 0.1)
    return point, len(selected)


def refine_limb_landmarks(rig, target) -> dict:
    import bpy

    minimum, maximum = bounds(target)
    size = maximum - minimum

    def yr(a, b):
        return (minimum.y + size.y * a, minimum.y + size.y * b)

    def zr(a, b):
        return (minimum.z + size.z * a, minimum.z + size.z * b)

    report = {}
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    bones = rig.data.edit_bones
    for side in ("L", "R"):
        front_top, n_ft = region_mean(target, side, yr(0.08, 0.42), zr(0.64, 0.92))
        front_elbow, n_fe = region_mean(target, side, yr(0.08, 0.46), zr(0.38, 0.65))
        front_paw, n_fp = region_mean(target, side, yr(0.05, 0.46), zr(0.0, 0.19))
        front_wrist = front_elbow.lerp(front_paw, 0.72)

        hind_top, n_ht = region_mean(target, side, yr(0.56, 0.9), zr(0.62, 0.9))
        hind_knee, n_hk = region_mean(target, side, yr(0.52, 0.92), zr(0.39, 0.66))
        hind_paw, n_hp = region_mean(target, side, yr(0.48, 0.94), zr(0.0, 0.2))
        hind_hock = hind_knee.lerp(hind_paw, 0.68)

        shoulder = bones[f"shoulder.{side}"]
        front_thigh = bones[f"front_thigh.{side}"]
        front_shin = bones[f"front_shin.{side}"]
        front_foot = bones[f"front_foot.{side}"]
        front_toe = bones[f"front_toe.{side}"]
        shoulder.tail = front_top
        front_thigh.head, front_thigh.tail = front_top, front_elbow
        front_shin.head, front_shin.tail = front_elbow, front_wrist
        front_foot.head, front_foot.tail = front_wrist, front_paw
        front_toe.head = front_paw
        front_toe.tail = front_paw.copy()
        front_toe.tail.y -= size.y * 0.035

        thigh = bones[f"thigh.{side}"]
        shin = bones[f"shin.{side}"]
        foot = bones[f"foot.{side}"]
        toe = bones[f"toe.{side}"]
        thigh.head, thigh.tail = hind_top, hind_knee
        shin.head, shin.tail = hind_knee, hind_hock
        foot.head, foot.tail = hind_hock, hind_paw
        toe.head = hind_paw
        toe.tail = hind_paw.copy()
        toe.tail.y -= size.y * 0.035

        report[side] = {
            "frontSamples": [n_ft, n_fe, n_fp],
            "hindSamples": [n_ht, n_hk, n_hp],
            "front": [list(front_top), list(front_elbow), list(front_wrist), list(front_paw)],
            "hind": [list(hind_top), list(hind_knee), list(hind_hock), list(hind_paw)],
        }
    bpy.ops.object.mode_set(mode="OBJECT")
    return report


def bind_mesh(target, rig) -> dict:
    import bpy

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=6)
    try:
        bpy.ops.object.vertex_group_smooth(
            group_select_mode="ALL", factor=0.3, repeat=2, expand=0.0
        )
    except RuntimeError:
        pass
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=5)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    weighted = sum(
        1 for vertex in target.data.vertices
        if any(group.weight > 0.0001 for group in vertex.groups)
    )
    return {
        "method": "Rigify metarig heat bind, joint-volume smooth",
        "weightedVertices": weighted,
        "unweightedVertices": len(target.data.vertices) - weighted,
        "groups": len(target.vertex_groups),
    }


RETARGET_MAP = {
    # Rear axial channels: donor lumbar/hips rock hard. On a mesh-fitted spine
    # that reads as a floppy rear — keep locomotion, cut the thrash.
    "Body": ("spine.004", 0.4),
    "Back": ("spine.005", 0.35),
    "Torso": ("spine.006", 0.55),
    "Torso2": ("spine.007", 0.65),
    "Torso3": ("spine.008", 0.75),
    "Neck1": ("spine.009", 0.85),
    "Neck2": ("spine.010", 0.9),
    "Head": ("spine.011", 0.95),
    "Tail1": ("spine.003", 0.45),
    "Tail3": ("spine.002", 0.4),
    "Tail5": ("spine.001", 0.4),
    "Tail8": ("spine", 0.35),
}
for _side in ("L", "R"):
    RETARGET_MAP.update(
        {
            # The donor has a three-bone front leg while Rigify splits paw/foot
            # into four. Damping prevents one donor bend being overexpressed.
            f"FrontShoulder.{_side}": (f"shoulder.{_side}", 0.75),
            f"FrontUpperLeg.{_side}": (f"front_thigh.{_side}", 0.7),
            f"FrontLowerLeg.{_side}": (f"front_shin.{_side}", 0.65),
            # Pelvis inherits Back thrash; keep it quieter than the thigh swing.
            f"BackShoulder.{_side}": (f"pelvis.{_side}", 0.4),
            f"BackLeg.{_side}": (f"thigh.{_side}", 0.8),
            f"BackUpperLeg.{_side}": (f"shin.{_side}", 0.8),
            f"BackLowerLeg.{_side}": (f"foot.{_side}", 0.8),
        }
    )


# Scale donor root translation separately from Body rotation damping.
ROOT_LOCATION_SCALE = 0.35
ROOT_LOCATION_SOURCE = "Body"
ROOT_LOCATION_TARGET = "spine.004"


def retarget_actions(source, target) -> tuple[dict, list]:
    import bpy
    from mathutils import Matrix, Quaternion

    source_actions = list(bpy.data.actions)
    source.animation_data_create()
    target.animation_data_create()
    scene = bpy.context.scene
    depsgraph = bpy.context.evaluated_depsgraph_get()
    created = []

    valid_map = {
        source_name: (target_name, factor)
        for source_name, (target_name, factor) in RETARGET_MAP.items()
        if source.data.bones.get(source_name) and target.data.bones.get(target_name)
    }
    for source_action in source_actions:
        label = source_action.name.rsplit("|", 1)[-1]
        action = bpy.data.actions.new(name=f"RigifyV3|{label}")
        # Actions are deliberately not kept in NLA tracks. Preserve them after
        # animation_data.action is cleared for bind-pose mesh export/save.
        action.use_fake_user = True
        target.animation_data.action = action
        start, end = (int(round(value)) for value in source_action.frame_range)
        source.animation_data.action = source_action

        for frame in range(start, end + 1):
            scene.frame_set(frame)
            depsgraph.update()
            for target_name, _factor in valid_map.values():
                pose = target.pose.bones[target_name]
                pose.rotation_mode = "QUATERNION"
                pose.location = (0.0, 0.0, 0.0)
                pose.rotation_quaternion = Quaternion()
                pose.scale = (1.0, 1.0, 1.0)

            for source_name, (target_name, factor) in valid_map.items():
                source_pose = source.pose.bones[source_name]
                target_pose = target.pose.bones[target_name]
                source_rest = source.data.bones[source_name].matrix_local.to_3x3().normalized()
                target_rest = target.data.bones[target_name].matrix_local.to_3x3().normalized()
                source_local_delta = source_pose.matrix_basis.to_3x3().normalized()
                world_delta = source_rest @ source_local_delta @ source_rest.inverted()
                target_delta = target_rest.inverted() @ world_delta @ target_rest
                rotation = target_delta.to_quaternion()
                if factor < 1.0:
                    rotation = Quaternion().slerp(rotation, factor)
                target_pose.rotation_quaternion = rotation
                if source_name == ROOT_LOCATION_SOURCE and target_name == ROOT_LOCATION_TARGET:
                    target_pose.location = source_pose.location * ROOT_LOCATION_SCALE
                    target_pose.keyframe_insert("location", frame=frame, group=target_name)
                target_pose.keyframe_insert(
                    "rotation_quaternion", frame=frame, group=target_name
                )
        action.frame_start = start
        action.frame_end = end
        created.append(action)

    source.animation_data.action = None
    target.animation_data.action = None
    for action in source_actions:
        bpy.data.actions.remove(action)
    return {
        "mapping": {key: value[0] for key, value in valid_map.items()},
        "damping": {key: value[1] for key, value in valid_map.items()},
        "rootLocationScale": ROOT_LOCATION_SCALE,
        "actions": [
            {"name": action.name, "frames": list(action.frame_range)}
            for action in created
        ],
    }, created


def export_mesh(path: Path, target, rig) -> None:
    import bpy

    target.name = "tripo_wolf_rigify_v3"
    target.data.name = "tripo_wolf_rigify_v3"
    rig.name = "TripoWolfRigifyV3"
    rig.data.name = "TripoWolfRigifyV3"
    rig.data.pose_position = "REST"
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


def remove_source_objects(objects) -> None:
    import bpy

    for obj in objects:
        if obj and obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)


def orientation_report(rig, target) -> dict:
    """Require both bone head and mesh snout on the -Y end."""

    root = rig.data.bones.get("spine.004")
    head = rig.data.bones.get("spine.011")
    ys = [vertex.co.y for vertex in target.data.vertices]
    y_min, y_max = min(ys), max(ys)
    front_band = y_min + (y_max - y_min) * 0.2
    rear_band = y_max - (y_max - y_min) * 0.2
    front_z = sum(v.co.z for v in target.data.vertices if v.co.y <= front_band) / max(
        1, sum(1 for v in target.data.vertices if v.co.y <= front_band)
    )
    rear_z = sum(v.co.z for v in target.data.vertices if v.co.y >= rear_band) / max(
        1, sum(1 for v in target.data.vertices if v.co.y >= rear_band)
    )
    # Snout/crown sits higher than the rump/feet end on this mesh.
    mesh_snout_at_neg_y = front_z > rear_z
    bones_face_neg_y = bool(root and head and head.head_local.y < root.head_local.y)
    facing_ok = bones_face_neg_y and mesh_snout_at_neg_y
    return {
        "facingOk": facing_ok,
        "expected": "-Y",
        "rootY": root.head_local.y if root else None,
        "headY": head.head_local.y if head else None,
        "meshYMin": y_min,
        "meshYMax": y_max,
        "frontThirdAvgZ": front_z,
        "rearThirdAvgZ": rear_z,
        "meshSnoutAtNegY": mesh_snout_at_neg_y,
        "bonesFaceNegY": bones_face_neg_y,
    }


def main() -> int:
    import bpy

    args = parse_args()
    base = load_base_module()
    donor_path = Path(args.donor).resolve()
    target_path = Path(args.target).resolve()
    out_fbx = Path(args.out_fbx).resolve()
    out_anims = Path(args.out_anims).resolve()
    out_blend = Path(args.out_blend).resolve()
    preview_dir = Path(args.preview_dir).resolve()

    base.clear_scene()
    donor_objects = base.import_fbx(donor_path)
    donor_mesh = next(obj for obj in donor_objects if obj.type == "MESH")
    donor_rig = next(obj for obj in donor_objects if obj.type == "ARMATURE")
    target_objects = base.import_fbx(target_path)
    target = max(
        (obj for obj in target_objects if obj.type == "MESH"),
        key=lambda obj: len(obj.data.vertices),
    )

    alignment = fit_target_for_comparison(target, donor_mesh, args.target_z_rotation)
    rig, rig_report = create_and_fit_metarig(target)
    orientation = orientation_report(rig, target)
    skin = bind_mesh(target, rig)
    retarget, _created = retarget_actions(donor_rig, rig)

    previews = base.render_previews(rig, target, donor_mesh, preview_dir)
    remove_source_objects(donor_objects)
    export_mesh(out_fbx, target, rig)
    export_anims(out_anims, rig)
    out_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))

    report = {
        "pipeline": "rigify_wolf_metarig_mesh_fit_v3",
        "prototypeOnly": True,
        "donor": donor_path.as_posix(),
        "target": target_path.as_posix(),
        "outFbx": out_fbx.as_posix(),
        "outAnims": out_anims.as_posix(),
        "outBlend": out_blend.as_posix(),
        "alignment": alignment,
        "rig": rig_report,
        "orientation": orientation,
        "skin": skin,
        "retarget": retarget,
        "previews": previews,
    }
    out_blend.with_suffix(".json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
