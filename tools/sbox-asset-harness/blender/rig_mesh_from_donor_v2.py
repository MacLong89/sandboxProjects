"""Clean v2 creature rig: fit the donor skeleton to mesh landmarks, then heat-bind.

This intentionally avoids the accumulated per-region weight rewrites in
rig_mesh_from_donor.py. Outputs are separate so the known-good rig is untouched.
"""

from __future__ import annotations

import argparse
import heapq
import importlib.util
import json
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
    parser.add_argument("--target-z-rotation", type=float, default=-90.0)
    return parser.parse_args(argv)


def load_base_module():
    path = Path(__file__).with_name("rig_mesh_from_donor.py")
    spec = importlib.util.spec_from_file_location("wolf_rig_base", path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def mean_point(vertices):
    from mathutils import Vector

    return sum((vertex.co for vertex in vertices), Vector((0.0, 0.0, 0.0))) / len(vertices)


def refine_front_leg_topology(target) -> dict:
    """Add one local subdivision pass so elbows have vertices to bend around."""

    import bmesh

    bm = bmesh.new()
    bm.from_mesh(target.data)
    edges = []
    for edge in bm.edges:
        a, b = edge.verts
        midpoint = (a.co + b.co) * 0.5
        same_side = a.co.x * b.co.x > 0.01
        if (
            same_side
            and abs(midpoint.x) > 0.1
            and midpoint.y < -0.35
            and midpoint.z < 1.48
        ):
            edges.append(edge)
    before = len(bm.verts)
    if edges:
        bmesh.ops.subdivide_edges(
            bm,
            edges=edges,
            cuts=1,
            use_grid_fill=True,
            smooth=0.08,
        )
    bm.to_mesh(target.data)
    bm.free()
    target.data.update()
    return {
        "subdividedEdges": len(edges),
        "addedVertices": len(target.data.vertices) - before,
        "totalVertices": len(target.data.vertices),
    }


def landmark(target, *, side: str, y_min: float, y_max: float, z_min: float, z_max: float):
    sign = 1.0 if side == "L" else -1.0
    vertices = [
        vertex
        for vertex in target.data.vertices
        if vertex.co.x * sign > 0.08
        and y_min <= vertex.co.y <= y_max
        and z_min <= vertex.co.z <= z_max
    ]
    if not vertices:
        raise RuntimeError(
            f"No {side} landmark vertices in y=[{y_min}, {y_max}], z=[{z_min}, {z_max}]"
        )
    # Keep the landmark on its own side even when asymmetric fur biases the mean.
    point = mean_point(vertices)
    point.x = sign * max(abs(point.x), 0.12)
    return point, len(vertices)


def fit_limb_chains(armature, target) -> dict:
    """Place each limb chain through the actual mesh, including its paw."""

    import bpy

    report = {}
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")

    for side in ("L", "R"):
        front_shoulder, n_fs = landmark(
            target, side=side, y_min=-1.4, y_max=-0.55, z_min=1.25, z_max=1.58
        )
        front_elbow, n_fe = landmark(
            target, side=side, y_min=-1.25, y_max=-0.35, z_min=0.78, z_max=1.12
        )
        front_foot, n_ff = landmark(
            target, side=side, y_min=-1.25, y_max=-0.20, z_min=-0.05, z_max=0.34
        )

        shoulder = armature.data.edit_bones.get(f"FrontShoulder.{side}")
        upper = armature.data.edit_bones.get(f"FrontUpperLeg.{side}")
        lower = armature.data.edit_bones.get(f"FrontLowerLeg.{side}")
        if not all((shoulder, upper, lower)):
            raise RuntimeError(f"Missing front limb bones for {side}")
        # Keep donor shoulder/elbow orientation so animation arcs remain valid.
        # Only extend the lower bone to the Tripo paw to stop apparent shrinking.
        lower.tail = front_foot

        report[side] = {
            "front": {
                "shoulder": list(front_shoulder),
                "elbow": list(front_elbow),
                "foot": list(front_foot),
                "samples": [n_fs, n_fe, n_ff],
            },
            "hind": "preserved donor rest chain",
        }

    bpy.ops.object.mode_set(mode="OBJECT")
    return report


def automatic_bind(target, donor, armature) -> dict:
    """Plain heat bind with restrained smoothing and side isolation."""

    import bpy

    donor_groups = {group.name for group in donor.vertex_groups}
    force_deform = {
        "Body",
        "Back",
        "Torso",
        "Torso2",
        "Torso3",
        "Neck1",
        "Neck2",
        "Neck3",
        "Head",
    }
    control_tokens = ("IK", "FF.", "FFB.", "PoleTarget", "_end", "Ear")
    for bone in armature.data.bones:
        bone.use_deform = (
            (bone.name in donor_groups or bone.name in force_deform)
            and not any(token in bone.name for token in control_tokens)
        )

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    try:
        bpy.ops.object.vertex_group_smooth(
            group_select_mode="ALL", factor=0.16, repeat=1, expand=0.0
        )
    except RuntimeError:
        pass

    # Strip only opposite-side limb influence. Do not rewrite anatomical regions.
    limb_tokens = ("FrontShoulder", "FrontUpperLeg", "FrontLowerLeg", "BackShoulder",
                   "BackLeg", "BackUpperLeg", "BackLowerLeg")
    stripped = 0
    for vertex in target.data.vertices:
        if abs(vertex.co.x) < 0.04:
            continue
        wrong_suffix = ".R" if vertex.co.x > 0.0 else ".L"
        changed = False
        for assignment in list(vertex.groups):
            name = target.vertex_groups[assignment.group].name
            if name.endswith(wrong_suffix) and any(token in name for token in limb_tokens):
                target.vertex_groups[name].remove([vertex.index])
                changed = True
        if changed:
            stripped += 1

    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    weighted = sum(
        1 for vertex in target.data.vertices if any(group.weight > 0.0001 for group in vertex.groups)
    )
    return {
        "method": "automatic_v2",
        "weightedVertices": weighted,
        "unweightedVertices": len(target.data.vertices) - weighted,
        "oppositeSideStripped": stripped,
    }


def bind_front_legs_geodesic(target, armature) -> dict:
    """Weight only paw-connected front-leg regions; leave chest weights untouched."""

    from mathutils import Vector

    report = {}
    adjacency: list[list[tuple[int, float]]] = [[] for _ in target.data.vertices]
    for edge in target.data.edges:
        a, b = edge.vertices
        length = (target.data.vertices[a].co - target.data.vertices[b].co).length
        adjacency[a].append((b, length))
        adjacency[b].append((a, length))

    def point_segment_distance(point: Vector, a: Vector, b: Vector) -> float:
        ab = b - a
        if ab.length_squared < 1e-10:
            return (point - a).length
        t = max(0.0, min(1.0, (point - a).dot(ab) / ab.length_squared))
        return (point - (a + ab * t)).length

    for side, sign in (("L", 1.0), ("R", -1.0)):
        names = [
            f"FrontShoulder.{side}",
            f"FrontUpperLeg.{side}",
            f"FrontLowerLeg.{side}",
        ]
        for name in names + ["Body"]:
            if target.vertex_groups.get(name) is None:
                target.vertex_groups.new(name=name)

        segments = []
        for name in names:
            bone = armature.data.bones.get(name)
            if bone is None:
                continue
            segments.append(
                (
                    name,
                    armature.matrix_world @ bone.head_local,
                    armature.matrix_world @ bone.tail_local,
                )
            )

        seeds = [
            vertex.index
            for vertex in target.data.vertices
            if vertex.co.x * sign > 0.08
            and vertex.co.y < -0.2
            and vertex.co.z < 0.34
        ]
        distances = {index: 0.0 for index in seeds}
        queue = [(0.0, index) for index in seeds]
        heapq.heapify(queue)
        while queue:
            distance, index = heapq.heappop(queue)
            if distance != distances.get(index) or distance > 1.05:
                continue
            for neighbor, edge_length in adjacency[index]:
                vertex = target.data.vertices[neighbor]
                # Anatomical gate: stay on this side/front and below shoulder height.
                if (
                    vertex.co.x * sign < 0.045
                    or vertex.co.y > -0.18
                    or vertex.co.z > 1.16
                ):
                    continue
                candidate = distance + edge_length
                if candidate < distances.get(neighbor, 1e9) and candidate <= 1.05:
                    distances[neighbor] = candidate
                    heapq.heappush(queue, (candidate, neighbor))

        selected = set(distances)
        for index in selected:
            point = Vector(target.data.vertices[index].co)
            # Gaussian distance to each fitted segment. This naturally blends at joints.
            weights = {}
            for name, head, tip in segments:
                distance = point_segment_distance(point, head, tip)
                weights[name] = pow(2.718281828, -((distance / 0.23) ** 2))
            # Paw-to-elbow only; shoulder/chest are deliberately untouched.
            geodesic = distances[index]
            if point.z > 1.0 or geodesic > 0.9:
                weights["Body"] = 0.12
            total = sum(weights.values())
            if total <= 1e-8:
                continue
            weights = {name: value / total for name, value in weights.items() if value > 0.01}
            total = sum(weights.values())
            weights = {name: value / total for name, value in weights.items()}
            # Reuse the base script's simple replacement contract.
            for group in target.vertex_groups:
                try:
                    group.remove([index])
                except RuntimeError:
                    pass
            for name, weight in weights.items():
                target.vertex_groups[name].add([index], weight, "REPLACE")

        report[side] = {
            "seedVertices": len(seeds),
            "boundVertices": len(selected),
            "maxGeodesic": max(distances.values()) if distances else 0.0,
        }

    return report



def gentle_limb_track_stiffen(
    target,
    armature,
    *,
    radius: float = 0.38,
    z_max: float = 1.05,
    torso_scale: float = 0.3,
) -> dict:
    """Attenuate torso/body weights near limb bones so legs track straighter.

    Scales Body/Back/Torso*/Neck*/Head down (keeps limb weights), then
    normalize + limit only. Does not full-replace weights (that spiked tear).
    """

    import bpy
    from mathutils import Vector

    def is_torso_group(name: str) -> bool:
        if name in ("Body", "Back", "Head"):
            return True
        return name.startswith("Torso") or name.startswith("Neck")

    def point_segment_distance(point: Vector, a: Vector, b: Vector) -> float:
        ab = b - a
        if ab.length_squared < 1e-10:
            return (point - a).length
        t = max(0.0, min(1.0, (point - a).dot(ab) / ab.length_squared))
        return (point - (a + ab * t)).length

    limb_names = []
    for side in ("L", "R"):
        for base in ("FrontUpperLeg", "FrontLowerLeg", "BackUpperLeg", "BackLowerLeg"):
            limb_names.append(f"{base}.{side}")

    capsules = []
    for name in limb_names:
        bone = armature.data.bones.get(name)
        if bone is None:
            continue
        capsules.append(
            (
                name,
                armature.matrix_world @ bone.head_local,
                armature.matrix_world @ bone.tail_local,
            )
        )

    touched = 0
    for vertex in target.data.vertices:
        if vertex.co.z >= z_max:
            continue
        near = any(
            point_segment_distance(vertex.co, head, tail) <= radius
            for _name, head, tail in capsules
        )
        if not near:
            continue

        changed = False
        for assignment in list(vertex.groups):
            group = target.vertex_groups[assignment.group]
            if is_torso_group(group.name) and assignment.weight > 1e-6:
                group.add([vertex.index], assignment.weight * torso_scale, "REPLACE")
                changed = True
        if changed:
            touched += 1

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=5)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    return {
        "touchedVertices": touched,
        "radius": radius,
        "zMax": z_max,
        "torsoScale": torso_scale,
        "capsules": [name for name, _h, _t in capsules],
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
    donor = next(obj for obj in donor_objects if obj.type == "MESH")
    armature = next(obj for obj in donor_objects if obj.type == "ARMATURE")
    armature.data.pose_position = "REST"
    bpy.context.scene.frame_set(1)

    target_objects = base.import_fbx(target_path)
    target = next(obj for obj in target_objects if obj.type == "MESH")
    target.name = "tripo_wolf_v2"
    target.data.name = "tripo_wolf_v2"

    alignment = base.fit_target_to_donor(target, donor, args.target_z_rotation)
    topology = {"mode": "original topology preserved"}
    axial_fit = base.fit_skeleton_to_mesh(armature, target)
    limb_fit = fit_limb_chains(armature, target)
    orientation = base.orientation_check(target, armature)
    # Use the proven rear/flank cleanup from v1 after the clean skeleton fit.
    # v2's plain heat bind fixed front placement but regressed the rear volume.
    weights = base.calculate_automatic_weights(target, donor, armature)
    stiffen = gentle_limb_track_stiffen(target, armature)
    weights["method"] = "automatic_v27_gentle_limb_track"
    weights["frontGeodesic"] = {"mode": "disabled; heat bind around extended lower bone"}
    weights["gentleLimbTrack"] = stiffen
    previews = base.render_previews(armature, target, donor, preview_dir)

    base.export_fbx(out_fbx, target, armature)
    base.export_armature_anims(out_anims, armature)
    out_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))

    report = {
        "pipeline": "clean_v2_landmark_skeleton_fit",
        "donor": donor_path.as_posix(),
        "target": target_path.as_posix(),
        "outFbx": out_fbx.as_posix(),
        "outAnims": out_anims.as_posix(),
        "outBlend": out_blend.as_posix(),
        "alignment": alignment,
        "topology": topology,
        "axialFit": axial_fit,
        "limbFit": limb_fit,
        "orientation": orientation,
        "weights": weights,
        "previews": previews,
    }
    out_blend.with_suffix(".json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
