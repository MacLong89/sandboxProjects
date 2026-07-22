"""Generate a v5 donor-free wolf variation from the proven scratch-v4 rig.

The skeleton, skin and source animation curves are authored by v4. This script
rebakes those procedural curves through a named motion profile so comparison
variants remain reproducible and documented.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from pathlib import Path


PROFILES = {
    "balanced": {
        "description": (
            "Reference-plus: preserve v4 stride, reduce torso motion 15%, "
            "head/neck 25%, and vertical root bounce 30%."
        ),
        "axial": 0.85,
        "limb": 1.0,
        "head": 0.75,
        "tail": 0.9,
        "root_rotation": 0.9,
        "root_xy": 0.95,
        "root_z": 0.7,
    },
    "grounded": {
        "description": (
            "Stable/grounded: half axial and head motion, 10% shorter limb "
            "arcs, 35% less root travel, and 80% less vertical bounce."
        ),
        "axial": 0.5,
        "limb": 0.9,
        "head": 0.5,
        "tail": 0.6,
        "root_rotation": 0.75,
        "root_xy": 0.65,
        "root_z": 0.2,
    },
    "athletic": {
        "description": (
            "Athletic/predatory: 18% longer limb arcs, 15% more root drive "
            "and tail action, with head/neck still restrained to 70%."
        ),
        "axial": 0.8,
        "limb": 1.18,
        "head": 0.7,
        "tail": 1.15,
        "root_rotation": 0.95,
        "root_xy": 1.15,
        "root_z": 0.85,
    },
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", required=True)
    parser.add_argument("--profile", choices=sorted(PROFILES), required=True)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--out-fbx", required=True)
    parser.add_argument("--out-anims", required=True)
    parser.add_argument("--out-blend", required=True)
    parser.add_argument("--preview-dir", required=True)
    parser.add_argument("--target-z-rotation", type=float, default=90.0)
    parser.add_argument("--target-height", type=float, default=1.15)
    parser.add_argument("--rig-name", default="ScratchWolfV5Armature")
    parser.add_argument(
        "--landmark-profile",
        choices=("wolf", "panther"),
        default="wolf",
        help="Geometry-specific landmark fallback; wolf preserves prior behavior.",
    )
    return parser.parse_args(argv)


def load_v4_module():
    path = Path(__file__).with_name("rig_mesh_from_scratch_v4.py")
    spec = importlib.util.spec_from_file_location("wolf_scratch_v4_for_v5", path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def configure_landmarks(v4, profile_name: str) -> None:
    """Install narrowly scoped landmark fallbacks without changing wolf builds."""

    if profile_name != "panther":
        return

    original_region_mean = v4.region_mean

    def panther_region_mean(
        target, *, side: str | None, y_frac, z_frac, x_min_abs: float = 0.04
    ):
        try:
            return original_region_mean(
                target,
                side=side,
                y_frac=y_frac,
                z_frac=z_frac,
                x_min_abs=x_min_abs,
            )
        except RuntimeError:
            # This panther's long tail curves below the wolf tail-tip band.
            # Broaden only the extreme rear centerline lookup.
            if side is None and y_frac[0] >= 0.9:
                return original_region_mean(
                    target,
                    side=None,
                    y_frac=y_frac,
                    z_frac=(0.05, 0.95),
                    x_min_abs=x_min_abs,
                )
            if side is not None:
                return original_region_mean(
                    target,
                    side=side,
                    y_frac=y_frac,
                    z_frac=(max(0.0, z_frac[0] - 0.15), min(1.0, z_frac[1] + 0.15)),
                    x_min_abs=x_min_abs,
                )
            raise

    v4.region_mean = panther_region_mean


def bone_factor(name: str, profile: dict, action_label: str) -> float:
    if name == "Root":
        # Death relies on a mostly rigid whole-body fall; preserve it.
        return 1.0 if action_label == "Death" else profile["root_rotation"]
    if name.startswith("Spine"):
        return profile["axial"]
    if name in {"Neck1", "Head", "Jaw"}:
        # Attack's intentional head thrust should remain readable.
        return max(profile["head"], 0.9) if action_label == "Attack" else profile["head"]
    if name.startswith("Tail"):
        return profile["tail"]
    if name.startswith(("Front", "Hind")):
        return profile["limb"]
    return 1.0


def scaled_quaternion(quaternion, factor: float):
    from mathutils import Quaternion

    value = quaternion.copy()
    value.normalize()
    if value.w < 0.0:
        value.negate()
    if value.angle < 1.0e-7:
        return Quaternion()
    return Quaternion(value.axis, value.angle * factor)


def bind_panther_nearest_bones(target, rig) -> dict:
    """Deterministic scratch weights when Blender bone heat cannot solve."""

    import bpy

    target.vertex_groups.clear()
    groups = {
        bone.name: target.vertex_groups.new(name=bone.name)
        for bone in rig.data.bones
        if bone.use_deform
    }

    def segment_distance(point, bone) -> float:
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return (point - start).length
        t = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return (point - (start + delta * t)).length

    for vertex in target.data.vertices:
        candidates = []
        for bone in rig.data.bones:
            if not bone.use_deform:
                continue
            if vertex.co.x > 0.025 and bone.name.endswith(".R"):
                continue
            if vertex.co.x < -0.025 and bone.name.endswith(".L"):
                continue
            candidates.append((segment_distance(vertex.co, bone), bone.name))
        nearest = sorted(candidates)[:4]
        raw = [(distance + 0.025) ** -4 for distance, _ in nearest]
        total = sum(raw)
        for weight, (_, name) in zip(raw, nearest):
            groups[name].add([vertex.index], weight / total, "REPLACE")

    target.parent = rig
    modifier = next(
        (item for item in target.modifiers if item.type == "ARMATURE"), None
    )
    if modifier is None:
        modifier = target.modifiers.new(name="ScratchPantherArmature", type="ARMATURE")
    modifier.object = rig
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    return {
        "method": "scratch nearest-bone fallback",
        "weightedVertices": len(target.data.vertices),
        "unweightedVertices": 0,
        "oppositeSideStripped": 0,
        "groups": len(target.vertex_groups),
    }


def rebake_profile(rig, source_actions: list, profile_name: str) -> list:
    """Sample v4 actions, scale semantic motion groups, and bake v5 actions."""

    import bpy

    profile = PROFILES[profile_name]
    scene = bpy.context.scene
    depsgraph = bpy.context.evaluated_depsgraph_get()
    created = []

    for source in source_actions:
        label = source.name.rsplit("|", 1)[-1]
        start, end = (int(round(value)) for value in source.frame_range)
        samples = {}
        if rig.animation_data is None:
            rig.animation_data_create()
        rig.animation_data.action = source
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            depsgraph.update()
            samples[frame] = {
                bone.name: (
                    bone.rotation_quaternion.copy(),
                    bone.location.copy(),
                )
                for bone in rig.pose.bones
            }

        target = bpy.data.actions.new(name=f"ScratchV5_{profile_name}|{label}")
        target.use_fake_user = True
        target.frame_start = start
        target.frame_end = end
        rig.animation_data.action = target
        for frame in range(start, end + 1):
            for bone in rig.pose.bones:
                source_rotation, source_location = samples[frame][bone.name]
                bone.rotation_mode = "QUATERNION"
                bone.rotation_quaternion = scaled_quaternion(
                    source_rotation,
                    bone_factor(bone.name, profile, label),
                )
                if bone.name == "Root":
                    if label == "Death":
                        # Preserve the authored ground-settling fall.
                        bone.location = source_location
                    else:
                        bone.location = (
                            source_location.x * profile["root_xy"],
                            source_location.y * profile["root_xy"],
                            source_location.z * profile["root_z"],
                        )
                else:
                    bone.location = source_location
                bone.keyframe_insert(
                    "rotation_quaternion", frame=frame, group=bone.name
                )
                bone.keyframe_insert("location", frame=frame, group=bone.name)
        created.append(target)

    rig.animation_data.action = None
    for action in source_actions:
        bpy.data.actions.remove(action)
    return created


def export_mesh(path: Path, target, rig, asset_id: str, rig_name: str) -> None:
    import bpy

    target.name = asset_id
    target.data.name = asset_id
    rig.name = rig_name
    rig.data.name = rig_name
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
        axis_forward="-Y",
        axis_up="Z",
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
        axis_forward="-Y",
        axis_up="Z",
        path_mode="AUTO",
    )
    rig.data.pose_position = "REST"


def main() -> int:
    import bpy

    args = parse_args()
    v4 = load_v4_module()
    configure_landmarks(v4, args.landmark_profile)
    base = v4.load_base_module()
    target_path = Path(args.target).resolve()
    out_fbx = Path(args.out_fbx).resolve()
    out_anims = Path(args.out_anims).resolve()
    out_blend = Path(args.out_blend).resolve()
    preview_dir = Path(args.preview_dir).resolve()

    base.clear_scene()
    # Blender 5.2's FBX importer still writes the removed Cycles light
    # cast_shadow property for FBX lights. Restore it only for this process;
    # this does not alter source geometry or the resulting scratch rig.
    probe = bpy.data.lights.new("__fbx_import_probe__", "POINT")
    cycles_settings = type(probe.cycles)
    if not hasattr(cycles_settings, "cast_shadow"):
        cycles_settings.cast_shadow = bpy.props.BoolProperty(default=True)
    bpy.data.lights.remove(probe)
    imported = base.import_fbx(target_path)
    target = max(
        (obj for obj in imported if obj.type == "MESH"),
        key=lambda obj: len(obj.data.vertices),
    )
    target_world = target.matrix_world.copy()
    target.parent = None
    target.matrix_world = target_world
    for modifier in list(target.modifiers):
        if modifier.type == "ARMATURE":
            target.modifiers.remove(modifier)
    target.vertex_groups.clear()
    for obj in list(imported):
        if obj.type == "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)
    # A source FBX may already contain a third-party rig/actions. The v5
    # pipeline consumes geometry only; purge all imported animation data before
    # generating our landmark skeleton and procedural takes.
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    bpy.context.scene.render.fps = 30
    if args.landmark_profile == "panther":
        # The packaged panther FBX carries a 4.831 object scale. v4 assumes
        # unit object scale before assigning its uniform target-height scale,
        # so bake this source transform first. Wolf behavior remains unchanged.
        bpy.ops.object.select_all(action="DESELECT")
        target.select_set(True)
        bpy.context.view_layer.objects.active = target
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    alignment = v4.prepare_target(
        target, args.target_z_rotation, args.target_height
    )
    rig, rig_report = v4.build_scratch_armature(target)
    orientation = v4.orientation_report(rig, target)
    skin = v4.bind_mesh(target, rig)
    if args.landmark_profile == "panther" and skin["unweightedVertices"]:
        skin = bind_panther_nearest_bones(target, rig)
    # Always run after bind / panther fallback so neither path skips isolation.
    stabilize = v4.stabilize_head_neck_weights(target)
    skin["stabilize"] = stabilize

    # Author the five donor-free v4 takes, then derive the requested v5 profile.
    v4.author_idle(rig)
    v4.author_walk(rig)
    v4.author_gallop(rig)
    v4.author_attack(rig)
    v4.author_death(rig)
    source_actions = [
        action for action in bpy.data.actions if action.name.startswith("Scratch|")
    ]
    actions = rebake_profile(rig, source_actions, args.profile)

    previews = base.render_previews(rig, target, rig, preview_dir)
    export_mesh(out_fbx, target, rig, args.asset_id, args.rig_name)
    export_anims(out_anims, rig)
    out_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))

    report = {
        "pipeline": "scratch_v5_profiled_procedural_anims",
        "donorUsed": False,
        "profile": args.profile,
        "profileSettings": PROFILES[args.profile],
        "assetId": args.asset_id,
        "rigName": args.rig_name,
        "landmarkProfile": args.landmark_profile,
        "target": target_path.as_posix(),
        "outFbx": out_fbx.as_posix(),
        "outAnims": out_anims.as_posix(),
        "outBlend": out_blend.as_posix(),
        "alignment": alignment,
        "rig": rig_report,
        "orientation": orientation,
        "skin": skin,
        "stabilize": stabilize,
        "actions": [
            {"name": action.name, "frames": list(action.frame_range)}
            for action in actions
        ],
        "previews": previews,
    }
    out_blend.with_suffix(".json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
