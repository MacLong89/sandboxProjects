#!/usr/bin/env python3
"""Build a donor-free, species-profiled quadruped rig and procedural actions.

v6 is intentionally separate from v4/v5. Source FBXs contribute geometry,
UV/material appearance, and nothing from their imported armatures, weights,
constraints, or actions. The generated action set is based on real gait classes:
four-beat walks, diagonal trots, family-specific gallops, and anatomy-appropriate
attack/defense poses.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--species", required=True)
    parser.add_argument("--out-fbx", required=True)
    parser.add_argument("--out-anims", required=True)
    parser.add_argument("--out-blend", required=True)
    parser.add_argument("--preview-dir", required=True)
    parser.add_argument("--palette-png")
    return parser.parse_args(argv)


def load_module(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def load_manifest(path: Path, species: str) -> tuple[dict, dict, dict]:
    data = json.loads(path.read_text(encoding="utf-8"))
    build = next((item for item in data["builds"] if item["species"] == species), None)
    if build is None:
        raise ValueError(f"Unknown species profile: {species}")
    family = dict(data["families"][build["family"]])
    family.update(build.get("motionOverrides", {}))
    return data, build, family


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def add_fbx_import_compatibility() -> None:
    import bpy

    probe = bpy.data.lights.new("__fbx_import_probe__", "POINT")
    cycles_settings = type(probe.cycles)
    if not hasattr(cycles_settings, "cast_shadow"):
        cycles_settings.cast_shadow = bpy.props.BoolProperty(default=True)
    bpy.data.lights.remove(probe)


def object_bounds(obj):
    from mathutils import Vector

    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector(min(point[i] for point in points) for i in range(3)),
        Vector(max(point[i] for point in points) for i in range(3)),
    )


def sanitize_geometry(imported, *, mark_head_accessories: bool = False) -> tuple[object, object, dict, list[int]]:
    """Strip all rig data, join render meshes, and retain a body-only proxy."""
    import bpy

    meshes = [obj for obj in imported if obj.type == "MESH"]
    non_meshes = [obj for obj in imported if obj.type != "MESH"]
    imported_armatures = sum(1 for obj in non_meshes if obj.type == "ARMATURE")
    if not meshes:
        raise RuntimeError("Source FBX contains no mesh")

    source_meshes = []
    for obj in meshes:
        world = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = world
        for modifier in list(obj.modifiers):
            if modifier.type == "ARMATURE":
                obj.modifiers.remove(modifier)
        obj.vertex_groups.clear()
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        # Bake every inherited/source transform before our normalization.
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        source_meshes.append(
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "polygons": len(obj.data.polygons),
            }
        )

    body = max(meshes, key=lambda item: len(item.data.vertices))
    accessory_group_name = "__ScratchV6HeadAccessory"
    if mark_head_accessories:
        for obj in meshes:
            if obj == body:
                continue
            group = obj.vertex_groups.new(name=accessory_group_name)
            group.add([vertex.index for vertex in obj.data.vertices], 1.0, "REPLACE")
    proxy = body.copy()
    proxy.data = body.data.copy()
    proxy.name = "__ScratchV6LandmarkProxy__"
    bpy.context.collection.objects.link(proxy)
    proxy.hide_render = True

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = body
    if len(meshes) > 1:
        bpy.ops.object.join()
    target = body
    accessory_group = target.vertex_groups.get(accessory_group_name)
    accessory_indices = []
    if accessory_group is not None:
        group_index = accessory_group.index
        accessory_indices = [
            vertex.index
            for vertex in target.data.vertices
            if any(item.group == group_index and item.weight > 0.5 for item in vertex.groups)
        ]

    for obj in non_meshes:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    return target, proxy, {
        "sourceMeshes": source_meshes,
        "selectedBody": body.name,
        "joinedMeshes": len(meshes),
        "importedArmaturesRemoved": imported_armatures,
        "importedActionsRemaining": len(bpy.data.actions),
        "headAccessoryVertices": len(accessory_indices),
    }, accessory_indices


def rotate_point_z(point, degrees: float):
    angle = math.radians(degrees)
    cosine, sine = math.cos(angle), math.sin(angle)
    return (
        point.x * cosine - point.y * sine,
        point.x * sine + point.y * cosine,
        point.z,
    )


def end_band(points, *, negative: bool, fraction: float = 0.22):
    ys = [point[1] for point in points]
    minimum, maximum = min(ys), max(ys)
    width = max(maximum - minimum, 1.0e-8)
    if negative:
        return [point for point in points if point[1] <= minimum + width * fraction]
    return [point for point in points if point[1] >= maximum - width * fraction]


def cranial_score(points, *, negative: bool) -> tuple[float, dict]:
    """Score how head-like an end is. Prefer elevated bilateral mass (skull/ears/antlers)
    over a thin centerline tip (tail)."""

    chosen = end_band(points, negative=negative)
    if not chosen:
        return -1.0e9, {"samples": 0}
    zs = [point[2] for point in chosen]
    xs = [point[0] for point in chosen]
    z_min = min(point[2] for point in points)
    z_max = max(point[2] for point in points)
    z_span = max(z_max - z_min, 1.0e-8)
    upper = sorted(zs)[len(zs) // 2 :]
    elevation = sum(upper) / len(upper)

    # Bilateral crown/ears: both sides of the top slice should be occupied.
    top_cut = z_min + 0.72 * z_span
    top = [point for point in chosen if point[2] >= top_cut]
    left_top = [point for point in top if point[0] > 0.02]
    right_top = [point for point in top if point[0] < -0.02]
    center_top = [point for point in top if abs(point[0]) <= 0.02]
    bilateral = 0.0
    if left_top and right_top:
        bilateral = (
            0.5
            * (
                sum(point[2] for point in left_top) / len(left_top)
                + sum(point[2] for point in right_top) / len(right_top)
            )
        )
    # Penalize a lone centerline spike (typical tail tip / single horn tip).
    centerline_penalty = 0.0
    if center_top and (not left_top or not right_top):
        centerline_penalty = 0.35 * (sum(point[2] for point in center_top) / len(center_top))

    lateral = max(xs) - min(xs) if xs else 0.0
    score = elevation + 0.55 * bilateral - centerline_penalty + 0.03 * lateral
    details = {
        "samples": len(chosen),
        "elevation": elevation,
        "bilateral": bilateral,
        "centerlinePenalty": centerline_penalty,
        "lateral": lateral,
        "topSamples": len(top),
        "leftTop": len(left_top),
        "rightTop": len(right_top),
    }
    return score, details


def detect_orientation(proxy, *, force_z_rotation: float | None = None) -> dict:
    points = [vertex.co.copy() for vertex in proxy.data.vertices]
    xs = [point.x for point in points]
    ys = [point.y for point in points]
    x_extent = max(xs) - min(xs)
    y_extent = max(ys) - min(ys)
    candidates = (90.0, -90.0) if x_extent > y_extent else (0.0, 180.0)
    reports = []
    for degrees in candidates:
        rotated = [rotate_point_z(point, degrees) for point in points]
        negative_score, negative_details = cranial_score(rotated, negative=True)
        positive_score, positive_details = cranial_score(rotated, negative=False)
        reports.append(
            {
                "rotationDegrees": degrees,
                "negativeEndScore": negative_score,
                "positiveEndScore": positive_score,
                "negativeSamples": negative_details["samples"],
                "positiveSamples": positive_details["samples"],
                "negativeDetails": negative_details,
                "positiveDetails": positive_details,
                "margin": negative_score - positive_score,
            }
        )
    chosen = max(reports, key=lambda item: item["margin"])
    if force_z_rotation is not None:
        forced = next(
            (item for item in reports if abs(item["rotationDegrees"] - force_z_rotation) < 1.0e-6),
            None,
        )
        if forced is None:
            # Allow forcing a rotation even when the long-axis heuristic excluded it.
            rotated = [rotate_point_z(point, force_z_rotation) for point in points]
            negative_score, negative_details = cranial_score(rotated, negative=True)
            positive_score, positive_details = cranial_score(rotated, negative=False)
            forced = {
                "rotationDegrees": float(force_z_rotation),
                "negativeEndScore": negative_score,
                "positiveEndScore": positive_score,
                "negativeSamples": negative_details["samples"],
                "positiveSamples": positive_details["samples"],
                "negativeDetails": negative_details,
                "positiveDetails": positive_details,
                "margin": negative_score - positive_score,
            }
            reports.append(forced)
        chosen = forced
        chosen = dict(chosen)
        chosen["forced"] = True
    scale = max(abs(chosen["negativeEndScore"]), abs(chosen["positiveEndScore"]), 1.0e-6)
    return {
        "rotationDegrees": chosen["rotationDegrees"],
        "headAtNegativeY": chosen["margin"] >= 0.0,
        "confidence": abs(chosen["margin"]) / scale,
        "candidates": reports,
        "sourceHorizontalExtents": {"x": x_extent, "y": y_extent},
        "forced": bool(chosen.get("forced")),
    }


def validate_snout_at_negative_y(proxy) -> dict:
    """Independent post-alignment check that the cranial end is at -Y."""

    points = [vertex.co.copy() for vertex in proxy.data.vertices]
    negative_score, negative_details = cranial_score(points, negative=True)
    positive_score, positive_details = cranial_score(points, negative=False)
    margin = negative_score - positive_score
    return {
        "ok": margin >= 0.0,
        "margin": margin,
        "negativeEndScore": negative_score,
        "positiveEndScore": positive_score,
        "negativeDetails": negative_details,
        "positiveDetails": positive_details,
    }


def apply_yaw_rotation(target, proxy, rotation_degrees: float) -> None:
    import bpy
    from mathutils import Matrix

    if abs(rotation_degrees) < 1.0e-8:
        return
    rotation = Matrix.Rotation(math.radians(rotation_degrees), 4, "Z")
    for obj in (target, proxy):
        obj.matrix_world = rotation @ obj.matrix_world
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def apply_alignment(target, proxy, rotation_degrees: float, target_height: float) -> dict:
    import bpy
    from mathutils import Vector

    apply_yaw_rotation(target, proxy, rotation_degrees)

    minimum, maximum = object_bounds(target)
    height = maximum.z - minimum.z
    scale = target_height / height if height > 1.0e-8 else 1.0
    for obj in (target, proxy):
        obj.scale = (scale, scale, scale)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    minimum, maximum = object_bounds(target)
    center = (minimum + maximum) * 0.5
    for obj in (target, proxy):
        obj.location.x -= center.x
        obj.location.y -= center.y
        obj.location.z -= minimum.z
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    minimum, maximum = object_bounds(target)
    return {
        "rotationDegrees": rotation_degrees,
        "targetHeight": target_height,
        "uniformScale": scale,
        "boundsMin": list(minimum),
        "boundsMax": list(maximum),
    }


def percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    if not ordered:
        return 0.0
    position = max(0.0, min(1.0, fraction)) * (len(ordered) - 1)
    lower = int(math.floor(position))
    upper = min(lower + 1, len(ordered) - 1)
    blend = position - lower
    return ordered[lower] * (1.0 - blend) + ordered[upper] * blend


def robust_proxy_bounds(proxy, family_name: str):
    from mathutils import Vector

    coordinates = [vertex.co for vertex in proxy.data.vertices]
    # Cervid crowns (stag antlers) inflate AABB Z and collapse hock/knee
    # landmarks into the same band — cap nearer the withers, not the tines.
    if family_name.startswith("cervid"):
        z_high = 0.72
    else:
        z_high = 0.98
    return (
        Vector(
            (
                min(point.x for point in coordinates),
                min(point.y for point in coordinates),
                percentile([point.z for point in coordinates], 0.01),
            )
        ),
        Vector(
            (
                max(point.x for point in coordinates),
                max(point.y for point in coordinates),
                percentile([point.z for point in coordinates], z_high),
            )
        ),
    )


def repair_scratch_limb_chains(rig, target, family_name: str) -> dict:
    """Rebuild collapsed limb bones after crown-inflated landmark placement.

    Stag/cervid builds often leave HindLower ~4cm long with the whole shin on
    HindFoot — gait then snaps the lower leg apart and reads as reverse/broken.
    """
    import bpy
    from mathutils import Vector

    if "HindUpper.L" not in rig.data.bones:
        return {"repaired": False, "method": "skipped"}

    zs = [vertex.co.z for vertex in target.data.vertices]
    ground = percentile(zs, 0.02)
    torso = [
        vertex.co
        for vertex in target.data.vertices
        if abs(vertex.co.x) < 0.16 and -0.25 < vertex.co.y < 0.5
    ]
    withers = max((point.z for point in torso), default=percentile(zs, 0.7))
    body_top = withers + 0.04

    def side_cloud(sign: float, y_min: float):
        return [
            vertex.co.copy()
            for vertex in target.data.vertices
            if vertex.co.x * sign > 0.04
            and vertex.co.y >= y_min
            and vertex.co.z <= body_top
        ]

    def mean_near(points, pred, fallback):
        picked = [point for point in points if pred(point)]
        if len(picked) < 3:
            return fallback.copy()
        return sum(picked, Vector()) / len(picked)

    repairs = []
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    edit = rig.data.edit_bones

    for side, sign in (("L", 1.0), ("R", -1.0)):
        hind_points = side_cloud(sign, y_min=0.02)
        front_points = side_cloud(sign, y_min=-1.0)
        front_points = [point for point in front_points if point.y < 0.05]
        if len(hind_points) < 12:
            continue

        # --- Hind: hip → stifle → hock → hoof (descending Z, ungulate stack) ---
        hu = edit.get(f"HindUpper.{side}")
        hl = edit.get(f"HindLower.{side}")
        hf = edit.get(f"HindFoot.{side}")
        if hu and hl and hf:
            hoof = mean_near(
                hind_points,
                lambda p: p.z <= ground + (body_top - ground) * 0.18,
                Vector((sign * 0.12, 0.55, ground + 0.04)),
            )
            hip = mean_near(
                hind_points,
                lambda p: p.z >= body_top - (body_top - ground) * 0.35
                and p.y < hoof.y + 0.05,
                Vector((sign * 0.14, 0.28, body_top - 0.08)),
            )
            # Hock sits above the hoof, usually a bit rearward of the stifle.
            hock = mean_near(
                hind_points,
                lambda p: (body_top - ground) * 0.2
                <= (p.z - ground)
                <= (body_top - ground) * 0.45
                and p.y >= (hip.y + hoof.y) * 0.45,
                hip.lerp(hoof, 0.62),
            )
            knee = mean_near(
                hind_points,
                lambda p: (body_top - ground) * 0.42
                <= (p.z - ground)
                <= (body_top - ground) * 0.7
                and abs(p.y - (hip.y * 0.55 + hock.y * 0.45))
                < max(0.12, abs(hoof.y - hip.y) * 0.35),
                hip.lerp(hock, 0.48),
            )
            for point in (hip, knee, hock, hoof):
                point.x = sign * max(abs(point.x), 0.09)
            # Enforce order / minimum segment lengths.
            if knee.z > hip.z - 0.04:
                knee.z = hip.z - 0.12
            if hock.z > knee.z - 0.06:
                hock.z = knee.z - 0.14
            if hoof.z > hock.z - 0.05:
                hoof.z = max(ground + 0.02, hock.z - 0.16)
            if (knee - hip).length < 0.14:
                knee = hip + (hoof - hip).normalized() * 0.22
                knee.z = min(knee.z, hip.z - 0.1)
            if (hock - knee).length < 0.12:
                hock = knee + (hoof - knee).normalized() * 0.2
                hock.z = min(hock.z, knee.z - 0.1)
            if (hoof - hock).length < 0.1:
                hoof = hock + Vector((0.0, 0.06, -0.14))
                hoof.z = max(ground + 0.02, hoof.z)
            old = (hl.tail - hl.head).length
            hu.head, hu.tail = hip, knee
            hl.head, hl.tail = knee, hock
            hf.head, hf.tail = hock, hoof
            repairs.append(
                {
                    "side": side,
                    "chain": "hind",
                    "oldLowerLen": old,
                    "newLowerLen": (hl.tail - hl.head).length,
                }
            )

        # --- Front: keep shoulder→elbow→wrist→paw descending when collapsed ---
        sh = edit.get(f"FrontShoulder.{side}")
        fu = edit.get(f"FrontUpper.{side}")
        fl = edit.get(f"FrontLower.{side}")
        if sh and fu and fl and front_points:
            lower_len = (fl.tail - fl.head).length
            shoulder_len = (sh.tail - sh.head).length
            upper_len = (fu.tail - fu.head).length
            if (
                lower_len < 0.1
                or shoulder_len < 0.08
                or upper_len < 0.08
                or sh.tail.z >= sh.head.z
            ):
                paw = mean_near(
                    front_points,
                    lambda p: p.z <= ground + (body_top - ground) * 0.16,
                    Vector((sign * 0.11, -0.35, ground + 0.04)),
                )
                shoulder = mean_near(
                    front_points,
                    lambda p: p.z >= body_top - (body_top - ground) * 0.4
                    and p.y < paw.y + 0.15,
                    Vector((sign * 0.12, -0.45, body_top - 0.12)),
                )
                wrist = mean_near(
                    front_points,
                    lambda p: (body_top - ground) * 0.18
                    <= (p.z - ground)
                    <= (body_top - ground) * 0.4,
                    shoulder.lerp(paw, 0.7),
                )
                elbow = mean_near(
                    front_points,
                    lambda p: (body_top - ground) * 0.4
                    <= (p.z - ground)
                    <= (body_top - ground) * 0.7,
                    shoulder.lerp(wrist, 0.5),
                )
                for point in (shoulder, elbow, wrist, paw):
                    point.x = sign * max(abs(point.x), 0.09)
                if elbow.z > shoulder.z - 0.04:
                    elbow.z = shoulder.z - 0.12
                if wrist.z > elbow.z - 0.05:
                    wrist.z = elbow.z - 0.12
                if paw.z > wrist.z - 0.04:
                    paw.z = max(ground + 0.02, wrist.z - 0.12)
                # Enforce a usable Upper segment even when cloud samples collapse.
                if (wrist - elbow).length < 0.08:
                    wrist = elbow.lerp(paw, 0.45)
                    wrist.z = min(wrist.z, elbow.z - 0.08)
                sh.head, sh.tail = shoulder, elbow
                fu.head, fu.tail = elbow, wrist
                fl.head, fl.tail = wrist, paw
                repairs.append(
                    {
                        "side": side,
                        "chain": "front",
                        "rebuilt": True,
                        "upperLen": (fu.tail - fu.head).length,
                    }
                )

    # Lift sunken Spine1–3 onto the dorsal ridge. Antler-inflated builds leave
    # the spine in the gut; withers verts then bind to Front/Hind and erupt in gait.
    s1 = edit.get("Spine1")
    s2 = edit.get("Spine2")
    s3 = edit.get("Spine3")
    neck = edit.get("Neck1")
    spine_lifted = False
    if s1 and s2 and s3:
        dorsum = [
            vertex.co.copy()
            for vertex in target.data.vertices
            if abs(vertex.co.x) < 0.11
            and -0.45 < vertex.co.y < 0.42
            and (withers - (withers - ground) * 0.45) <= vertex.co.z <= body_top
        ]
        if len(dorsum) >= 8:
            ridge_z = min(
                withers - 0.04,
                percentile([point.z for point in dorsum], 0.85),
            )
            hips_y = percentile([point.y for point in dorsum], 0.82)
            mid_y = percentile([point.y for point in dorsum], 0.45)
            withers_y = percentile([point.y for point in dorsum], 0.18)
            chest_y = percentile([point.y for point in dorsum], 0.06)
            hip = Vector((0.0, hips_y, ridge_z - 0.05))
            mid = Vector((0.0, mid_y, ridge_z - 0.01))
            withers_pt = Vector((0.0, withers_y, ridge_z + 0.01))
            chest = Vector((0.0, chest_y, ridge_z - 0.02))
            if neck is not None:
                chest = Vector((0.0, min(chest.y, neck.head.y + 0.02), ridge_z - 0.01))
                neck.head = chest.copy()
            s1.head, s1.tail = hip, mid
            s2.head, s2.tail = mid, withers_pt
            s3.head, s3.tail = withers_pt, chest
            spine_lifted = True
            repairs.append(
                {
                    "chain": "spine",
                    "ridgeZ": ridge_z,
                    "spine1HeadZ": hip.z,
                    "spine3TailZ": chest.z,
                }
            )

    bpy.ops.object.mode_set(mode="OBJECT")
    return {
        "repaired": bool(repairs),
        "repairs": repairs,
        "method": "limb-chain-repair",
        "family": family_name,
        "withersZ": withers,
        "spineLifted": spine_lifted,
    }


def configure_landmark_fallbacks(v4, proxy, family_name: str):
    original_bounds = v4.bounds
    original_region_mean = v4.region_mean

    def profiled_bounds(_obj):
        return robust_proxy_bounds(proxy, family_name)

    v4.bounds = profiled_bounds

    def forgiving_region_mean(target, *, side, y_frac, z_frac, x_min_abs=0.04):
        try:
            point, count = original_region_mean(
                target,
                side=side,
                y_frac=y_frac,
                z_frac=z_frac,
                x_min_abs=x_min_abs,
            )
            if count < 6:
                raise RuntimeError("Landmark sample count below v6 minimum")
            return point, count
        except RuntimeError:
            try:
                if side is None and y_frac[0] >= 0.78:
                    point, count = original_region_mean(
                        target,
                        side=None,
                        y_frac=y_frac,
                        z_frac=(0.02, 0.98),
                        x_min_abs=x_min_abs,
                    )
                else:
                    point, count = original_region_mean(
                        target,
                        side=side,
                        y_frac=y_frac,
                        z_frac=(max(0.0, z_frac[0] - 0.3), min(1.0, z_frac[1] + 0.3)),
                        x_min_abs=max(0.01, x_min_abs * 0.35),
                    )
                if count < 6:
                    raise RuntimeError("Broadened landmark sample count below v6 minimum")
                return point, count
            except RuntimeError:
                # Sparse/irregular low-poly meshes can miss a fractional box
                # entirely. Fall back to the nearest local vertices around the
                # requested anatomical fraction rather than aborting the build.
                from mathutils import Vector

                minimum, maximum = profiled_bounds(target)
                size = maximum - minimum
                target_y = minimum.y + size.y * ((y_frac[0] + y_frac[1]) * 0.5)
                target_z = minimum.z + size.z * ((z_frac[0] + z_frac[1]) * 0.5)
                candidates = []
                for vertex in target.data.vertices:
                    if side == "L" and vertex.co.x <= 0.0:
                        continue
                    if side == "R" and vertex.co.x >= 0.0:
                        continue
                    if side is None and abs(vertex.co.x) > max(size.x * 0.3, 0.03):
                        continue
                    dy = (vertex.co.y - target_y) / max(size.y, 1.0e-6)
                    dz = (vertex.co.z - target_z) / max(size.z, 1.0e-6)
                    candidates.append((dy * dy + dz * dz, vertex.co.copy()))
                if not candidates:
                    raise
                selected = [point for _distance, point in sorted(candidates, key=lambda item: item[0])[:12]]
                point = sum(selected, Vector()) / len(selected)
                if side == "L":
                    point.x = abs(point.x)
                elif side == "R":
                    point.x = -abs(point.x)
                else:
                    point.x = 0.0
                return point, len(selected)

    v4.region_mean = forgiving_region_mean
    return original_bounds, original_region_mean


def create_palette_uv(target, path: Path) -> dict:
    """Flatten low-poly source material colors into a deterministic UV palette."""
    import bpy

    slots = list(target.material_slots)
    colors = []
    names = []
    for index, slot in enumerate(slots):
        material = slot.material
        names.append(material.name if material else f"Material_{index}")
        color = tuple(material.diffuse_color[:4]) if material else (0.5, 0.5, 0.5, 1.0)
        colors.append(color)
    if not colors:
        names = ["Default"]
        colors = [(0.5, 0.5, 0.5, 1.0)]

    width = max(4, len(colors) * 4)
    height = 4
    image = bpy.data.images.new(f"{target.name}_palette", width=width, height=height, alpha=True)
    pixels = []
    for _y in range(height):
        for x in range(width):
            index = min(len(colors) - 1, x // 4)
            pixels.extend(colors[index])
    image.pixels = pixels
    path.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()

    uv_layer = target.data.uv_layers.get("ScratchV6Palette")
    if uv_layer is None:
        uv_layer = target.data.uv_layers.new(name="ScratchV6Palette")
    target.data.uv_layers.active = uv_layer
    for polygon in target.data.polygons:
        palette_index = min(polygon.material_index, len(colors) - 1)
        u = (palette_index * 4.0 + 2.0) / width
        for loop_index in polygon.loop_indices:
            uv_layer.data[loop_index].uv = (u, 0.5)

    target.data.materials.clear()
    material = bpy.data.materials.new(name=f"{target.name}_palette")
    target.data.materials.append(material)
    for polygon in target.data.polygons:
        polygon.material_index = 0
    return {
        "path": path.as_posix(),
        "sourceMaterialNames": names,
        "sourceMaterialColors": [list(color) for color in colors],
        "width": width,
        "height": height,
    }


def bind_nearest_segments(target, rig) -> dict:
    import bpy

    target.vertex_groups.clear()
    groups = {
        bone.name: target.vertex_groups.new(name=bone.name)
        for bone in rig.data.bones
        if bone.use_deform
    }

    def segment_distance(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return (point - (start + delta * amount)).length

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
        raw = [(distance + 0.025) ** -4 for distance, _name in nearest]
        total = sum(raw)
        for weight, (_distance, name) in zip(raw, nearest):
            groups[name].add([vertex.index], weight / total, "REPLACE")

    target.parent = rig
    modifier = target.modifiers.new(name="ScratchV6Armature", type="ARMATURE")
    modifier.object = rig
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    return {
        "method": "scratch nearest-segment fallback",
        "weightedVertices": len(target.data.vertices),
        "unweightedVertices": 0,
        "oppositeSideStripped": 0,
        "groups": len(target.vertex_groups),
    }


def lock_high_front_to_head(target, rig, family_name: str) -> dict:
    """Last-pass lock for antler/horn tips that heat-bind leaves on Spine/Root."""
    if family_name not in {"cervid_light", "cervid_heavy", "bovine"}:
        return {"vertices": 0}
    if "Head" not in rig.data.bones:
        return {"vertices": 0}
    head_bone = rig.data.bones["Head"]
    origin = head_bone.head_local.copy()
    tip = head_bone.tail_local.copy()
    axis = tip - origin
    if axis.length < 1.0e-8:
        return {"vertices": 0}
    axis.normalize()
    # Swept cervid antlers reach well past the skull tip.
    reach = max((tip - origin).length * (6.5 if "cervid" in family_name else 4.5), percentile(
        [v.co.z for v in target.data.vertices], 0.5
    ) * 0.35)

    vertices = target.data.vertices
    ys = [vertex.co.y for vertex in vertices]
    zs = [vertex.co.z for vertex in vertices]
    y_min, y_max = min(ys), max(ys)
    y_span = max(1.0e-6, y_max - y_min)
    front_limit = y_min + y_span * (0.55 if "cervid" in family_name else 0.45)
    head_top = max(float(head_bone.head_local.z), float(head_bone.tail_local.z))
    # Only above the skull tip — body-percentile highs include withers and shred.
    high_z = head_top + max(1.0e-4, (max(zs) - min(zs)) * 0.01)
    front_zs = [vertex.co.z for vertex in vertices if vertex.co.y <= front_limit]
    if front_zs:
        high_z = max(high_z, percentile(front_zs, 0.92))

    deform_names = {bone.name for bone in rig.data.bones if bone.use_deform}
    head_group = target.vertex_groups.get("Head")
    if head_group is None:
        head_group = target.vertex_groups.new(name="Head")

    locked = []
    antler_z = percentile(zs, 0.9 if "cervid" in family_name else 0.94)
    spine = rig.data.bones.get("Spine2") or rig.data.bones.get("Spine1")
    for vertex in vertices:
        offset = vertex.co - origin
        along = offset.dot(axis)
        radial = (offset - axis * along).length
        d_head = _bone_seg_dist(vertex.co, head_bone)
        d_spine = _bone_seg_dist(vertex.co, spine) if spine is not None else 1.0e9
        near_skull = (-reach * 0.25 <= along <= reach) and (radial <= reach * 1.35)
        high_antler = vertex.co.z >= antler_z and (near_skull or d_head <= d_spine * 1.2)
        high_front = vertex.co.y <= front_limit and vertex.co.z >= high_z and (
            near_skull or d_head <= d_spine * 1.15
        )
        in_cone = (
            vertex.co.y <= front_limit
            and (-reach * 0.2 <= along <= reach)
            and (radial <= reach * 1.05)
        )
        # Head-near high mass anywhere — catches swept tips behind the snout.
        # Bovine: skip mid-sagittal highs (hump); only lateral horn cones.
        bovine = family_name == "bovine"
        swept_tip = (
            (not bovine or abs(vertex.co.x) >= 0.08)
            and vertex.co.z >= head_top - 0.04
            and d_head <= d_spine * 1.15
            and radial <= reach * 1.6
            and (not bovine or vertex.co.y <= front_limit)
        )
        if bovine:
            high_antler = high_antler and abs(vertex.co.x) >= 0.08
            high_front = high_front and abs(vertex.co.x) >= 0.06
        if not (high_antler or high_front or in_cone or swept_tip):
            continue
        for assignment in list(vertex.groups):
            group = target.vertex_groups[assignment.group]
            if group.name in deform_names:
                group.remove([vertex.index])
        head_group.add([vertex.index], 1.0, "REPLACE")
        locked.append(vertex.index)
    # Expand through the crown tree so no tip is left on Spine/Root.
    expanded = expand_crown_tree(target, set(locked), rig)
    for index in expanded - set(locked):
        for assignment in list(vertices[index].groups):
            group = target.vertex_groups[assignment.group]
            if group.name in deform_names:
                group.remove([index])
        head_group.add([index], 1.0, "REPLACE")
        locked.append(index)
    return {
        "vertices": len(locked),
        "reach": float(reach),
        "highZ": float(high_z),
        "antlerZ": float(antler_z),
    }


def rigidify_head_accessories(
    target, rig, vertex_indices: list[int], *, expand: bool = True
) -> dict:
    """Keep disconnected antlers/horns rigidly attached to the generated Head."""
    if not vertex_indices:
        return {"vertices": 0, "bone": "Head"}
    deform_names = {bone.name for bone in rig.data.bones if bone.use_deform}
    head = target.vertex_groups.get("Head")
    if head is None:
        head = target.vertex_groups.new(name="Head")
    locked = set(vertex_indices)
    for index in locked:
        vertex = target.data.vertices[index]
        for assignment in list(vertex.groups):
            group = target.vertex_groups[assignment.group]
            if group.name in deform_names:
                group.remove([index])
        head.add([index], 1.0, "REPLACE")

    newly: set[int] = set()
    if expand:
        # Expand one connectivity pass: any crown-tree neighbor of a locked vert
        # must also be 100% Head — soft blends here are what leave tip ribbons.
        expanded = expand_crown_tree(target, locked, rig)
        newly = expanded - locked
        for index in newly:
            for assignment in list(target.data.vertices[index].groups):
                group = target.vertex_groups[assignment.group]
                if group.name in deform_names:
                    group.remove([index])
            head.add([index], 1.0, "REPLACE")
        locked |= newly

    # Soften only the true skull/neck junction below the crown — never antlers.
    adjacency = [set() for _vertex in target.data.vertices]
    for edge in target.data.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    skull_top = (
        max(
            float(rig.data.bones["Head"].head_local.z),
            float(rig.data.bones["Head"].tail_local.z),
        )
        if "Head" in rig.data.bones
        else percentile([vertex.co.z for vertex in target.data.vertices], 0.75)
    )
    border = {
        neighbor
        for index in locked
        for neighbor in adjacency[index]
        if neighbor not in locked
        and target.data.vertices[neighbor].co.z < skull_top - 0.02
        and target.data.vertices[neighbor].co.z >= skull_top - 0.18
    }
    for index in border:
        weights = {}
        for assignment in target.data.vertices[index].groups:
            name = target.vertex_groups[assignment.group].name
            if name in deform_names:
                weights[name] = assignment.weight
        if not weights:
            continue
        for name in list(weights):
            if name in deform_names:
                target.vertex_groups[name].remove([index])
        head_w = max(0.55, weights.get("Head", 0.0))
        remain = 1.0 - head_w
        others = [(n, w) for n, w in weights.items() if n != "Head" and w > 0.02]
        others.sort(key=lambda item: item[1], reverse=True)
        others = others[:2]
        other_total = sum(w for _, w in others) or 1.0
        head.add([index], head_w, "REPLACE")
        for name, weight in others:
            target.vertex_groups[name].add(
                [index], remain * (weight / other_total), "REPLACE"
            )

    custom = target.vertex_groups.get("__ScratchV6HeadAccessory")
    if custom is not None:
        target.vertex_groups.remove(custom)
    return {
        "vertices": len(locked),
        "expandedFromSeeds": len(newly),
        "borderSoftened": len(border),
        "bone": "Head",
        "lockedIndices": sorted(locked),
    }


def _bone_seg_dist(point, bone) -> float:
    start = bone.head_local
    delta = bone.tail_local - start
    length_squared = delta.length_squared
    if length_squared < 1.0e-10:
        return (point - start).length
    amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
    return (point - (start + delta * amount)).length


def expand_crown_tree(target, seeds: set[int], rig) -> set[int]:
    """Flood antler/horn islands from seeds without walking the torso.

    Welded Tripo crowns share edges with the body, so whole-island capture fails.
    Walk crown-like verts and hanging flora; stop at the soft neck column and
    deep torso so shoulders/rump stay off Head.
    """
    if not seeds or rig is None or "Head" not in rig.data.bones:
        return set(seeds)
    vertices = target.data.vertices
    head = rig.data.bones["Head"]
    neck = rig.data.bones.get("Neck1")
    spine = rig.data.bones.get("Spine2") or rig.data.bones.get("Spine1")
    spine1 = rig.data.bones.get("Spine1")
    skull_top = max(float(head.head_local.z), float(head.tail_local.z))
    ys = [vertex.co.y for vertex in vertices]
    y_mid = 0.5 * (min(ys) + max(ys))

    adjacency = [set() for _vertex in vertices]
    for edge in target.data.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)

    selected = set(seeds)
    stack = list(seeds)
    while stack:
        current = stack.pop()
        parent = vertices[current].co
        parent_is_high = parent.z >= skull_top - 0.08
        for neighbor in adjacency[current]:
            if neighbor in selected:
                continue
            point = vertices[neighbor].co
            d_head = _bone_seg_dist(point, head)
            d_spine = _bone_seg_dist(point, spine) if spine is not None else 1.0e9
            d_neck = _bone_seg_dist(point, neck) if neck is not None else 1.0e9
            # Soft neck column stays off Head — reclaimed later.
            if (
                neck is not None
                and point.z < skull_top - 0.06
                and d_neck + 0.01 < d_head
                and d_neck <= d_spine * 1.05
            ):
                continue
            above_skull = point.z >= skull_top - 0.1
            near_head = d_head <= d_spine * 1.35 and point.z >= skull_top - 0.35
            # Long purple hangers / draped tines off an already-selected branch.
            # Keep the drop shallow — 0.75 flooded Tripo ram chests onto Head.
            hanging = parent_is_high and point.z >= skull_top - 0.32
            if not (above_skull or near_head or hanging):
                continue
            # Never walk crown paint onto the brisket / front chest.
            if (
                point.z < skull_top - 0.28
                and abs(point.x) < 0.22
                and point.y > float(head.head_local.y) + 0.08
            ):
                continue
            # Block rump / back flora islands (not drapes from the crown).
            if (
                not hanging
                and point.y > y_mid
                and d_spine < d_head * 0.85
                and point.z < skull_top + 0.08
            ):
                if spine1 is not None and _bone_seg_dist(point, spine1) < d_head * 0.9:
                    continue
            selected.add(neighbor)
            stack.append(neighbor)
    return selected


def seal_crown_appendages(target, locked: set[int], rig) -> dict:
    """Pull antler flora ribbons off Hind/Spine/Root onto Head.

    Bloom deer/elk drape purple strands from antlers onto the back; heat-bind
    pins the back end to Hind/Spine while the antler end is on Head → ribbons.
    """
    if not locked or rig is None or "Head" not in rig.data.bones:
        return {"absorbed": 0, "lockedIndices": sorted(locked)}
    vertices = target.data.vertices
    head = rig.data.bones["Head"]
    neck = rig.data.bones.get("Neck1")
    spine = rig.data.bones.get("Spine2") or rig.data.bones.get("Spine1")
    skull_top = max(float(head.head_local.z), float(head.tail_local.z))
    deform_names = {bone.name for bone in rig.data.bones if bone.use_deform}
    head_group = target.vertex_groups.get("Head") or target.vertex_groups.new(name="Head")
    adjacency = [set() for _vertex in vertices]
    for edge in target.data.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)

    bad_prefixes = ("Hind", "Front", "Tail")
    bad_exact = {"Spine1", "Spine2", "Root"}

    def dominant(index: int) -> str:
        best_name, best_w = "", -1.0
        for assignment in vertices[index].groups:
            name = target.vertex_groups[assignment.group].name
            if name in deform_names and assignment.weight > best_w:
                best_name, best_w = name, assignment.weight
        return best_name

    def assign_head(index: int) -> None:
        for assignment in list(vertices[index].groups):
            group = target.vertex_groups[assignment.group]
            if group.name in deform_names:
                group.remove([index])
        head_group.add([index], 1.0, "REPLACE")

    absorbed = 0
    depth = {index: 0 for index in locked}
    stack = list(locked)
    max_hops = 48
    while stack:
        current = stack.pop()
        if depth[current] >= max_hops:
            continue
        for neighbor in adjacency[current]:
            if neighbor in locked:
                continue
            point = vertices[neighbor].co
            d_head = _bone_seg_dist(point, head)
            d_spine = _bone_seg_dist(point, spine) if spine is not None else 1.0e9
            d_neck = _bone_seg_dist(point, neck) if neck is not None else 1.0e9
            # Leave the soft neck column alone.
            if (
                neck is not None
                and point.z < skull_top - 0.05
                and d_neck + 0.02 < d_head
                and d_neck <= d_spine * 1.1
            ):
                continue
            dom = dominant(neighbor)
            misowned = dom.startswith(bad_prefixes) or dom in bad_exact
            # Also catch flora that only has a secondary Hind/Spine weight.
            hind_w = 0.0
            for assignment in vertices[neighbor].groups:
                name = target.vertex_groups[assignment.group].name
                if name.startswith(("Hind", "Front", "Tail")) or name in bad_exact:
                    hind_w = max(hind_w, assignment.weight)
            # Only misweighted appendages / high Spine3 flora — never flood torso.
            high_flora = (
                (dom == "Spine3" or hind_w >= 0.25)
                and point.z >= skull_top - 0.35
                and d_head <= d_spine * 1.3
            )
            draped = (
                (misowned or hind_w >= 0.3)
                and point.z >= skull_top - 0.85
                and d_head <= d_spine * 1.7
            )
            if not (high_flora or draped):
                continue
            locked.add(neighbor)
            assign_head(neighbor)
            depth[neighbor] = depth[current] + 1
            absorbed += 1
            stack.append(neighbor)

    return {"absorbed": absorbed, "lockedIndices": sorted(locked)}


def detect_head_crown_vertices(target, family_name: str, rig=None) -> list[int]:
    """Find the full antler/horn tree and keep it rigid on Head."""
    if family_name not in {"cervid_light", "cervid_heavy", "bovine"}:
        return []
    vertices = target.data.vertices
    if len(vertices) == 0:
        return []
    ys = [vertex.co.y for vertex in vertices]
    zs = [vertex.co.z for vertex in vertices]
    y_min, y_max = min(ys), max(ys)
    z_min, z_max = min(zs), max(zs)
    y_span = max(1.0e-6, y_max - y_min)
    z_span = max(1.0e-6, z_max - z_min)
    front_limit = y_min + y_span * 0.42
    tip_z = percentile(zs, 0.93 if "cervid" in family_name else 0.94)
    crown_z = max(tip_z - z_span * 0.04, percentile(zs, 0.9))
    seeds = {
        vertex.index
        for vertex in vertices
        if vertex.co.y <= front_limit and vertex.co.z >= crown_z
    }
    if rig is not None and "Head" in rig.data.bones:
        head_bone = rig.data.bones["Head"]
        head_top = max(float(head_bone.head_local.z), float(head_bone.tail_local.z))
        spine = rig.data.bones.get("Spine2") or rig.data.bones.get("Spine1")
        # Seed every tip above the skull that is nearer Head than mid-spine —
        # catches swept tines that sit behind the snout in Y.
        # Bovine: only lateral horn seeds — mid-sagittal highs are the hump.
        bovine = family_name == "bovine"
        for vertex in vertices:
            if vertex.co.z < head_top + z_span * 0.004:
                continue
            if bovine and abs(vertex.co.x) < 0.08:
                continue
            d_head = _bone_seg_dist(vertex.co, head_bone)
            d_spine = (
                _bone_seg_dist(vertex.co, spine) if spine is not None else 1.0e9
            )
            if d_head <= d_spine * 1.25 or (vertex.co.y <= front_limit and not bovine):
                seeds.add(vertex.index)
            elif bovine and vertex.co.y <= front_limit and abs(vertex.co.x) >= 0.08:
                seeds.add(vertex.index)
    if not seeds:
        return []

    # Separate (stag) antler meshes: take whole small islands that touch seeds.
    adjacency = [set() for _vertex in vertices]
    for edge in target.data.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    remaining = set(range(len(vertices)))
    selected = set()
    max_island = max(16, int(len(vertices) * 0.2))
    body_z_ref = percentile(zs, 0.7)
    while remaining:
        start = remaining.pop()
        component = {start}
        stack = [start]
        while stack:
            current = stack.pop()
            neighbors = adjacency[current] & remaining
            remaining.difference_update(neighbors)
            component.update(neighbors)
            stack.extend(neighbors)
        if component & seeds and len(component) <= max_island:
            above = sum(1 for index in component if vertices[index].co.z >= body_z_ref)
            if above >= max(1, int(len(component) * 0.5)):
                selected.update(component)

    # Welded crowns: height/Head-gated flood from seeds (covers full tine trees).
    selected |= expand_crown_tree(target, seeds, rig)

    # Drop soft neck column only — never cull high crown tines by Neck1 distance
    # (swept antlers sit nearer Neck1 in 3D while still belonging on Head).
    if rig is not None and "Neck1" in rig.data.bones and "Head" in rig.data.bones:
        neck_bone = rig.data.bones["Neck1"]
        head_bone = rig.data.bones["Head"]
        skull_top = max(float(head_bone.head_local.z), float(head_bone.tail_local.z))
        selected = {
            index
            for index in selected
            if vertices[index].co.z >= skull_top - 0.06
            or _bone_seg_dist(vertices[index].co, head_bone)
            <= _bone_seg_dist(vertices[index].co, neck_bone) * 1.15
        }
    # Safety cap: keep Head-near verts if the flood grew too large.
    cap_frac = 0.28 if "cervid" in family_name else 0.22
    if len(selected) > int(len(vertices) * cap_frac) and rig is not None:
        head_bone = rig.data.bones.get("Head")
        spine = rig.data.bones.get("Spine2") or rig.data.bones.get("Spine1")
        if head_bone is not None:
            ranked = sorted(
                selected,
                key=lambda index: (
                    _bone_seg_dist(vertices[index].co, head_bone)
                    - (
                        0.35
                        * _bone_seg_dist(vertices[index].co, spine)
                        if spine is not None
                        else 0.0
                    )
                    - 0.15 * vertices[index].co.z
                ),
            )
            selected = set(ranked[: int(len(vertices) * cap_frac)])
    return sorted(selected)


def detect_rigid_herbivore_head(target, family_name: str, rig=None) -> list[int]:
    """Rigidify skull tip only; leave the neck column soft for charge tips.

    AABB-front picks fail on lowered cervid heads: the hanging neck sits in the
    front band and gets pinned to Head, then rubber-bands on tip.
    """
    if family_name not in {"cervid_light", "cervid_heavy", "bovine"}:
        return []
    vertices = target.data.vertices
    if rig is None or "Head" not in getattr(rig.data, "bones", {}):
        ys = [vertex.co.y for vertex in vertices]
        zs = [vertex.co.z for vertex in vertices]
        front_limit = min(ys) + (max(ys) - min(ys)) * 0.12
        skull_cutoff = percentile(zs, 0.72)
        return [
            vertex.index
            for vertex in vertices
            if vertex.co.y <= front_limit and vertex.co.z >= skull_cutoff
        ]
    head = rig.data.bones["Head"]
    origin = head.head_local
    tip = head.tail_local
    axis = tip - origin
    length = max(axis.length, 1.0e-6)
    axis_n = axis / length
    radius = length * (1.05 if "cervid" in family_name else 1.25)
    selected = []
    for vertex in vertices:
        offset = vertex.co - origin
        along = offset.dot(axis_n)
        radial = (offset - axis_n * along).length
        # Skull pad only — never the Neck1 column behind the joint.
        if -length * 0.15 <= along <= length * 1.15 and radial <= radius:
            selected.append(vertex.index)
    return selected


def stabilize_hind_limb_weights(target, rig) -> dict:
    """Rebind hind column to Upper→Lower→Foot so gait cannot tear the cannon/hoof."""
    import bpy

    if "HindUpper.L" not in rig.data.bones:
        return {"stabilizedVertices": 0, "method": "skipped"}

    def seg_proj(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return 0.0, (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return amount, (point - (start + delta * amount)).length

    stabilized = 0
    per_side = {}
    for side in ("L", "R"):
        chain = [
            ("HindUpper", rig.data.bones.get(f"HindUpper.{side}")),
            ("HindLower", rig.data.bones.get(f"HindLower.{side}")),
            ("HindFoot", rig.data.bones.get(f"HindFoot.{side}")),
        ]
        if any(bone is None for _name, bone in chain):
            continue
        names = [f"{name}.{side}" for name, _bone in chain]
        groups = {
            name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
            for name in names
        }
        reach = max(
            (chain[0][1].tail_local - chain[0][1].head_local).length * 1.1,
            (chain[2][1].tail_local - chain[0][1].head_local).length * 0.55,
            0.22,
        )
        hip_top = float(chain[0][1].head_local.z) + 0.02
        hoof_z = float(chain[2][1].tail_local.z) + 0.08
        # Facing -Y: hind limbs live at higher Y than the chest.
        limb_front_y = min(
            float(bone.head_local.y) for _name, bone in chain
        ) - 0.08
        spine_bones = [
            bone
            for name in ("Spine1", "Spine2", "Spine3", "Tail1", "Tail2")
            if (bone := rig.data.bones.get(name)) is not None
        ]
        dorsum_z = max(
            (float(bone.tail_local.z) for bone in spine_bones if bone.name.startswith("Spine")),
            default=hip_top,
        ) - 0.04
        count = 0
        for vertex in target.data.vertices:
            if side == "L" and vertex.co.x < -0.02:
                continue
            if side == "R" and vertex.co.x > 0.02:
                continue
            if vertex.co.z > hip_top:
                continue
            if vertex.co.y < limb_front_y:
                continue
            # Never paint the dorsal ridge onto the hind limb.
            if vertex.co.z >= dorsum_z and abs(vertex.co.x) < 0.14:
                continue
            dists = {
                f"{name}.{side}": seg_proj(vertex.co, bone) for name, bone in chain
            }
            nearest_name, (_t, nearest_d) = min(dists.items(), key=lambda item: item[1][1])
            foot_t, foot_d = dists[names[2]]
            is_hoof = vertex.co.z <= hoof_z and foot_t >= 0.5 and foot_d <= 0.14
            if nearest_d > reach and not is_hoof:
                continue
            if spine_bones and not is_hoof:
                spine_d = min(seg_proj(vertex.co, bone)[1] for bone in spine_bones)
                if spine_d + 0.02 < nearest_d:
                    continue
            for assignment in list(vertex.groups):
                gname = target.vertex_groups[assignment.group].name
                if gname.startswith("Hind") and gname.endswith(f".{side}"):
                    target.vertex_groups[gname].remove([vertex.index])
                elif gname in {
                    "Spine1",
                    "Spine2",
                    "Spine3",
                    "Tail1",
                    "Tail2",
                    "Tail3",
                    "Tail4",
                    "Root",
                }:
                    target.vertex_groups[gname].remove([vertex.index])
            upper_t, upper_d = dists[names[0]]
            lower_t, lower_d = dists[names[1]]
            weights = {names[0]: 0.0, names[1]: 0.0, names[2]: 0.0}
            if is_hoof or (foot_t >= 0.55 and foot_d <= lower_d and vertex.co.z <= hoof_z + 0.05):
                weights[names[2]] = 0.9
                weights[names[1]] = 0.1
            elif nearest_name == names[0] or upper_d <= lower_d * 0.9:
                tip_bleed = 0.18 * (upper_t ** 2)
                weights[names[0]] = 1.0 - tip_bleed
                weights[names[1]] = tip_bleed
            elif nearest_name == names[1] or lower_d <= foot_d:
                base = 0.22 * ((1.0 - lower_t) ** 2)
                tip = 0.2 * (lower_t ** 2)
                weights[names[1]] = 1.0 - base - tip
                weights[names[0]] = base
                weights[names[2]] = tip
            else:
                base = 0.2 * ((1.0 - foot_t) ** 2)
                weights[names[2]] = 1.0 - base
                weights[names[1]] = base
            ranked = sorted(
                ((name, weight) for name, weight in weights.items() if weight > 0.02),
                key=lambda item: item[1],
                reverse=True,
            )
            total = sum(weight for _, weight in ranked) or 1.0
            for name, weight in ranked:
                groups[name].add([vertex.index], weight / total, "REPLACE")
            count += 1
        per_side[side] = count
        stabilized += count

    bpy.context.view_layer.objects.active = target
    bpy.ops.object.mode_set(mode="OBJECT")
    for side in ("L", "R"):
        for bone_name in (
            f"HindUpper.{side}",
            f"HindLower.{side}",
            f"HindFoot.{side}",
        ):
            group = target.vertex_groups.get(bone_name)
            if group is None:
                continue
            target.vertex_groups.active_index = group.index
            try:
                bpy.ops.object.vertex_group_smooth(
                    group_select_mode="ACTIVE", factor=0.1, repeat=1, expand=0.0
                )
            except RuntimeError:
                pass
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    return {
        "stabilizedVertices": stabilized,
        "perSide": per_side,
        "method": "hind-limb-chain",
    }


def stabilize_front_limb_weights(target, rig) -> dict:
    """Rebind forelimb column to Shoulder→Upper→Lower so attacks hinge at the shoulder.

    Heat/nearest binds often dump paw verts onto Spine3 (chest), so a shoulder
    swing leaves the feet behind and stretches them off the legs.
    """
    import bpy

    if "FrontShoulder.L" not in rig.data.bones:
        return {"stabilizedVertices": 0, "method": "skipped"}

    def seg_proj(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return 0.0, (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return amount, (point - (start + delta * amount)).length

    stabilized = 0
    tip_reclaimed = 0
    per_side = {}
    for side in ("L", "R"):
        chain = [
            ("FrontShoulder", rig.data.bones.get(f"FrontShoulder.{side}")),
            ("FrontUpper", rig.data.bones.get(f"FrontUpper.{side}")),
            ("FrontLower", rig.data.bones.get(f"FrontLower.{side}")),
        ]
        if any(bone is None for _name, bone in chain):
            continue
        names = [f"{name}.{side}" for name, _bone in chain]
        groups = {
            name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
            for name in names
        }
        reach = max(
            (chain[0][1].tail_local - chain[0][1].head_local).length * 1.05,
            (chain[2][1].tail_local - chain[0][1].head_local).length * 0.55,
            0.22,
        )
        spine_bones = [
            bone
            for name in ("Spine1", "Spine2", "Spine3")
            if (bone := rig.data.bones.get(name)) is not None
        ]
        shoulder_top = float(chain[0][1].head_local.z) + 0.02
        paw_z = float(chain[2][1].tail_local.z) + 0.07
        # Facing -Y: higher Y is toward the belly/hips. Keep limb paint forward
        # of the forelimb column so the brisket cannot ride FrontShoulder.
        limb_rear_y = max(
            max(float(bone.head_local.y), float(bone.tail_local.y))
            for _name, bone in chain
        )
        limb_x = float(chain[0][1].head_local.x)
        dorsum_z = max(
            (
                float(bone.tail_local.z)
                for bone in spine_bones
                if bone.name.startswith("Spine")
            ),
            default=shoulder_top,
        ) - 0.04
        count = 0
        for vertex in target.data.vertices:
            if side == "L" and vertex.co.x < -0.02:
                continue
            if side == "R" and vertex.co.x > 0.02:
                continue
            if vertex.co.z > shoulder_top:
                continue
            dists = {
                f"{name}.{side}": seg_proj(vertex.co, bone) for name, bone in chain
            }
            nearest_name, (_t, nearest_d) = min(dists.items(), key=lambda item: item[1][1])
            lower_t, lower_d = dists[names[2]]
            is_paw_tip = vertex.co.z <= paw_z and lower_t >= 0.55 and lower_d <= 0.12
            if nearest_d > reach and not is_paw_tip:
                continue
            # Skip chest/ribcage: nearer any thoracic spine than the limb, or
            # clearly caudal of the forelimb column (unless a real paw tip).
            if not is_paw_tip:
                if vertex.co.y > limb_rear_y + 0.1:
                    continue
                # Withers / back plate must stay on Spine — not FrontUpper.
                if vertex.co.z >= dorsum_z and abs(vertex.co.x) < 0.14:
                    continue
                if abs(vertex.co.x) < abs(limb_x) * 0.45 and vertex.co.z > paw_z + 0.08:
                    continue
                if spine_bones:
                    spine_d = min(seg_proj(vertex.co, bone)[1] for bone in spine_bones)
                    if spine_d + 0.015 < nearest_d:
                        continue
            for assignment in list(vertex.groups):
                gname = target.vertex_groups[assignment.group].name
                if gname.startswith("Front") and gname.endswith(f".{side}"):
                    target.vertex_groups[gname].remove([vertex.index])
                elif gname in {"Spine2", "Spine3", "Spine1", "Neck1", "Root"}:
                    target.vertex_groups[gname].remove([vertex.index])
            shoulder_t, shoulder_d = dists[names[0]]
            upper_t, upper_d = dists[names[1]]
            weights = {names[0]: 0.0, names[1]: 0.0, names[2]: 0.0}
            if is_paw_tip or (
                lower_t >= 0.6 and lower_d <= upper_d and vertex.co.z <= paw_z + 0.04
            ):
                weights[names[2]] = 0.92
                weights[names[1]] = 0.08
            elif nearest_name == names[0] or shoulder_d <= upper_d * 0.9:
                elbow_bleed = 0.12 * (shoulder_t ** 2)
                weights[names[0]] = 1.0 - elbow_bleed
                weights[names[1]] = elbow_bleed
            elif nearest_name == names[1] or upper_d <= lower_d:
                base_bleed = 0.28 * ((1.0 - upper_t) ** 2)
                tip_bleed = 0.16 * (upper_t ** 2)
                weights[names[1]] = 1.0 - base_bleed - tip_bleed
                weights[names[0]] = base_bleed
                weights[names[2]] = tip_bleed
            else:
                base_bleed = 0.18 * ((1.0 - lower_t) ** 2)
                weights[names[2]] = 1.0 - base_bleed
                weights[names[1]] = base_bleed
            ranked = sorted(
                ((name, weight) for name, weight in weights.items() if weight > 0.02),
                key=lambda item: item[1],
                reverse=True,
            )
            total = sum(weight for _, weight in ranked) or 1.0
            for name, weight in ranked:
                groups[name].add([vertex.index], weight / total, "REPLACE")
            count += 1

        # Reclaim Spine-painted paw pads that sit at the FrontLower tip.
        lower_bone = chain[2][1]
        deform = {bone.name for bone in rig.data.bones if bone.use_deform}
        for vertex in target.data.vertices:
            if side == "L" and vertex.co.x < -0.02:
                continue
            if side == "R" and vertex.co.x > 0.02:
                continue
            if vertex.co.z > paw_z:
                continue
            lower_t, lower_d = seg_proj(vertex.co, lower_bone)
            if lower_d > 0.09 or lower_t < 0.65:
                continue
            spine_w = sum(
                assignment.weight
                for assignment in vertex.groups
                if target.vertex_groups[assignment.group].name
                in {"Spine1", "Spine2", "Spine3", "Neck1", "Root"}
            )
            if spine_w < 0.2:
                continue
            for assignment in list(vertex.groups):
                gname = target.vertex_groups[assignment.group].name
                if gname in deform:
                    target.vertex_groups[gname].remove([vertex.index])
            groups[names[2]].add([vertex.index], 0.92, "REPLACE")
            groups[names[1]].add([vertex.index], 0.08, "REPLACE")
            tip_reclaimed += 1
            count += 1
        per_side[side] = count
        stabilized += count

    # Smooth only front-limb groups so Spine/chest weights cannot bleed back
    # onto paws (ALL-mode smooth is what recreates paw ribbons).
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.mode_set(mode="OBJECT")
    for side in ("L", "R"):
        for bone_name in (
            f"FrontShoulder.{side}",
            f"FrontUpper.{side}",
            f"FrontLower.{side}",
        ):
            group = target.vertex_groups.get(bone_name)
            if group is None:
                continue
            target.vertex_groups.active_index = group.index
            try:
                bpy.ops.object.vertex_group_smooth(
                    group_select_mode="ACTIVE", factor=0.08, repeat=1, expand=0.0
                )
            except RuntimeError:
                pass
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    # After smooth, re-assert only low paw pads that picked Spine back up.
    resealed = 0
    for side in ("L", "R"):
        lower = rig.data.bones.get(f"FrontLower.{side}")
        upper = rig.data.bones.get(f"FrontUpper.{side}")
        if lower is None or upper is None:
            continue
        lg = target.vertex_groups.get(f"FrontLower.{side}")
        ug = target.vertex_groups.get(f"FrontUpper.{side}")
        if lg is None or ug is None:
            continue
        deform = {bone.name for bone in rig.data.bones if bone.use_deform}
        paw_z = float(lower.tail_local.z) + 0.05
        for vertex in target.data.vertices:
            if side == "L" and vertex.co.x < -0.02:
                continue
            if side == "R" and vertex.co.x > 0.02:
                continue
            if vertex.co.z > paw_z:
                continue
            lower_t, lower_d = seg_proj(vertex.co, lower)
            if lower_t < 0.65 or lower_d > 0.09:
                continue
            spine_w = sum(
                assignment.weight
                for assignment in vertex.groups
                if target.vertex_groups[assignment.group].name
                in {"Spine1", "Spine2", "Spine3", "Neck1", "Root"}
            )
            if spine_w < 0.15:
                continue
            for assignment in list(vertex.groups):
                gname = target.vertex_groups[assignment.group].name
                if gname in deform:
                    target.vertex_groups[gname].remove([vertex.index])
            lg.add([vertex.index], 0.92, "REPLACE")
            ug.add([vertex.index], 0.08, "REPLACE")
            resealed += 1

    return {
        "stabilizedVertices": stabilized,
        "tipReclaimed": tip_reclaimed,
        "pawResealed": resealed,
        "perSide": per_side,
        "method": "front-limb-chain",
    }


def reclaim_dorsum_from_limbs(target, rig) -> dict:
    """Pull withers/back verts off Front/Hind/Neck so attack/gait cannot hump the ridge.

    Stag mid-back often parks on Neck1 (short neck + high withers). Headbutt tip
    then lifts that plate into a giant dorsal hump.
    """
    if "Spine2" not in rig.data.bones:
        return {"reclaimed": 0, "method": "skipped"}

    def seg_proj(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return 0.0, (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return amount, (point - (start + delta * amount)).length

    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    spine_bones = {
        name: rig.data.bones[name]
        for name in ("Spine1", "Spine2", "Spine3")
        if name in rig.data.bones
    }
    if not spine_bones:
        return {"reclaimed": 0, "method": "no-spine"}
    spine_groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in spine_bones
    }
    neck = rig.data.bones.get("Neck1")
    ridge_z = max(float(bone.tail_local.z) for bone in spine_bones.values())
    # Anything near/above the spinal ridge and not clearly out on a limb.
    dorsum_z = ridge_z - 0.08
    # Facing -Y: back of the neck joint — verts caudal of this are torso, not neck.
    neck_cut_y = float(neck.head_local.y) + 0.04 if neck is not None else 0.0
    reclaimed = 0
    from_neck = 0
    for vertex in target.data.vertices:
        if vertex.co.z < dorsum_z:
            continue
        limb_w = 0.0
        neck_w = 0.0
        for assignment in vertex.groups:
            name = target.vertex_groups[assignment.group].name
            if name.startswith("Front") or name.startswith("Hind"):
                limb_w += assignment.weight
            elif name == "Neck1":
                neck_w += assignment.weight
        # Mid-back on Neck1 (caudal of Neck1 head) — classic attack hump source.
        neck_on_back = neck_w >= 0.2 and vertex.co.y > neck_cut_y
        if limb_w < 0.25 and not neck_on_back:
            continue
        spine_dists = {
            name: seg_proj(vertex.co, bone)[1] for name, bone in spine_bones.items()
        }
        nearest_spine, spine_d = min(spine_dists.items(), key=lambda item: item[1])
        if neck_on_back:
            neck_d = seg_proj(vertex.co, neck)[1] if neck is not None else 1.0
            # Keep true neck column; only steal clearly nearer-spine back plate.
            if spine_d + 0.02 >= neck_d and vertex.co.y < neck_cut_y + 0.08:
                continue
        else:
            # Lateral shoulder/hip caps still count as dorsum when nearer spine.
            limb_bones = [
                bone
                for bone in rig.data.bones
                if bone.use_deform
                and (bone.name.startswith("Front") or bone.name.startswith("Hind"))
            ]
            limb_d = (
                min(seg_proj(vertex.co, bone)[1] for bone in limb_bones)
                if limb_bones
                else 1.0
            )
            if abs(vertex.co.x) > 0.13 and spine_d + 0.03 > limb_d:
                continue
        # Prefer spine whenever the vert is on the dorsal plate.
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform:
                target.vertex_groups[gname].remove([vertex.index])
        if nearest_spine == "Spine1" and "Spine2" in spine_groups:
            spine_groups["Spine1"].add([vertex.index], 0.4, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.6, "REPLACE")
        elif nearest_spine == "Spine3" and "Spine2" in spine_groups:
            spine_groups["Spine3"].add([vertex.index], 0.45, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.55, "REPLACE")
        elif nearest_spine == "Spine2" and "Spine3" in spine_groups:
            spine_groups["Spine2"].add([vertex.index], 0.65, "REPLACE")
            spine_groups["Spine3"].add([vertex.index], 0.35, "REPLACE")
        else:
            spine_groups[nearest_spine].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
        if neck_on_back:
            from_neck += 1
    return {
        "reclaimed": reclaimed,
        "fromNeck": from_neck,
        "method": "dorsum-from-limbs",
        "dorsumZ": dorsum_z,
        "neckCutY": neck_cut_y,
    }


def reclaim_torso_from_front_limbs(target, rig) -> dict:
    """Return chest/ribcage verts that heat-bind parked on FrontShoulder/Upper.

    Those verts sit nearer Spine2 than the forelimb; on pounce they dive with the
    shoulder and read as a protruding brisket flap.
    """
    if "FrontShoulder.L" not in rig.data.bones:
        return {"reclaimed": 0, "method": "skipped"}

    def seg_proj(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return 0.0, (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return amount, (point - (start + delta * amount)).length

    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    spine_bones = {
        name: rig.data.bones[name]
        for name in ("Spine1", "Spine2", "Spine3")
        if name in rig.data.bones
    }
    if not spine_bones:
        return {"reclaimed": 0, "method": "no-spine"}
    spine_groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in spine_bones
    }
    front_bones = {
        name: rig.data.bones[name]
        for name in rig.data.bones.keys()
        if name.startswith("Front") and name in deform
    }
    limb_rear_y = max(
        max(float(bone.head_local.y), float(bone.tail_local.y))
        for bone in front_bones.values()
    )
    shoulder_y = max(
        float(rig.data.bones[name].head_local.y)
        for name in ("FrontShoulder.L", "FrontShoulder.R")
        if name in rig.data.bones
    )
    shoulder_z = max(
        float(rig.data.bones[name].head_local.z)
        for name in ("FrontShoulder.L", "FrontShoulder.R")
        if name in rig.data.bones
    )
    reclaimed = 0
    for vertex in target.data.vertices:
        front_w = 0.0
        for assignment in vertex.groups:
            name = target.vertex_groups[assignment.group].name
            if name.startswith("Front"):
                front_w += assignment.weight
        if front_w < 0.25:
            continue
        # Keep real paws / shins on the limb chain.
        keep_paw = False
        for side in ("L", "R"):
            lower = rig.data.bones.get(f"FrontLower.{side}")
            if lower is None:
                continue
            lower_t, lower_d = seg_proj(vertex.co, lower)
            paw_z = float(lower.tail_local.z) + 0.08
            if vertex.co.z <= paw_z and lower_t >= 0.45 and lower_d <= 0.14:
                keep_paw = True
                break
        if keep_paw:
            continue
        spine_dists = {
            name: seg_proj(vertex.co, bone)[1] for name, bone in spine_bones.items()
        }
        front_d = min(seg_proj(vertex.co, bone)[1] for bone in front_bones.values())
        nearest_spine, spine_d = min(spine_dists.items(), key=lambda item: item[1])
        caudal = vertex.co.y > limb_rear_y + 0.06
        # Upper chest / armpit (not shins): prefer spine even when slightly nearer
        # a Front* bone so pounce cannot drag a brisket flap forward.
        chest_band = caudal and vertex.co.z > 0.35
        # Front chest / brisket sits FORWARD of the paw tip in -Y facing, so the
        # caudal test misses it — reclaim the midline pad even when nearer Front*.
        brisket = (
            abs(vertex.co.x) <= 0.16
            and vertex.co.z >= 0.5
            and vertex.co.z <= shoulder_z + 0.06
            and vertex.co.y <= shoulder_y + 0.18
        )
        if not (
            spine_d + 0.02 < front_d
            or (caudal and spine_d <= front_d + 0.05)
            or (chest_band and spine_d <= front_d + 0.14)
            or brisket
        ):
            continue
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform:
                target.vertex_groups[gname].remove([vertex.index])
        # Soft thoracic blend — prefer Spine2/3 ownership of the brisket.
        if nearest_spine == "Spine1" and "Spine2" in spine_groups:
            spine_groups["Spine1"].add([vertex.index], 0.35, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.65, "REPLACE")
        elif nearest_spine == "Spine3" and "Spine2" in spine_groups:
            spine_groups["Spine3"].add([vertex.index], 0.4, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.6, "REPLACE")
        else:
            spine_groups[nearest_spine].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
    return {"reclaimed": reclaimed, "method": "torso-from-front"}


def reclaim_head_from_front_limbs(target, rig) -> dict:
    """Pull snout/beard/face verts off Front* so gallop cannot swing the head.

    Bloom buffalo heat-bind parks the beard and face on FrontLower; gait then
    reads as the whole head thrashing with the foreleg, and Head-tip attacks
    look like they do nothing.
    """
    if "Head" not in rig.data.bones or "FrontLower.L" not in rig.data.bones:
        return {"reclaimed": 0, "method": "skipped"}

    def seg_dist(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return (point - (start + delta * amount)).length

    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    head = rig.data.bones["Head"]
    jaw = rig.data.bones.get("Jaw")
    neck = rig.data.bones.get("Neck1")
    shoulder = rig.data.bones.get("FrontShoulder.L")
    axial = {
        name: rig.data.bones[name]
        for name in ("Head", "Jaw", "Neck1")
        if name in rig.data.bones
    }
    groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in axial
    }
    front_bones = [
        bone
        for bone in rig.data.bones
        if bone.use_deform and bone.name.startswith("Front")
    ]
    # Facing -Y: anything forward of the neck joint is head territory.
    neck_cut = float(neck.head_local.y) if neck is not None else float(head.head_local.y)
    skull_y = float(head.head_local.y)
    jaw_z = float(jaw.head_local.z) if jaw is not None else float(head.head_local.z) - 0.15
    shoulder_x = abs(float(shoulder.head_local.x)) if shoulder is not None else 0.4
    shoulder_y = float(shoulder.head_local.y) if shoulder is not None else neck_cut
    hoof_z = min(
        float(rig.data.bones[name].tail_local.z)
        for name in ("FrontLower.L", "FrontLower.R")
        if name in rig.data.bones
    )
    reclaimed = 0
    for vertex in target.data.vertices:
        front_w = 0.0
        for assignment in vertex.groups:
            name = target.vertex_groups[assignment.group].name
            if name.startswith("Front"):
                front_w += assignment.weight
        if front_w < 0.05:
            continue
        # True hoof tips only — never beard, jowl, or chin (FrontLower passes
        # through the throat on Tripo bison meshes).
        keep_paw = False
        for side in ("L", "R"):
            lower = rig.data.bones.get(f"FrontLower.{side}")
            if lower is None:
                continue
            if (
                vertex.co.z <= hoof_z + 0.06
                and seg_dist(vertex.co, lower) <= 0.12
                and abs(vertex.co.x) >= 0.06
            ):
                keep_paw = True
                break
        if keep_paw:
            continue
        # Midline cranial pad only — never whole-limb "force_cranial". The buffalo
        # dewlap fix briefly treated every vert above the hoof as reclaimable and
        # wiped Front.R on short-neck horned meshes (ram).
        midline_x = shoulder_x * 0.55
        force_cranial = (
            vertex.co.z >= hoof_z + 0.08
            and abs(vertex.co.x) <= midline_x
        )
        # Tripo bison dewlap hangs on FrontLower through the whole throat —
        # caudal of Neck1 but still cranial of the brisket (y ~ neck_cut..-0.35).
        throat_dewlap = (
            abs(vertex.co.x) <= midline_x
            and neck_cut + 0.05 < vertex.co.y <= neck_cut + 0.42
            and vertex.co.z >= hoof_z + 0.08
            and vertex.co.z <= float(head.head_local.z) + 0.08
        )
        lateral_jowl = (
            abs(vertex.co.x) >= 0.12
            and abs(vertex.co.x) <= shoulder_x * 0.95
            and vertex.co.z >= 0.55
            and vertex.co.y <= neck_cut + 0.18
            and vertex.co.y <= skull_y + 0.35
        )
        cranial_y_limit = max(neck_cut, shoulder_y) + 0.08
        if force_cranial and vertex.co.z >= 0.5:
            cranial_y_limit = max(cranial_y_limit, neck_cut + 0.42)
        if throat_dewlap or lateral_jowl:
            cranial_y_limit = max(cranial_y_limit, neck_cut + 0.45)
        if vertex.co.y > cranial_y_limit:
            continue
        # Foreleg column: never reclaim lateral shin/shoulder verts onto Head.
        d_front_probe = min(seg_dist(vertex.co, bone) for bone in front_bones)
        if (
            abs(vertex.co.x) >= midline_x * 0.85
            and d_front_probe <= 0.16
            and vertex.co.z <= float(head.head_local.z) - 0.15
            and not lateral_jowl
        ):
            continue
        d_front = d_front_probe
        d_axial = {
            name: seg_dist(vertex.co, bone) for name, bone in axial.items()
        }
        nearest, d_near = min(d_axial.items(), key=lambda item: item[1])
        forward_face = vertex.co.y <= skull_y + 0.22
        force_midline = (
            vertex.co.y <= skull_y + 0.12
            and vertex.co.z >= 0.35
            and abs(vertex.co.x) <= 0.55
        )
        dewlap = (
            abs(vertex.co.x) <= shoulder_x * 0.85
            and vertex.co.y <= neck_cut + 0.42
            and vertex.co.z >= hoof_z + 0.08
            and vertex.co.z <= float(head.head_local.z) + 0.08
        )
        if not (
            force_cranial
            or dewlap
            or throat_dewlap
            or lateral_jowl
            or force_midline
            or d_near + 0.02 < d_front
            or (forward_face and d_near <= d_front + 0.18)
            or (vertex.co.y <= neck_cut and d_near <= d_front + 0.28)
        ):
            continue
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform and (
                gname.startswith("Front")
                or gname in {"Head", "Jaw", "Neck1", "Spine3", "Spine2"}
            ):
                target.vertex_groups[gname].remove([vertex.index])
        # Prefer Jaw for the hanging underside; Head for the snout pad.
        if throat_dewlap and jaw is not None and neck is not None:
            groups["Jaw"].add([vertex.index], 0.62, "REPLACE")
            groups["Neck1"].add([vertex.index], 0.38, "REPLACE")
        elif lateral_jowl and jaw is not None:
            groups["Jaw"].add([vertex.index], 0.55, "REPLACE")
            groups["Head"].add([vertex.index], 0.45, "REPLACE")
        elif (dewlap or force_cranial) and jaw is not None:
            if vertex.co.z <= jaw_z + 0.08:
                groups["Jaw"].add([vertex.index], 0.78, "REPLACE")
                groups["Head"].add([vertex.index], 0.22, "REPLACE")
            elif vertex.co.z >= float(head.head_local.z) - 0.08:
                groups["Head"].add([vertex.index], 0.72, "REPLACE")
                groups["Jaw"].add([vertex.index], 0.28, "REPLACE")
            elif nearest == "Neck1":
                groups["Neck1"].add([vertex.index], 0.55, "REPLACE")
                groups["Jaw"].add([vertex.index], 0.45, "REPLACE")
            else:
                groups["Jaw"].add([vertex.index], 0.55, "REPLACE")
                groups["Head"].add([vertex.index], 0.45, "REPLACE")
        elif nearest == "Jaw" and "Head" in groups:
            groups["Jaw"].add([vertex.index], 0.72, "REPLACE")
            groups["Head"].add([vertex.index], 0.28, "REPLACE")
        elif nearest == "Head" and jaw is not None and vertex.co.z < float(head.head_local.z) - 0.04:
            groups["Head"].add([vertex.index], 0.55, "REPLACE")
            groups["Jaw"].add([vertex.index], 0.45, "REPLACE")
        elif nearest == "Neck1" and "Head" in groups:
            groups["Neck1"].add([vertex.index], 0.6, "REPLACE")
            groups["Head"].add([vertex.index], 0.4, "REPLACE")
        else:
            groups[nearest].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
    return {"reclaimed": reclaimed, "method": "head-from-front"}


def seal_front_limb_spine_seams(target, rig) -> dict:
    """Pull Spine/Neck verts off front-leg seams so paws cannot ribbon away.

    Panther heat-bind leaves chest/flora verts on Spine3 while adjacent paw verts
    are on FrontLower — any shoulder swing stretches those edges into strings.
    Stay local to the limb bones so the chest is not swallowed onto FrontLower.
    """
    if "FrontShoulder.L" not in rig.data.bones:
        return {"absorbed": 0, "method": "skipped"}

    def seg_proj(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return 0.0, (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return amount, (point - (start + delta * amount)).length

    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    bad_exact = {"Spine1", "Spine2", "Spine3", "Neck1", "Root"}
    absorbed_total = 0
    per_side = {}

    for side in ("L", "R"):
        chain = [
            rig.data.bones.get(f"FrontShoulder.{side}"),
            rig.data.bones.get(f"FrontUpper.{side}"),
            rig.data.bones.get(f"FrontLower.{side}"),
        ]
        if any(bone is None for bone in chain):
            continue
        # Seal only Upper/Lower against Spine. FrontShoulder must stay a soft
        # chest hinge — hard-absorbing Spine onto Shoulder creates withers ribbons.
        names = [
            f"FrontShoulder.{side}",
            f"FrontUpper.{side}",
            f"FrontLower.{side}",
        ]
        seal_names = {names[1], names[2]}
        groups = {
            name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
            for name in names
        }
        shoulder_top = float(chain[0].head_local.z) + 0.05
        local_reach = max(
            (chain[2].tail_local - chain[1].head_local).length * 0.9,
            0.16,
        )
        limb_rear_y = max(
            max(float(bone.head_local.y), float(bone.tail_local.y)) for bone in chain
        )
        spine_bones = [
            bone
            for name in ("Spine1", "Spine2", "Spine3")
            if (bone := rig.data.bones.get(name)) is not None
        ]

        def dominant(index: int) -> str:
            best_name, best_w = "", -1.0
            for assignment in target.data.vertices[index].groups:
                name = target.vertex_groups[assignment.group].name
                if name in deform and assignment.weight > best_w:
                    best_name, best_w = name, assignment.weight
            return best_name

        def assign_to(index: int, bone_name: str) -> None:
            for assignment in list(target.data.vertices[index].groups):
                gname = target.vertex_groups[assignment.group].name
                if gname in deform:
                    target.vertex_groups[gname].remove([index])
            # Keep seam verts on the same limb bone as their neighbor.
            if bone_name == names[2]:
                groups[names[2]].add([index], 0.92, "REPLACE")
                groups[names[1]].add([index], 0.08, "REPLACE")
            else:
                groups[names[1]].add([index], 0.82, "REPLACE")
                groups[names[2]].add([index], 0.12, "REPLACE")
                groups[names[0]].add([index], 0.06, "REPLACE")

        absorbed = 0
        for _round in range(12):
            round_hits = 0
            for edge in target.data.edges:
                a, b = edge.vertices
                da, db = dominant(a), dominant(b)
                for _limb_i, other_i, limb_dom, other_dom in (
                    (a, b, da, db),
                    (b, a, db, da),
                ):
                    if limb_dom not in seal_names:
                        continue
                    if other_dom not in bad_exact and not other_dom.startswith("Hind"):
                        continue
                    point = target.data.vertices[other_i].co
                    # Panther laterality: L limb is +X, R limb is -X.
                    if side == "L" and point.x < -0.02:
                        continue
                    if side == "R" and point.x > 0.02:
                        continue
                    if point.z > shoulder_top:
                        continue
                    # Never pull the brisket/ribcage onto the forelimb.
                    if point.y > limb_rear_y + 0.08:
                        continue
                    d_upper = seg_proj(point, chain[1])[1]
                    d_lower = seg_proj(point, chain[2])[1]
                    if min(d_upper, d_lower) > local_reach:
                        continue
                    if spine_bones:
                        spine_d = min(seg_proj(point, bone)[1] for bone in spine_bones)
                        if spine_d + 0.02 < min(d_upper, d_lower):
                            continue
                    assign_to(other_i, limb_dom)
                    round_hits += 1
            absorbed += round_hits
            if round_hits == 0:
                break
        per_side[side] = absorbed
        absorbed_total += absorbed

    return {
        "absorbed": absorbed_total,
        "perSide": per_side,
        "method": "front-limb-spine-seal",
    }


def strip_neck_from_crown(target, locked: set[int], rig) -> dict:
    """Remove soft neck-column verts from the rigid crown set.

    Crown flood/seal can walk into the hanging neck; if those stay protected,
    reclaim cannot fix the Head rubber-band. Stag's short Neck1 makes this
    especially common — strip by Spine3→Head axial slot, not bone distance alone.
    """
    if not locked or rig is None or "Head" not in rig.data.bones:
        return {"lockedIndices": sorted(locked), "stripped": 0}
    vertices = target.data.vertices
    head = rig.data.bones["Head"]
    neck = rig.data.bones.get("Neck1")
    spine3 = rig.data.bones.get("Spine3")
    skull_top = max(float(head.head_local.z), float(head.tail_local.z))
    axis_start = spine3.head_local if spine3 is not None else head.head_local
    axis = head.tail_local - axis_start
    axis_len_sq = axis.length_squared
    cleaned: set[int] = set()
    stripped = 0
    for index in locked:
        point = vertices[index].co
        axial = 0.0
        if axis_len_sq > 1.0e-10:
            axial = max(0.0, min(1.0, (point - axis_start).dot(axis) / axis_len_sq))
        d_head = _bone_seg_dist(point, head)
        d_neck = _bone_seg_dist(point, neck) if neck is not None else 1.0e9
        # Antlers / horns rise above the skull tip. Mid-sagittal bovine humps are
        # stripped later by reclaim_bovine_hump_from_head — keep this cervid-safe.
        antler = point.z >= skull_top + 0.008
        # Tight skull pad only — not the hanging neck under the jaw/poll.
        skull_pad = (
            axial >= 0.86
            and point.z >= skull_top - 0.05
            and d_head <= d_neck * 1.05
        )
        if antler or skull_pad:
            cleaned.add(index)
            continue
        # Everything else that got crown-locked is soft neck / withers — strip it.
        stripped += 1
    return {"lockedIndices": sorted(cleaned), "stripped": stripped}


def reclaim_bovine_hump_from_head(target, rig, family_name: str) -> dict:
    """Pull bison/bull dorsal hump (+ bloom props) off Head onto Spine.

    Bovine Neck1 often starts on the withers, so crown/high-Z heuristics treat the
    whole hump as an 'antler' and lock it to Head. Headbutt then swings the body.
    """
    if family_name != "bovine":
        return {"reclaimed": 0, "method": "skipped"}
    if "Head" not in rig.data.bones:
        return {"reclaimed": 0, "method": "no-head"}
    spine_names = [n for n in ("Spine1", "Spine2", "Spine3") if n in rig.data.bones]
    if not spine_names:
        return {"reclaimed": 0, "method": "no-spine"}
    head = rig.data.bones["Head"]
    neck = rig.data.bones.get("Neck1")
    spine_bones = {name: rig.data.bones[name] for name in spine_names}
    spine_groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in spine_names
    }
    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    skull_y = float(head.head_local.y)
    skull_top = max(float(head.head_local.z), float(head.tail_local.z))
    ridge_z = max(float(bone.tail_local.z) for bone in spine_bones.values())
    dorsum_z = min(ridge_z - 0.1, skull_top + 0.02)
    # Horns stay: lateral + near the skull in Y. Everything else high goes Spine.
    reclaimed = 0
    for vertex in target.data.vertices:
        if vertex.co.z < dorsum_z:
            continue
        head_w = 0.0
        for assignment in vertex.groups:
            if target.vertex_groups[assignment.group].name == "Head":
                head_w = assignment.weight
                break
        if head_w < 0.12:
            continue
        lateral_horn = (
            abs(vertex.co.x) >= 0.09
            and vertex.co.y <= skull_y + 0.14
            and _bone_seg_dist(vertex.co, head)
            <= (
                _bone_seg_dist(vertex.co, neck) if neck is not None else 1.0e9
            )
            * 1.1
        )
        if lateral_horn:
            continue
        # Keep the true skull / face on Head — only steal caudal dorsal ridge.
        d_head = _bone_seg_dist(vertex.co, head)
        d_spine = min(_bone_seg_dist(vertex.co, bone) for bone in spine_bones.values())
        neck_cut = float(neck.head_local.y) if neck is not None else skull_y + 0.2
        if vertex.co.y <= neck_cut and d_head <= d_spine + 0.06:
            continue
        # Mid-back / hump / dorsal props — own with nearest spine.
        nearest = min(
            spine_bones.items(),
            key=lambda item: _bone_seg_dist(vertex.co, item[1]),
        )[0]
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform:
                target.vertex_groups[gname].remove([vertex.index])
        if nearest == "Spine2" and "Spine3" in spine_groups and "Spine1" in spine_groups:
            # Blend along the ridge so the hump doesn't hinge on one bone.
            spine_groups["Spine1"].add([vertex.index], 0.25, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.5, "REPLACE")
            spine_groups["Spine3"].add([vertex.index], 0.25, "REPLACE")
        elif nearest == "Spine3" and "Spine2" in spine_groups:
            spine_groups["Spine3"].add([vertex.index], 0.55, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.45, "REPLACE")
        elif nearest == "Spine1" and "Spine2" in spine_groups:
            spine_groups["Spine1"].add([vertex.index], 0.55, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.45, "REPLACE")
        else:
            spine_groups[nearest].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
    return {
        "reclaimed": reclaimed,
        "method": "bovine-hump-from-head",
        "dorsumZ": dorsum_z,
    }


def reclaim_neck1_caudal_of_pivot(target, rig) -> dict:
    """Strip Neck1 from verts behind its head (withers pivot).

    Bovine/cervid Neck1 often originates on the withers. Any torso/hump verts
    still painted Neck1 orbit that pivot on head-charge and explode the back
    upward — exactly the broken buffalo attack silhouette.
    """
    if "Neck1" not in rig.data.bones:
        return {"reclaimed": 0, "method": "skipped"}
    neck = rig.data.bones["Neck1"]
    spine_names = [n for n in ("Spine1", "Spine2", "Spine3") if n in rig.data.bones]
    if not spine_names:
        return {"reclaimed": 0, "method": "no-spine"}
    spine_bones = {name: rig.data.bones[name] for name in spine_names}
    spine_groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in spine_names
    }
    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    # Facing -Y: larger Y is toward the tail. Slack keeps the true neck column.
    cut_y = float(neck.head_local.y) + 0.03
    reclaimed = 0
    for vertex in target.data.vertices:
        if vertex.co.y <= cut_y:
            continue
        neck_w = 0.0
        for assignment in vertex.groups:
            if target.vertex_groups[assignment.group].name == "Neck1":
                neck_w = assignment.weight
                break
        if neck_w < 0.08:
            continue
        nearest = min(
            spine_bones.items(),
            key=lambda item: _bone_seg_dist(vertex.co, item[1]),
        )[0]
        # Remove Neck1 (and Head bleed on the back) then park on Spine.
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform and gname in {
                "Neck1",
                "Head",
                "Jaw",
                "Spine1",
                "Spine2",
                "Spine3",
            }:
                target.vertex_groups[gname].remove([vertex.index])
        if nearest == "Spine3" and "Spine2" in spine_groups:
            spine_groups["Spine3"].add([vertex.index], 0.5, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.5, "REPLACE")
        elif nearest == "Spine1" and "Spine2" in spine_groups:
            spine_groups["Spine1"].add([vertex.index], 0.5, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.5, "REPLACE")
        elif nearest == "Spine2" and "Spine3" in spine_groups:
            spine_groups["Spine2"].add([vertex.index], 0.6, "REPLACE")
            spine_groups["Spine3"].add([vertex.index], 0.4, "REPLACE")
        else:
            spine_groups[nearest].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
    return {
        "reclaimed": reclaimed,
        "method": "neck1-caudal-to-spine",
        "cutY": cut_y,
    }


def reclaim_head_caudal_of_skull(target, rig) -> dict:
    """Strip Head/Jaw from torso verts behind the skull.

    Crown/high-front lock can paint mid-back bloom props onto Head. A head-charge
    tip then flings those verts in a huge arc (ridge Z spikes ~0.5m+) — the
    classic 'exploding back' buffalo attack.
    """
    if "Head" not in rig.data.bones:
        return {"reclaimed": 0, "method": "skipped"}
    head = rig.data.bones["Head"]
    neck = rig.data.bones.get("Neck1")
    spine_names = [n for n in ("Spine1", "Spine2", "Spine3") if n in rig.data.bones]
    if not spine_names:
        return {"reclaimed": 0, "method": "no-spine"}
    spine_bones = {name: rig.data.bones[name] for name in spine_names}
    spine_groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in spine_names
    }
    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    # Anything clearly behind the skull / neck joint is torso, not crown.
    skull_y = float(head.head_local.y)
    skull_top = max(float(head.head_local.z), float(head.tail_local.z))
    cut_y = (
        float(neck.head_local.y) + 0.02
        if neck is not None
        else skull_y + 0.15
    )
    reclaimed = 0
    for vertex in target.data.vertices:
        if vertex.co.y <= cut_y:
            continue
        head_w = 0.0
        jaw_w = 0.0
        for assignment in vertex.groups:
            name = target.vertex_groups[assignment.group].name
            if name == "Head":
                head_w = assignment.weight
            elif name == "Jaw":
                jaw_w = assignment.weight
        if head_w + jaw_w < 0.08:
            continue
        # Keep curled horns / antler curls that wrap caudal of the skull.
        # Require ABOVE the skull pad — chest/withers at ~skull_top must not count.
        if (
            abs(vertex.co.x) >= 0.12
            and vertex.co.z >= skull_top + 0.05
            and vertex.co.y <= skull_y + 0.45
        ):
            continue
        # Keep true lateral horn roots on the skull pad (nearer Head than Spine).
        if (
            abs(vertex.co.x) >= 0.1
            and vertex.co.y <= skull_y + 0.2
            and vertex.co.z >= skull_top - 0.02
            and _bone_seg_dist(vertex.co, head)
            <= min(_bone_seg_dist(vertex.co, b) for b in spine_bones.values()) * 0.95
        ):
            continue
        nearest = min(
            spine_bones.items(),
            key=lambda item: _bone_seg_dist(vertex.co, item[1]),
        )[0]
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform and gname in {
                "Head",
                "Jaw",
                "Neck1",
                "Spine1",
                "Spine2",
                "Spine3",
            }:
                target.vertex_groups[gname].remove([vertex.index])
        if nearest == "Spine2" and "Spine3" in spine_groups and "Spine1" in spine_groups:
            spine_groups["Spine1"].add([vertex.index], 0.2, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.5, "REPLACE")
            spine_groups["Spine3"].add([vertex.index], 0.3, "REPLACE")
        elif nearest == "Spine3" and "Spine2" in spine_groups:
            spine_groups["Spine3"].add([vertex.index], 0.55, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.45, "REPLACE")
        elif nearest == "Spine1" and "Spine2" in spine_groups:
            spine_groups["Spine1"].add([vertex.index], 0.55, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.45, "REPLACE")
        else:
            spine_groups[nearest].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
    return {
        "reclaimed": reclaimed,
        "method": "head-caudal-to-spine",
        "cutY": cut_y,
    }


def reclaim_brisket_from_head(target, rig) -> dict:
    """Strip Head from the front chest / withers pad.

    Crown flood and neck restabilize often leave the brisket on Head. Gallop then
    pumps the front chest even when Neck1/Head keys are locked (via Front* bleed
    or residual Head weights near the shoulders).
    """
    if "Head" not in rig.data.bones:
        return {"reclaimed": 0, "method": "skipped"}
    head = rig.data.bones["Head"]
    spine_names = [n for n in ("Spine1", "Spine2", "Spine3") if n in rig.data.bones]
    if not spine_names:
        return {"reclaimed": 0, "method": "no-spine"}
    spine_bones = {name: rig.data.bones[name] for name in spine_names}
    spine_groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in spine_names
    }
    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    skull_y = float(head.head_local.y)
    skull_top = max(float(head.head_local.z), float(head.tail_local.z))
    reclaimed = 0
    for vertex in target.data.vertices:
        if abs(vertex.co.x) > 0.3:
            continue
        if vertex.co.z >= skull_top - 0.04:
            continue
        if vertex.co.y <= skull_y + 0.04:
            continue
        if vertex.co.z < 0.4:
            continue
        head_w = 0.0
        for assignment in vertex.groups:
            if target.vertex_groups[assignment.group].name == "Head":
                head_w = assignment.weight
                break
        if head_w < 0.2:
            continue
        # Keep anything still nearer Head than Spine (true skull sides).
        if _bone_seg_dist(vertex.co, head) <= min(
            _bone_seg_dist(vertex.co, b) for b in spine_bones.values()
        ) * 0.9 and vertex.co.z >= skull_top - 0.12:
            continue
        nearest = min(
            spine_bones.items(),
            key=lambda item: _bone_seg_dist(vertex.co, item[1]),
        )[0]
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform and gname in {
                "Head",
                "Jaw",
                "Neck1",
                "Spine1",
                "Spine2",
                "Spine3",
                "FrontShoulder.L",
                "FrontShoulder.R",
                "FrontUpper.L",
                "FrontUpper.R",
            }:
                target.vertex_groups[gname].remove([vertex.index])
        if nearest == "Spine3" and "Spine2" in spine_groups:
            spine_groups["Spine3"].add([vertex.index], 0.55, "REPLACE")
            spine_groups["Spine2"].add([vertex.index], 0.45, "REPLACE")
        elif nearest == "Spine2" and "Spine3" in spine_groups:
            spine_groups["Spine2"].add([vertex.index], 0.6, "REPLACE")
            spine_groups["Spine3"].add([vertex.index], 0.4, "REPLACE")
        else:
            spine_groups[nearest].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
    return {"reclaimed": reclaimed, "method": "brisket-from-head"}


def reclaim_bovine_jaw_caudal_of_hinge(target, rig, family_name: str) -> dict:
    """Park bison beard/dewlap off Jaw when caudal of the jaw hinge.

    Tripo heat-bind + head-from-front reclaim dump the hanging beard onto Jaw.
    Attack tips Head/Neck1 and the Jaw child swings that pad ~1m while the
    Spine3 neck column stays planted — reads as the head/neck stretching.
    """
    if family_name != "bovine":
        return {"reclaimed": 0, "method": "skipped"}
    jaw = rig.data.bones.get("Jaw")
    neck = rig.data.bones.get("Neck1")
    head = rig.data.bones.get("Head")
    if jaw is None or neck is None or head is None:
        return {"reclaimed": 0, "method": "skipped"}
    deform = {bone.name for bone in rig.data.bones if bone.use_deform}
    groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in ("Jaw", "Neck1", "Head", "Spine3", "Spine2")
        if name in rig.data.bones or name in {"Jaw", "Neck1", "Head"}
    }
    hinge_y = float(jaw.head_local.y)
    neck_y = float(neck.head_local.y)
    reclaimed = 0
    for vertex in target.data.vertices:
        jaw_w = 0.0
        for assignment in vertex.groups:
            if target.vertex_groups[assignment.group].name == "Jaw":
                jaw_w = assignment.weight
                break
        if jaw_w < 0.2:
            continue
        # Facing -Y: larger Y is toward the body. Keep true mandible forward of hinge.
        if vertex.co.y <= hinge_y + 0.02:
            continue
        for assignment in list(vertex.groups):
            gname = target.vertex_groups[assignment.group].name
            if gname in deform and gname in {
                "Jaw",
                "Head",
                "Neck1",
                "Spine2",
                "Spine3",
            }:
                target.vertex_groups[gname].remove([vertex.index])
        # Along Neck1 bone: move with the lean. Behind Neck1 pivot: stay on spine.
        if vertex.co.y <= neck_y + 0.06:
            groups["Neck1"].add([vertex.index], 0.7, "REPLACE")
            groups["Head"].add([vertex.index], 0.3, "REPLACE")
        elif "Spine3" in groups and "Spine2" in groups:
            groups["Spine3"].add([vertex.index], 0.55, "REPLACE")
            groups["Spine2"].add([vertex.index], 0.45, "REPLACE")
        elif "Spine3" in groups:
            groups["Spine3"].add([vertex.index], 1.0, "REPLACE")
        else:
            groups["Neck1"].add([vertex.index], 1.0, "REPLACE")
        reclaimed += 1
    return {
        "reclaimed": reclaimed,
        "method": "bovine-jaw-caudal-to-neck",
        "hingeY": hinge_y,
    }


def reclaim_neck_from_head(target, rig, protect_indices: set[int] | None = None) -> dict:
    """Pull the neck column off Head so tips bend instead of rubber-banding."""
    if "Neck1" not in rig.data.bones or "Head" not in rig.data.bones:
        return {"reclaimed": 0}
    deform_names = {bone.name for bone in rig.data.bones if bone.use_deform}
    neck_bone = rig.data.bones["Neck1"]
    head_bone = rig.data.bones["Head"]
    spine_bone = rig.data.bones.get("Spine3")
    vertices = target.data.vertices
    skull_top = max(float(head_bone.head_local.z), float(head_bone.tail_local.z))
    # Only true crown (above skull) stays protected — never the neck column.
    protect = {
        index
        for index in (protect_indices or ())
        if vertices[index].co.z >= skull_top - 0.03
    }
    groups = {
        name: target.vertex_groups.get(name) or target.vertex_groups.new(name=name)
        for name in ("Head", "Neck1", "Spine3")
    }
    reach = max(
        (neck_bone.tail_local - neck_bone.head_local).length * 1.7,
        (head_bone.tail_local - head_bone.head_local).length * 2.4,
        0.36,
    )

    reclaimed = 0
    for vertex in vertices:
        if vertex.index in protect:
            continue
        d_neck = _bone_seg_dist(vertex.co, neck_bone)
        d_head = _bone_seg_dist(vertex.co, head_bone)
        d_spine = (
            _bone_seg_dist(vertex.co, spine_bone) if spine_bone is not None else 1.0e9
        )
        # True crown pad: high AND nearer Head than Neck1. Hanging cervid necks
        # can sit at similar Z to the skull and must still be reclaimable.
        if vertex.co.z >= skull_top - 0.03 and d_head + 0.01 < d_neck:
            continue
        near_column = (
            d_neck <= reach
            or (d_spine <= reach * 1.2 and d_neck <= d_head * 1.6)
            or (d_neck <= d_head * 1.35)
        )
        if not near_column:
            continue
        head_w = 0.0
        for assignment in vertex.groups:
            name = target.vertex_groups[assignment.group].name
            if name == "Head":
                head_w = assignment.weight
                break
        if head_w < 0.05 and d_neck > min(d_head, d_spine) * 0.9:
            continue
        along = 0.0
        start = neck_bone.head_local
        delta = neck_bone.tail_local - start
        if delta.length_squared > 1.0e-10:
            along = max(
                0.0, min(1.0, (vertex.co - start).dot(delta) / delta.length_squared)
            )
        for assignment in list(vertex.groups):
            name = target.vertex_groups[assignment.group].name
            if name in deform_names and name in {"Head", "Neck1", "Spine3", "Jaw"}:
                target.vertex_groups[name].remove([vertex.index])
        tip = along ** 2
        if along < 0.72:
            # Mid-neck: no Head influence — this is what kills the rubber tube.
            groups["Neck1"].add([vertex.index], 0.82, "REPLACE")
            groups["Spine3"].add([vertex.index], 0.18 * (1.0 - along), "REPLACE")
            groups["Head"].add([vertex.index], 0.0, "REPLACE")
        else:
            groups["Neck1"].add([vertex.index], 0.72 - 0.1 * tip, "REPLACE")
            groups["Spine3"].add([vertex.index], 0.1 * (1.0 - along), "REPLACE")
            groups["Head"].add([vertex.index], 0.08 + 0.2 * tip, "REPLACE")
        reclaimed += 1
    return {"reclaimed": reclaimed}


def stabilize_head_neck_by_bones(
    target, rig, protect_indices: set[int] | None = None
) -> dict:
    """Assign Head/Neck1/Spine3 from the bone chain, not AABB front-Y.

    Short Neck1 bones (stag) make most of the visual neck nearer Head in 3D;
    use axial position along Spine3→Head so the column stays on Neck1 and tips
    bend instead of looking like the neck extends.
    """
    import bpy

    needed = ("Head", "Jaw", "Neck1", "Spine3")
    bones = {name: rig.data.bones.get(name) for name in ("Head", "Neck1", "Spine3")}
    if any(bone is None for bone in bones.values()):
        return {"stabilizedVertices": 0, "method": "bone-chain-skipped"}

    groups = {name: target.vertex_groups.get(name) for name in needed}
    for name in needed:
        if groups[name] is None:
            groups[name] = target.vertex_groups.new(name=name)

    protect = set(protect_indices or ())
    axis_start = bones["Spine3"].head_local
    axis_end = bones["Head"].tail_local
    axis = axis_end - axis_start
    axis_len_sq = axis.length_squared
    skull_top = max(
        float(bones["Head"].head_local.z), float(bones["Head"].tail_local.z)
    )

    def segment_proj(point, bone):
        start = bone.head_local
        delta = bone.tail_local - start
        length_squared = delta.length_squared
        if length_squared < 1.0e-10:
            return 0.0, (point - start).length
        amount = max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
        return amount, (point - (start + delta * amount)).length

    # Limit to verts near the axial chain so limbs stay untouched.
    reach = max(
        (bones["Neck1"].tail_local - bones["Spine3"].head_local).length * 1.05,
        (bones["Head"].tail_local - bones["Head"].head_local).length * 3.0,
        (bones["Neck1"].tail_local - bones["Neck1"].head_local).length * 1.5,
        0.3,
    )
    limb_bones = [
        bone
        for bone in rig.data.bones
        if bone.use_deform and (bone.name.startswith("Front") or bone.name.startswith("Hind"))
    ]
    stabilized = 0
    jaw_weighted = 0
    skipped_limb = 0
    for vertex in target.data.vertices:
        if vertex.index in protect:
            continue
        dists = {name: segment_proj(vertex.co, bone) for name, bone in bones.items()}
        nearest_name, (_t, nearest_d) = min(dists.items(), key=lambda item: item[1][1])
        if nearest_d > reach:
            continue
        # Never strip Front/Hind weights from verts that belong to a limb column.
        # Final neck restabilize previously re-painted armpit/paw seams onto Spine3
        # and global-smoothed ribbons back into the forelimbs.
        if limb_bones:
            limb_d = min(segment_proj(vertex.co, bone)[1] for bone in limb_bones)
            if limb_d + 0.01 < nearest_d:
                skipped_limb += 1
                continue
        # Only strip limb weights on verts we are committing to the neck axis.
        for assignment in list(vertex.groups):
            name = target.vertex_groups[assignment.group].name
            if name.startswith("Front") or name.startswith("Hind"):
                target.vertex_groups[name].remove([vertex.index])

        head_t, head_d = dists["Head"]
        neck_t, neck_d = dists["Neck1"]
        spine_t, spine_d = dists["Spine3"]
        weights = {"Head": 0.0, "Jaw": 0.0, "Neck1": 0.0, "Spine3": 0.0}

        # Axial slot along the whole neck+skull: 0 at withers, 1 at snout tip.
        axial = 0.0
        if axis_len_sq > 1.0e-10:
            axial = max(
                0.0,
                min(1.0, (vertex.co - axis_start).dot(axis) / axis_len_sq),
            )

        on_skull = (
            vertex.co.z >= skull_top - 0.04
            and head_d <= neck_d * 1.05
            and axial >= 0.78
        )
        if on_skull or (nearest_name == "Head" and axial >= 0.84):
            if vertex.co.z < bones["Head"].head_local.z - 0.02:
                weights["Jaw"] = 0.62
                weights["Head"] = 0.38
            else:
                weights["Head"] = 0.88
                weights["Neck1"] = 0.12 * max(0.0, 1.0 - head_t)
        elif axial < 0.42 or (nearest_name == "Spine3" and spine_d + 0.01 < neck_d):
            weights["Spine3"] = 0.55 + 0.35 * (1.0 - axial)
            weights["Neck1"] = 1.0 - weights["Spine3"]
        else:
            # Visual neck column — Neck1 owns it even when Head bone is nearer.
            tip = max(0.0, (axial - 0.55) / 0.3)
            base = max(0.0, (0.55 - axial) / 0.55)
            weights["Neck1"] = 0.78 - 0.2 * tip
            weights["Spine3"] = 0.22 * base
            weights["Head"] = 0.08 * tip

        ranked = sorted(
            ((name, weight) for name, weight in weights.items() if weight > 0.02),
            key=lambda item: item[1],
            reverse=True,
        )[:3]
        total = sum(weight for _, weight in ranked)
        if total <= 1.0e-6:
            continue
        # Clear only head/neck groups — never strip Front/Hind limb weights.
        for name in needed:
            groups[name].remove([vertex.index])
        for name, weight in ranked:
            groups[name].add([vertex.index], weight / total, "REPLACE")
        stabilized += 1
        if any(name == "Jaw" for name, _ in ranked):
            jaw_weighted += 1

    # Smooth only axial groups so limb weights cannot be overwritten by bleed.
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.mode_set(mode="OBJECT")
    for name in needed:
        group = groups.get(name)
        if group is None:
            continue
        target.vertex_groups.active_index = group.index
        try:
            bpy.ops.object.vertex_group_smooth(
                group_select_mode="ACTIVE", factor=0.14, repeat=1, expand=0.0
            )
        except RuntimeError:
            pass
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    return {
        "stabilizedVertices": stabilized,
        "jawWeightedVertices": jaw_weighted,
        "skippedLimbVertices": skipped_limb,
        "method": "bone-chain-axial",
        "reach": reach,
        "maxGroups": 4,
        "protectedCrown": len(protect),
    }


def create_action(v4, rig, species: str, label: str, frames: int):
    return v4.create_action(rig, f"ScratchV6_{species}|{label}", 1, frames)


def loop_phase(frame: int, frames: int) -> float:
    return ((frame - 1) / max(1, frames - 1)) * math.tau


def rotate(v4, rig, name: str, axis: str, angle: float) -> None:
    bone = rig.pose.bones.get(name)
    if bone is not None:
        v4.set_bone_rotation(bone, v4.quat_axis_angle(axis, angle))


def author_idle(v4, rig, species: str, profile: dict) -> dict:
    frames = 61
    action = create_action(v4, rig, species, "Idle", frames)
    names = [bone.name for bone in rig.pose.bones]
    family = profile["family"]
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        breathe = math.sin(phase)
        scan = math.sin(phase)
        v4.reset_pose(rig)
        if family == "avian":
            # Hovering glide — soft wing breathe, tucked feet still.
            rotate(v4, rig, "FrontShoulder.L", "y", 0.04 * breathe)
            rotate(v4, rig, "FrontShoulder.R", "y", -0.04 * breathe)
            rotate(v4, rig, "FrontUpper.L", "y", 0.02 * breathe)
            rotate(v4, rig, "FrontUpper.R", "y", -0.02 * breathe)
            rotate(v4, rig, "Neck1", "z", 0.01 * scan)
            rotate(v4, rig, "Head", "x", 0.008 * math.sin(phase + 0.5))
            rotate(v4, rig, "Tail2", "z", 0.02 * math.sin(phase + 0.3))
            set_root_world_z(rig, 0.04 + 0.008 * breathe)
        elif family == "piscine":
            # Station-keeping swim — tiny lateral wave.
            rotate(v4, rig, "Spine2", "z", 0.03 * breathe)
            rotate(v4, rig, "Spine3", "z", 0.04 * math.sin(phase + 0.4))
            rotate(v4, rig, "Tail2", "z", 0.08 * math.sin(phase + 0.7))
            rotate(v4, rig, "Tail3", "z", 0.1 * math.sin(phase + 1.0))
            rotate(v4, rig, "FrontShoulder.L", "z", 0.05 * math.sin(phase + 0.2))
            rotate(v4, rig, "FrontShoulder.R", "z", -0.05 * math.sin(phase + 0.2))
            rotate(v4, rig, "Head", "x", 0.006 * math.sin(phase + 0.5))
            set_root_world_z(rig, 0.06 + 0.004 * breathe)
        elif family == "reptile":
            rotate(v4, rig, "Spine2", "x", 0.01 * breathe)
            rotate(v4, rig, "Tail2", "z", 0.05 * math.sin(phase + 0.3))
            rotate(v4, rig, "Tail3", "z", 0.07 * math.sin(phase + 0.7))
            rotate(v4, rig, "Jaw", "x", 0.02 * max(0.0, math.sin(phase * 0.5)))
            rotate(v4, rig, "Head", "x", 0.005 * math.sin(phase + 0.5))
            set_root_world_z(rig, 0.02 + 0.003 * breathe)
        else:
            rotate(v4, rig, "Spine2", "x", 0.012 * breathe)
            rotate(v4, rig, "Spine3", "x", 0.009 * breathe)
            rotate(v4, rig, "Neck1", "z", (0.012 if "cervid" in family else 0.007) * scan)
            rotate(v4, rig, "Head", "x", 0.006 * math.sin(phase + 0.5))
            tail = 0.035 * profile["tailBalance"]
            rotate(v4, rig, "Tail2", "z", tail * math.sin(phase + 0.3))
            rotate(v4, rig, "Tail3", "z", tail * 1.25 * math.sin(phase + 0.7))
            rig.pose.bones["Root"].location = (0.0, 0.0, 0.0025 * breathe)
        v4.key_pose(rig, frame, names)
    return {"name": action.name, "frames": frames, "loop": True}


def author_flight(
    v4, rig, species: str, profile: dict, *, label: str, intensity: str
) -> dict:
    """Avian locomotion: Walk/Trot/Gallop = cruise / steady / power wing flaps."""
    frames = {"Walk": 37, "Trot": 29, "Gallop": 21}[label]
    amp = {"cruise": 0.42, "steady": 0.58, "power": 0.78}[intensity]
    action = create_action(v4, rig, species, label, frames)
    names = [bone.name for bone in rig.pose.bones]
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        flap = math.sin(phase)
        # Slight phase lag down the wing for a soft fold.
        mid = math.sin(phase - 0.25)
        tip = math.sin(phase - 0.45)
        v4.reset_pose(rig)
        # Synchronized downstroke (mirrored L/R). Legs stay tucked — tiny talon sway only.
        rotate(v4, rig, "FrontShoulder.L", "y", amp * 0.55 * flap)
        rotate(v4, rig, "FrontShoulder.R", "y", -amp * 0.55 * flap)
        rotate(v4, rig, "FrontUpper.L", "y", amp * 0.35 * mid)
        rotate(v4, rig, "FrontUpper.R", "y", -amp * 0.35 * mid)
        rotate(v4, rig, "FrontLower.L", "y", amp * 0.22 * tip)
        rotate(v4, rig, "FrontLower.R", "y", -amp * 0.22 * tip)
        rotate(v4, rig, "HindUpper.L", "x", 0.04 * math.sin(phase + 1.1))
        rotate(v4, rig, "HindUpper.R", "x", 0.04 * math.sin(phase + 1.1))
        rotate(v4, rig, "HindFoot.L", "x", 0.05 * math.sin(phase + 1.4))
        rotate(v4, rig, "HindFoot.R", "x", 0.05 * math.sin(phase + 1.4))
        rotate(v4, rig, "Spine2", "x", 0.02 * amp * flap)
        rotate(v4, rig, "Tail2", "z", 0.03 * math.sin(phase + 0.6))
        rotate(v4, rig, "Neck1", "x", 0.012 * (1.0 - profile["headStability"]) * flap)
        rotate(v4, rig, "Head", "x", 0.008 * math.sin(phase + 0.4))
        lift = 0.08 + 0.05 * amp * (1.0 - abs(flap))
        set_root_world_z(rig, lift)
        v4.key_pose(rig, frame, names)
    return {
        "name": action.name,
        "frames": frames,
        "loop": True,
        "pattern": profile.get("walkPattern" if label == "Walk" else "trotPattern" if label == "Trot" else "gallopPattern"),
    }


def author_swim(
    v4, rig, species: str, profile: dict, *, label: str, intensity: str
) -> dict:
    """Piscine locomotion: Walk/Trot/Gallop = slow / cruise / dart swimming."""
    frames = {"Walk": 41, "Trot": 31, "Gallop": 23}[label]
    amp = {"slow": 0.55, "cruise": 0.85, "dart": 1.2}[intensity]
    action = create_action(v4, rig, species, label, frames)
    names = [bone.name for bone in rig.pose.bones]
    tail_bal = profile["tailBalance"]
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        # Traveling lateral wave head → tail.
        v4.reset_pose(rig)
        rotate(v4, rig, "Spine1", "z", amp * 0.06 * math.sin(phase))
        rotate(v4, rig, "Spine2", "z", amp * 0.11 * math.sin(phase - 0.45))
        rotate(v4, rig, "Spine3", "z", amp * 0.14 * math.sin(phase - 0.9))
        rotate(v4, rig, "Neck1", "z", amp * 0.04 * math.sin(phase + 0.2))
        rotate(v4, rig, "Head", "z", amp * 0.02 * math.sin(phase + 0.35))
        rotate(v4, rig, "Tail1", "z", amp * 0.22 * tail_bal * math.sin(phase - 1.2))
        rotate(v4, rig, "Tail2", "z", amp * 0.32 * tail_bal * math.sin(phase - 1.55))
        rotate(v4, rig, "Tail3", "z", amp * 0.4 * tail_bal * math.sin(phase - 1.9))
        rotate(v4, rig, "Tail4", "z", amp * 0.28 * tail_bal * math.sin(phase - 2.2))
        # Pectoral fin flutter (counts as Front* limb motion for QA).
        fin = amp * 0.22 * math.sin(phase + 0.7)
        rotate(v4, rig, "FrontShoulder.L", "z", fin)
        rotate(v4, rig, "FrontShoulder.R", "z", -fin)
        rotate(v4, rig, "FrontUpper.L", "z", fin * 0.55)
        rotate(v4, rig, "FrontUpper.R", "z", -fin * 0.55)
        # Soft pelvic sway so Hind* also registers.
        rotate(v4, rig, "HindUpper.L", "z", fin * 0.35)
        rotate(v4, rig, "HindUpper.R", "z", -fin * 0.35)
        set_root_world_z(rig, 0.1 + 0.015 * amp * abs(math.sin(phase)))
        v4.key_pose(rig, frame, names)
    return {
        "name": action.name,
        "frames": frames,
        "loop": True,
        "pattern": profile.get("walkPattern" if label == "Walk" else "trotPattern" if label == "Trot" else "gallopPattern"),
    }


def author_waddle(
    v4, rig, species: str, profile: dict, *, label: str, intensity: str
) -> dict:
    """Reptile land locomotion: Walk/Trot = slow / fast sprawled waddle."""
    frames = {"Walk": 45, "Trot": 33}[label]
    scale = {"slow": 0.42, "fast": 0.58}[intensity]
    action = create_action(v4, rig, species, label, frames)
    names = [bone.name for bone in rig.pose.bones]
    # Lateral couplet: same-side limbs move together (classic crocodilian crawl).
    offsets = {"Hind.L": 0.0, "Front.L": 0.35, "Hind.R": math.pi, "Front.R": math.pi + 0.35}
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        v4.reset_pose(rig)
        for side in ("L", "R"):
            front = math.sin(phase + offsets[f"Front.{side}"])
            hind = math.sin(phase + offsets[f"Hind.{side}"])
            apply_limb_cycle(
                v4,
                rig,
                side,
                front,
                hind,
                scale,
                reach_sign=profile.get("frontReachSign", -1.0),
                reach_axis=profile.get("frontReachAxis", "x"),
                hind_reach_sign=profile.get("hindReachSign"),
            )
        sway = 0.045 * math.sin(phase)
        rotate(v4, rig, "Spine1", "z", sway)
        rotate(v4, rig, "Spine2", "z", sway * 0.7)
        rotate(v4, rig, "Tail2", "z", 0.12 * profile["tailBalance"] * math.sin(phase + math.pi))
        rotate(v4, rig, "Tail3", "z", 0.16 * profile["tailBalance"] * math.sin(phase + math.pi))
        rotate(v4, rig, "Head", "z", 0.01 * math.sin(phase + 0.5))
        set_root_world_z(rig, 0.1)
        v4.key_pose(rig, frame, names)
    return {
        "name": action.name,
        "frames": frames,
        "loop": True,
        "pattern": profile.get("walkPattern" if label == "Walk" else "trotPattern"),
    }


def author_reptile_swim(v4, rig, species: str, profile: dict) -> dict:
    """Reptile Gallop equivalent: side-to-side swimming with a driving tail."""
    frames = 29
    action = create_action(v4, rig, species, "Gallop", frames)
    names = [bone.name for bone in rig.pose.bones]
    amp = 1.0
    tail_bal = profile["tailBalance"]
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        v4.reset_pose(rig)
        rotate(v4, rig, "Spine1", "z", amp * 0.08 * math.sin(phase))
        rotate(v4, rig, "Spine2", "z", amp * 0.12 * math.sin(phase - 0.4))
        rotate(v4, rig, "Spine3", "z", amp * 0.1 * math.sin(phase - 0.8))
        rotate(v4, rig, "Tail1", "z", amp * 0.28 * tail_bal * math.sin(phase - 1.1))
        rotate(v4, rig, "Tail2", "z", amp * 0.4 * tail_bal * math.sin(phase - 1.45))
        rotate(v4, rig, "Tail3", "z", amp * 0.48 * tail_bal * math.sin(phase - 1.8))
        rotate(v4, rig, "Tail4", "z", amp * 0.32 * tail_bal * math.sin(phase - 2.1))
        # Soft paddle so Front/Hind register limb motion without a gallop run.
        paddle = 0.28 * math.sin(phase + 0.6)
        for side, sign in (("L", 1.0), ("R", -1.0)):
            rotate(v4, rig, f"FrontShoulder.{side}", "x", sign * paddle * 0.55)
            rotate(v4, rig, f"HindUpper.{side}", "x", -sign * paddle * 0.45)
        rotate(v4, rig, "Head", "z", 0.02 * math.sin(phase + 0.3))
        set_root_world_z(rig, 0.12)
        v4.key_pose(rig, frame, names)
    return {
        "name": action.name,
        "frames": frames,
        "loop": True,
        "pattern": profile.get("gallopPattern", "swim_undulate"),
    }


def walk_offsets(pattern: str) -> dict[str, float]:
    if pattern == "lateral_couplet_walk":
        return {"Hind.L": 0.0, "Front.L": 0.35, "Hind.R": math.pi, "Front.R": math.pi + 0.35}
    return {
        "Hind.L": 0.0,
        "Front.L": math.pi * 0.5,
        "Hind.R": math.pi,
        "Front.R": math.pi * 1.5,
    }


def set_root_world_z(rig, world_z: float) -> None:
    """Translate Root so the armature rises on world +Z regardless of bone tilt.

    Sprawled reptiles lay the Root bone nearly horizontal, so pose-space Z is
    mostly world forward — raw (0,0,z) digs the mesh into the ground.
    """
    from mathutils import Vector

    bone = rig.pose.bones["Root"].bone
    local = bone.matrix_local.to_3x3().inverted() @ Vector((0.0, 0.0, float(world_z)))
    rig.pose.bones["Root"].location = (float(local.x), float(local.y), float(local.z))


def apply_limb_cycle(
    v4,
    rig,
    side: str,
    front: float,
    hind: float,
    scale: float,
    reach_sign: float = -1.0,
    reach_axis: str = "x",
    hind_reach_sign: float | None = None,
    *,
    hind_scale: float | None = None,
) -> None:
    # Drive from the shoulder/hip. Large FrontUpper/HindLower angles read as a
    # mid-leg snap instead of a real limb swing.
    axis = reach_axis
    hind_sign = reach_sign if hind_reach_sign is None else hind_reach_sign
    h_scale = scale if hind_scale is None else hind_scale
    rotate(v4, rig, f"FrontShoulder.{side}", axis, reach_sign * 0.34 * scale * front)
    rotate(v4, rig, f"FrontUpper.{side}", axis, reach_sign * 0.08 * scale * front)
    rotate(
        v4,
        rig,
        f"FrontLower.{side}",
        axis,
        -reach_sign * 0.05 * scale * max(front, 0.0),
    )
    # Hind always hinges on local X; use its own forward sign so a weird front
    # axis/sign cannot make the animal moonwalk.
    rotate(v4, rig, f"HindUpper.{side}", "x", hind_sign * 0.36 * h_scale * hind)
    rotate(
        v4,
        rig,
        f"HindLower.{side}",
        "x",
        -hind_sign * 0.05 * h_scale * max(hind, 0.0),
    )
    rotate(v4, rig, f"HindFoot.{side}", "x", hind_sign * 0.04 * h_scale * hind)


def author_walk(v4, rig, species: str, profile: dict) -> dict:
    family = profile["family"]
    if family == "avian":
        return author_flight(v4, rig, species, profile, label="Walk", intensity="cruise")
    if family == "piscine":
        return author_swim(v4, rig, species, profile, label="Walk", intensity="slow")
    if family == "reptile":
        return author_waddle(v4, rig, species, profile, label="Walk", intensity="slow")
    frames = 41
    action = create_action(v4, rig, species, "Walk", frames)
    names = [bone.name for bone in rig.pose.bones]
    offsets = walk_offsets(profile["walkPattern"])
    stealth = profile["walkPattern"] == "stealth_four_beat"
    heavy = (
        profile["walkPattern"] == "heavy_four_beat"
        or profile["family"] == "cervid_heavy"
    )
    if heavy:
        scale = 0.68
    elif profile["family"] == "cervid_light":
        scale = 0.78
    elif profile["family"] == "reptile":
        # Sprawled low bodies dig into the ground with mammal-scale swing.
        scale = 0.48
    elif stealth:
        scale = 0.66
    else:
        scale = 0.88
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        v4.reset_pose(rig)
        for side in ("L", "R"):
            front = math.sin(phase + offsets[f"Front.{side}"])
            hind = math.sin(phase + offsets[f"Hind.{side}"])
            apply_limb_cycle(
                v4,
                rig,
                side,
                front,
                hind,
                scale,
                reach_sign=profile.get("frontReachSign", -1.0),
                reach_axis=profile.get("frontReachAxis", "x"),
                hind_reach_sign=profile.get("hindReachSign"),
            )
        axial = profile["spineFlex"] * (0.1 if "cervid" in profile.get("family", "") else 0.18)
        if profile.get("family") == "bovine":
            axial *= 0.35
            neck_nod = 0.0
            head_nod = 0.0
        else:
            neck_nod = 0.004 * (1.0 - profile["headStability"]) * math.sin(phase + 0.7)
            head_nod = 0.003 * math.sin(phase + 1.1)
        rotate(v4, rig, "Spine1", "x", axial * math.sin(phase))
        rotate(v4, rig, "Spine3", "z", (0.0 if profile.get("family") == "bovine" else 0.008) * math.sin(phase))
        rotate(v4, rig, "Neck1", "x", neck_nod)
        rotate(v4, rig, "Head", "x", head_nod)
        rotate(v4, rig, "Tail2", "z", 0.035 * profile["tailBalance"] * math.sin(phase))
        root_lower = -0.018 if stealth else 0.0
        bounce = profile["rootBounce"]
        if "cervid" in profile.get("family", ""):
            bounce *= 0.55
        if profile.get("family") == "bovine":
            bounce *= 0.4
        root_z = root_lower + 0.006 * bounce * (1.0 - math.cos(phase * 4.0))
        if profile.get("family") == "reptile":
            # Quiet limbs alone still plant the belly under z=0; lift on world +Z.
            set_root_world_z(rig, 0.14 + root_z)
        else:
            rig.pose.bones["Root"].location = (0.0, 0.0, root_z)
        v4.key_pose(rig, frame, names)
    return {"name": action.name, "frames": frames, "loop": True, "pattern": profile["walkPattern"]}


def author_trot(v4, rig, species: str, profile: dict) -> dict:
    family = profile["family"]
    if family == "avian":
        return author_flight(v4, rig, species, profile, label="Trot", intensity="steady")
    if family == "piscine":
        return author_swim(v4, rig, species, profile, label="Trot", intensity="cruise")
    if family == "reptile":
        return author_waddle(v4, rig, species, profile, label="Trot", intensity="fast")
    frames = 29
    action = create_action(v4, rig, species, "Trot", frames)
    names = [bone.name for bone in rig.pose.bones]
    lateral = profile["trotPattern"] == "lateral_sequence_run"
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        v4.reset_pose(rig)
        if lateral:
            values = {
                "Front.L": math.sin(phase + 0.3),
                "Hind.L": math.sin(phase),
                "Front.R": math.sin(phase + math.pi + 0.3),
                "Hind.R": math.sin(phase + math.pi),
            }
        else:
            diagonal_a = math.sin(phase)
            diagonal_b = math.sin(phase + math.pi)
            values = {
                "Front.L": diagonal_a,
                "Hind.R": diagonal_a,
                "Front.R": diagonal_b,
                "Hind.L": diagonal_b,
            }
        limb_scale = 0.88 if profile["family"] in {"cervid_heavy", "bovine", "equid"} else 0.98
        if profile["family"] == "bovine":
            limb_scale = 0.72
            hind_scale = 0.6
        elif profile["family"] == "reptile":
            limb_scale = 0.52
            hind_scale = 0.48
        else:
            hind_scale = None
        for side in ("L", "R"):
            apply_limb_cycle(
                v4,
                rig,
                side,
                values[f"Front.{side}"],
                values[f"Hind.{side}"],
                limb_scale,
                reach_sign=profile.get("frontReachSign", -1.0),
                reach_axis=profile.get("frontReachAxis", "x"),
                hind_reach_sign=profile.get("hindReachSign"),
                hind_scale=hind_scale,
            )
        spine = profile["spineFlex"] * 0.22
        bounce = profile["rootBounce"]
        if profile["family"] == "bovine":
            spine *= 0.3
            bounce *= 0.35
            neck_nod = 0.0
            head_nod = 0.0
        else:
            neck_nod = 0.0
            head_nod = 0.0
        rotate(v4, rig, "Spine1", "x", spine * math.sin(phase * 2.0))
        rotate(v4, rig, "Neck1", "x", neck_nod)
        rotate(v4, rig, "Head", "x", head_nod)
        rotate(v4, rig, "Tail2", "z", 0.035 * profile["tailBalance"] * math.sin(phase))
        root_z = 0.008 * bounce * (1.0 - math.cos(phase * 2.0))
        if profile.get("family") == "reptile":
            set_root_world_z(rig, 0.16 + root_z)
        else:
            rig.pose.bones["Root"].location = (0.0, 0.0, root_z)
        v4.key_pose(rig, frame, names)
    return {"name": action.name, "frames": frames, "loop": True, "pattern": profile["trotPattern"]}


def author_gallop(v4, rig, species: str, profile: dict) -> dict:
    family = profile["family"]
    if family == "avian":
        return author_flight(v4, rig, species, profile, label="Gallop", intensity="power")
    if family == "piscine":
        return author_swim(v4, rig, species, profile, label="Gallop", intensity="dart")
    if family == "reptile":
        return author_reptile_swim(v4, rig, species, profile)
    frames = 23
    action = create_action(v4, rig, species, "Gallop", frames)
    names = [bone.name for bone in rig.pose.bones]
    pattern = profile["gallopPattern"]
    for frame in range(1, frames + 1):
        phase = loop_phase(frame, frames)
        v4.reset_pose(rig)
        if pattern == "fast_trot_lope":
            lead = 0.34
            front_l = math.sin(phase)
            front_r = math.sin(phase + math.pi + lead)
            hind_l = math.sin(phase + math.pi)
            hind_r = math.sin(phase + lead)
        else:
            side_lag = 0.1 if pattern == "elastic_bound" else 0.28
            front_l = math.sin(phase)
            front_r = math.sin(phase + side_lag)
            if pattern == "rotary_double_suspension":
                hind_l = math.sin(phase + math.pi + side_lag)
                hind_r = math.sin(phase + math.pi)
            else:
                hind_l = math.sin(phase + math.pi)
                hind_r = math.sin(phase + math.pi + side_lag)
        limb_scale = 1.02 if profile["family"] in {"cervid_heavy", "bovine", "equid"} else 1.12
        if profile["family"] == "bovine":
            # Heavy short-legged runners: quieter fores, even quieter hinds.
            limb_scale = 0.78
            hind_scale = 0.62
        elif profile["family"] == "reptile":
            limb_scale = 0.55
            hind_scale = 0.5
        elif species == "ram":
            # Quieter fores so residual chest↔shoulder weights don't pump the neck.
            limb_scale = 0.68
            hind_scale = 0.62
        else:
            hind_scale = None
        for side, front, hind in (
            ("L", front_l, hind_l),
            ("R", front_r, hind_r),
        ):
            apply_limb_cycle(
                v4,
                rig,
                side,
                front,
                hind,
                limb_scale,
                reach_sign=profile.get("frontReachSign", -1.0),
                reach_axis=profile.get("frontReachAxis", "x"),
                hind_reach_sign=profile.get("hindReachSign"),
                hind_scale=hind_scale,
            )
        gather = math.sin(phase)
        spine = profile["spineFlex"]
        # Cervids / bovines: keep gallop spine mild — residual dorsum weights
        # (withers, hump, bloom props) read as a back "eruption" when Spine1/2
        # counter-flex hard.
        if "cervid" in profile.get("family", "") or profile.get("family") == "bovine":
            spine *= 0.45
        # Buffalo / ram: no torso / head bob — legs carry the read; residual
        # Head/Spine weights on the brisket amplify any nod into chest thrashing.
        if species in {"buffalo", "ram"} or profile.get("family") == "bovine":
            spine = 0.0
        rotate(v4, rig, "Spine1", "x", spine * 0.4 * gather)
        rotate(v4, rig, "Spine2", "x", -spine * 0.32 * gather)
        rotate(v4, rig, "Spine3", "x", spine * 0.25 * gather)
        if species in {"buffalo", "ram"} or profile.get("family") == "bovine":
            # Lock head/neck in gallop — heavy runners shouldn't nod every stride.
            neck_nod = 0.0
            head_nod = 0.0
            bounce = 0.0
        else:
            neck_nod = -0.004 * (1.0 - profile["headStability"]) * gather
            head_nod = -0.002 * gather
            bounce = 0.018 * profile["rootBounce"]
            if pattern == "elastic_bound":
                bounce *= 1.15
            if (
                pattern == "rotary_double_suspension"
                and profile.get("family") in {"cervid_heavy", "bovine"}
            ):
                bounce *= 0.5
        rotate(v4, rig, "Neck1", "x", neck_nod)
        rotate(v4, rig, "Head", "x", head_nod)
        rotate(v4, rig, "Tail2", "x", 0.04 * profile["tailBalance"] * gather)
        rotate(v4, rig, "Tail3", "x", 0.05 * profile["tailBalance"] * gather)
        root_z = bounce * (1.0 - math.cos(phase))
        if profile.get("family") == "reptile":
            set_root_world_z(rig, 0.18 + root_z)
        else:
            rig.pose.bones["Root"].location = (0.0, 0.0, root_z)
        v4.key_pose(rig, frame, names)
    return {"name": action.name, "frames": frames, "loop": True, "pattern": pattern}


def pose_action(
    v4,
    rig,
    species: str,
    label: str,
    frames: int,
    poses,
    note: str,
    *,
    blend_mode: str = "smoothstep",
) -> dict:
    action = create_action(v4, rig, species, label, frames)
    names = [bone.name for bone in rig.pose.bones]
    for frame in range(1, frames + 1):
        v4.reset_pose(rig)
        rotations, root_location = v4.sample_action_pose(
            frame,
            frames,
            poses,
            blend_mode=blend_mode,
        )
        for name, (axis, angle) in rotations.items():
            rotate(v4, rig, name, axis, angle)
        rig.pose.bones["Root"].location = root_location
        v4.key_pose(rig, frame, names)
    return {
        "name": action.name,
        "frames": frames,
        "loop": False,
        "note": note,
        "blendMode": blend_mode,
    }


def detect_head_lower_sign(v4, rig, target=None) -> float:
    """Return the local pitch sign that lowers the snout/horns (mesh when available).

    Short Tripo Head bones often disagree with the visual snout — bone-tip
    lowering can raise the beard/face mesh. Prefer evaluated Head-weighted snout.
    For curled-horn meshes (ram), prefer lowering the horn crown — snout-only
    scoring tips the chin up and reads as a neck stretch.
    """
    import bpy

    snout_indices = []
    horn_indices = []
    if target is not None and len(target.data.vertices):
        head_group = target.vertex_groups.get("Head")
        jaw_group = target.vertex_groups.get("Jaw")
        head_idx = head_group.index if head_group else -1
        jaw_idx = jaw_group.index if jaw_group else -1
        head_bone = rig.data.bones.get("Head")
        crown_z = (
            max(float(head_bone.head_local.z), float(head_bone.tail_local.z)) + 0.04
            if head_bone is not None
            else 1.0e9
        )
        ys = sorted(v.co.y for v in target.data.vertices)
        y_cut = ys[max(0, int(len(ys) * 0.15))]
        for vertex in target.data.vertices:
            axial_w = 0.0
            head_w = 0.0
            for assignment in vertex.groups:
                if assignment.group == head_idx:
                    head_w = assignment.weight
                    axial_w += assignment.weight
                elif assignment.group == jaw_idx:
                    axial_w += assignment.weight
            if (
                head_w >= 0.45
                and vertex.co.z >= crown_z
                and abs(vertex.co.x) >= 0.05
            ):
                horn_indices.append(vertex.index)
            if vertex.co.y > y_cut:
                continue
            if axial_w >= 0.35:
                snout_indices.append(vertex.index)
        if len(snout_indices) < 6:
            # Fallback: most-forward verts regardless of weight.
            snout_indices = [
                v.index for v in target.data.vertices if v.co.y <= y_cut
            ]

    use_horns = len(horn_indices) >= 8

    def snout_metrics():
        if not snout_indices:
            tip = rig.pose.bones["Head"].tail
            return float(tip.z), float(tip.y), float(tip.z)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = target.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            samples = [mesh.vertices[i].co for i in snout_indices]
            avg_z = sum(p.z for p in samples) / len(samples)
            # Most-forward tip among Head/Jaw samples (facing -Y).
            tip = min(samples, key=lambda p: p.y)
            return avg_z, tip.y, tip.z
        finally:
            evaluated.to_mesh_clear()

    def horn_metrics():
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = target.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            samples = [mesh.vertices[i].co for i in horn_indices]
            avg_z = sum(p.z for p in samples) / len(samples)
            # Most-forward horn curl sample (facing -Y).
            tip = min(samples, key=lambda p: p.y)
            max_z = max(p.z for p in samples)
            return avg_z, tip.y, tip.z, max_z
        finally:
            evaluated.to_mesh_clear()

    rig.data.pose_position = "POSE"
    if rig.animation_data is not None:
        rig.animation_data.action = None

    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    rest_avg_z, rest_tip_y, rest_tip_z = snout_metrics()
    rest_horn = horn_metrics() if use_horns else None
    scores = {}
    for sign in (1.0, -1.0):
        v4.reset_pose(rig)
        rotate(v4, rig, "Neck1", "x", 0.28 * sign)
        rotate(v4, rig, "Head", "x", 0.22 * sign)
        bpy.context.view_layer.update()
        if use_horns and rest_horn is not None:
            avg_z, tip_y, tip_z, max_z = horn_metrics()
            # Prefer: forward-most horn face lowers and advances (-Y).
            # Do NOT score on max-Z crown — curled ram horns drop their high
            # spiral on the wrong pitch sign while the ramming face lifts.
            forward = rest_horn[1] - tip_y
            tip_lower = rest_horn[2] - tip_z
            avg_lower = rest_horn[0] - avg_z
            scores[sign] = forward * 2.4 + tip_lower * 2.6 + avg_lower * 1.2
        else:
            avg_z, tip_y, tip_z = snout_metrics()
            # Prefer: tip moves forward (-Y), tip/face lowers (+Z down).
            forward = rest_tip_y - tip_y
            lower = rest_avg_z - avg_z
            tip_lower = rest_tip_z - tip_z
            scores[sign] = forward * 2.0 + lower * 1.2 + tip_lower * 1.5
    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    return max(scores, key=scores.get)


def detect_ram_horn_charge_sign(v4, rig, target) -> float:
    """Pitch sign that drops the forward horn face for a ram charge.

    Curled horns make max-Z / crown scoring pick the wrong pitch — the spiral
    crest drops while the ramming face lifts. Score the most-forward horn pad.
    """
    import bpy

    if target is None or "Head" not in rig.data.bones:
        return -1.0
    head = rig.data.bones["Head"]
    neck = rig.data.bones.get("Neck1")
    neck_y = float(neck.head_local.y) if neck is not None else float(head.head_local.y) + 0.2
    head_group = target.vertex_groups.get("Head")
    head_idx = head_group.index if head_group is not None else -1
    candidates = []
    for vertex in target.data.vertices:
        if abs(vertex.co.x) < 0.08:
            continue
        if vertex.co.y > neck_y + 0.05:
            continue
        if vertex.co.z < float(head.head_local.z) - 0.08:
            continue
        head_w = 0.0
        for assignment in vertex.groups:
            if assignment.group == head_idx:
                head_w = assignment.weight
                break
        if head_w < 0.45:
            continue
        candidates.append(vertex.index)
    if len(candidates) < 6:
        return detect_head_lower_sign(v4, rig, target=target)
    candidates.sort(key=lambda index: target.data.vertices[index].co.y)
    face = candidates[: max(6, len(candidates) // 4)]

    def face_metrics():
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = target.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            samples = [mesh.vertices[index].co for index in face]
            return (
                sum(p.y for p in samples) / len(samples),
                sum(p.z for p in samples) / len(samples),
            )
        finally:
            evaluated.to_mesh_clear()

    rig.data.pose_position = "POSE"
    if rig.animation_data is not None:
        rig.animation_data.action = None
    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    rest_y, rest_z = face_metrics()
    scores = {}
    for sign in (1.0, -1.0):
        v4.reset_pose(rig)
        rotate(v4, rig, "Neck1", "x", 0.12 * sign)
        rotate(v4, rig, "Head", "x", 0.28 * sign)
        bpy.context.view_layer.update()
        posed_y, posed_z = face_metrics()
        forward = rest_y - posed_y
        lower = rest_z - posed_z
        scores[sign] = forward * 1.5 + lower * 3.0
    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    return max(scores, key=scores.get)


def detect_front_reach(v4, rig, target=None) -> dict:
    """Pick FrontUpper axis+sign that swings the forepaw mesh toward -Y."""
    import bpy
    from mathutils import Vector

    rig.data.pose_position = "POSE"
    if rig.animation_data is not None:
        rig.animation_data.action = None

    upper = rig.pose.bones.get("FrontUpper.L")
    lower = rig.pose.bones.get("FrontLower.L")
    if upper is None:
        return {"axis": "x", "sign": -1.0, "score": 0.0, "pawSamples": 0}

    # Paw only — FrontUpper/shoulder weights on Tripo binds include chest verts
    # that drift -Y on a tuck and falsely win the forward score.
    paw_indices = []
    if target is not None and "FrontLower.L" in {g.name for g in target.vertex_groups}:
        lower_bone = rig.data.bones.get("FrontLower.L") or rig.data.bones.get("FrontUpper.L")
        tip = lower_bone.tail_local if lower_bone is not None else None
        zs = [vertex.co.z for vertex in target.data.vertices]
        z_cut = percentile(zs, 0.28) if zs else 0.0
        for vertex in target.data.vertices:
            if vertex.co.z > z_cut:
                continue
            weight = 0.0
            for assignment in vertex.groups:
                if target.vertex_groups[assignment.group].name == "FrontLower.L":
                    weight = assignment.weight
                    break
            if weight < 0.45:
                continue
            if tip is not None and (vertex.co - tip).length > 0.28:
                continue
            paw_indices.append(vertex.index)
    if not paw_indices and target is not None:
        # Fallback: lowest FrontLower-weighted verts.
        scored = []
        for vertex in target.data.vertices:
            weight = 0.0
            for assignment in vertex.groups:
                if target.vertex_groups[assignment.group].name == "FrontLower.L":
                    weight = assignment.weight
                    break
            if weight >= 0.35:
                scored.append((vertex.co.z, vertex.index))
        scored.sort()
        paw_indices = [index for _z, index in scored[: max(6, len(scored) // 5)]]

    def bone_tip_y(bone):
        return (rig.matrix_world @ (bone.matrix @ Vector((0.0, bone.length, 0.0)))).y

    def paw_metric():
        if not paw_indices:
            tip = lower or upper
            point = rig.matrix_world @ (tip.matrix @ Vector((0.0, tip.length, 0.0)))
            return point.y, max(0.0, point.z)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = target.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            samples = [
                (mesh.vertices[index].co.y, mesh.vertices[index].co.z)
                for index in paw_indices
            ]
            samples.sort(key=lambda item: item[0])
            count = max(1, len(samples) // 4)
            front = samples[:count]
            return (
                sum(item[0] for item in front) / count,
                sum(item[1] for item in front) / count,
            )
        finally:
            evaluated.to_mesh_clear()

    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    rest_y, rest_z = paw_metric()
    best = None
    # Prefer shoulder-driven probes — matching attack authorship.
    for axis in ("x", "z", "y"):
        for sign in (1.0, -1.0):
            v4.reset_pose(rig)
            rotate(v4, rig, "FrontShoulder.L", axis, 0.55 * sign)
            rotate(v4, rig, "FrontUpper.L", axis, 0.12 * sign)
            bpy.context.view_layer.update()
            posed_y, posed_z = paw_metric()
            # Forward = more -Y. Mild lift penalty; heavy rear tuck penalty.
            score = (posed_y - rest_y) + 0.45 * max(0.0, posed_z - rest_z)
            if posed_y > rest_y + 0.01:
                score += 0.35 * (posed_y - rest_y)
            if best is None or score < best["score"]:
                best = {
                    "axis": axis,
                    "sign": sign,
                    "score": score,
                    "deltaY": posed_y - rest_y,
                    "pawSamples": len(paw_indices),
                }
    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    return best or {"axis": "x", "sign": -1.0, "score": 0.0, "pawSamples": 0}


def detect_front_reach_sign(v4, rig, target=None) -> float:
    """Backward-compatible sign helper for locomotion callers."""
    return detect_front_reach(v4, rig, target=target)["sign"]


def detect_hind_reach(v4, rig, target=None) -> dict:
    """Pick HindUpper X sign that swings the hoof mesh toward -Y (forward)."""
    import bpy
    from mathutils import Vector

    rig.data.pose_position = "POSE"
    if rig.animation_data is not None:
        rig.animation_data.action = None

    upper = rig.pose.bones.get("HindUpper.L")
    foot = rig.pose.bones.get("HindFoot.L")
    if upper is None or foot is None:
        return {"axis": "x", "sign": -1.0, "score": 0.0, "hoofSamples": 0}

    hoof_indices = []
    if target is not None and target.vertex_groups.get("HindFoot.L") is not None:
        tip = rig.data.bones["HindFoot.L"].tail_local
        zs = [vertex.co.z for vertex in target.data.vertices]
        z_cut = percentile(zs, 0.32) if zs else 0.0
        for vertex in target.data.vertices:
            if vertex.co.z > z_cut:
                continue
            weight = 0.0
            for assignment in vertex.groups:
                if target.vertex_groups[assignment.group].name == "HindFoot.L":
                    weight = assignment.weight
                    break
            if weight < 0.4:
                continue
            if (vertex.co - tip).length > 0.32:
                continue
            hoof_indices.append(vertex.index)

    def hoof_y():
        if not hoof_indices:
            point = rig.matrix_world @ (foot.matrix @ Vector((0.0, foot.length, 0.0)))
            return point.y
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = target.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            samples = [mesh.vertices[index].co.y for index in hoof_indices]
            samples.sort()
            count = max(1, len(samples) // 4)
            # Most -Y samples = forward edge of the hoof cluster.
            return sum(samples[:count]) / count
        finally:
            evaluated.to_mesh_clear()

    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    rest_y = hoof_y()
    best = None
    for sign in (1.0, -1.0):
        v4.reset_pose(rig)
        rotate(v4, rig, "HindUpper.L", "x", 0.45 * sign)
        rotate(v4, rig, "HindLower.L", "x", -0.1 * sign)
        bpy.context.view_layer.update()
        posed_y = hoof_y()
        score = posed_y - rest_y
        if best is None or score < best["score"]:
            best = {
                "axis": "x",
                "sign": sign,
                "score": score,
                "deltaY": posed_y - rest_y,
                "hoofSamples": len(hoof_indices),
            }
    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    return best or {"axis": "x", "sign": -1.0, "score": 0.0, "hoofSamples": 0}


def detect_horn_bear_angles(v4, rig, lower_sign: float, family: str = "") -> dict:
    """Find a dramatic headbutt tip: crowns bear forward, Neck1 stays secondary.

    Antlers/horns are rigid on Head, so the readable charge is a hard skull nod
    (and a light neck flex) — not cranking the whole neck column.
    """
    import bpy

    head = rig.pose.bones["Head"]
    best = None
    cervid = "cervid" in family
    amount_range = range(16, 30) if cervid else range(18, 32)
    for amount in (index * 0.05 for index in amount_range):
        v4.reset_pose(rig)
        neck = lower_sign * amount * 0.22
        skull = lower_sign * amount * 0.92
        spine = lower_sign * amount * 0.12
        rotate(v4, rig, "Spine2", "x", spine * 0.3)
        rotate(v4, rig, "Spine3", "x", spine)
        rotate(v4, rig, "Neck1", "x", neck)
        rotate(v4, rig, "Head", "x", skull)
        bpy.context.view_layer.update()
        direction = (rig.matrix_world @ head.tail) - (rig.matrix_world @ head.head)
        if direction.length < 1.0e-8:
            continue
        direction.normalize()
        # Crown-bearing charge: snout clearly down / antlers forward.
        target_z = -0.78 if cervid else -0.85
        score = (
            abs(direction.z - target_z) * 1.5
            + max(0.0, direction.y - 0.12) * 2.6
            + max(0.0, -direction.y - 0.75) * 0.7
            + abs(direction.x) * 0.2
            + abs(neck) * 0.4
            - amount * 0.05
        )
        if best is None or score < best["score"]:
            best = {
                "amount": amount,
                "score": score,
                "neck": neck,
                "head": skull,
                "spine": spine,
                "snoutDirection": [float(direction.x), float(direction.y), float(direction.z)],
            }
    v4.reset_pose(rig)
    bpy.context.view_layer.update()
    if best is None:
        amount = 1.0
        return {
            "amount": amount,
            "score": 1.0,
            "neck": lower_sign * amount * 0.22,
            "head": lower_sign * amount * 0.92,
            "spine": lower_sign * amount * 0.12,
            "snoutDirection": [0.0, -0.35, -0.9],
        }
    # Dramatic Head tip; Neck stays supporting, not rubber-banding.
    max_neck = 0.22 if cervid else 0.2
    max_head = 0.95 if cervid else 0.85
    max_spine = 0.12 if cervid else 0.1
    sign = 1.0 if best["neck"] >= 0.0 else -1.0
    best["neck"] = sign * min(abs(best["neck"]), max_neck)
    best["head"] = sign * min(abs(best["head"]), max_head)
    best["spine"] = sign * min(abs(best["spine"]), max_spine)
    best["cappedNeck"] = True
    return best


def author_attack(v4, rig, species: str, profile: dict, target_height: float) -> dict:
    # Keep attacks planted. Root translation reads as the whole animal teleporting.
    planted = (0.0, 0.0, 0.0)
    rest = {}
    style = profile["attackStyle"]
    if style == "talon_strike":
        # Eagle: talons reach / curl — no running lunge.
        poses = [
            (0.0, rest, planted),
            (
                0.18,
                {
                    "Spine3": ("x", 0.04),
                    "Neck1": ("x", 0.06),
                    "Head": ("x", 0.04),
                    "HindUpper.L": ("x", -0.12),
                    "HindUpper.R": ("x", -0.14),
                    "HindLower.L": ("x", 0.18),
                    "HindLower.R": ("x", 0.2),
                    "HindFoot.L": ("x", 0.28),
                    "HindFoot.R": ("x", 0.32),
                    "FrontShoulder.L": ("y", 0.08),
                    "FrontShoulder.R": ("y", -0.08),
                },
                planted,
            ),
            (
                0.45,
                {
                    "Spine3": ("x", 0.06),
                    "Neck1": ("x", 0.08),
                    "Head": ("x", 0.05),
                    "HindUpper.L": ("x", -0.22),
                    "HindUpper.R": ("x", -0.24),
                    "HindLower.L": ("x", 0.32),
                    "HindLower.R": ("x", 0.34),
                    "HindFoot.L": ("x", 0.48),
                    "HindFoot.R": ("x", 0.52),
                    "FrontShoulder.L": ("y", 0.12),
                    "FrontShoulder.R": ("y", -0.12),
                },
                planted,
            ),
            (
                0.72,
                {
                    "HindUpper.L": ("x", -0.1),
                    "HindUpper.R": ("x", -0.12),
                    "HindFoot.L": ("x", 0.2),
                    "HindFoot.R": ("x", 0.22),
                },
                planted,
            ),
            (1.0, rest, planted),
        ]
        return pose_action(
            v4, rig, species, "Attack", 33, poses, "talon strike", blend_mode="smoothstep"
        )
    if style == "jaw_gape":
        # Alligator: mouth opens — no pounce.
        poses = [
            (0.0, rest, planted),
            (
                0.2,
                {
                    "Neck1": ("x", -0.06),
                    "Head": ("x", -0.04),
                    "Jaw": ("x", 0.35),
                },
                planted,
            ),
            (
                0.48,
                {
                    "Neck1": ("x", -0.1),
                    "Head": ("x", -0.06),
                    "Jaw": ("x", 0.72),
                    "Spine3": ("x", 0.03),
                },
                planted,
            ),
            (
                0.78,
                {
                    "Jaw": ("x", 0.2),
                    "Neck1": ("x", -0.04),
                },
                planted,
            ),
            (1.0, rest, planted),
        ]
        return pose_action(
            v4, rig, species, "Attack", 37, poses, "jaw gape", blend_mode="smoothstep"
        )
    if style == "snap_bite":
        # Salmon: quick jaw snap + body flick.
        poses = [
            (0.0, rest, planted),
            (
                0.16,
                {
                    "Spine2": ("z", -0.06),
                    "Spine3": ("z", -0.08),
                    "Head": ("x", -0.05),
                    "Jaw": ("x", 0.28),
                },
                planted,
            ),
            (
                0.4,
                {
                    "Spine2": ("z", 0.1),
                    "Spine3": ("z", 0.12),
                    "Tail2": ("z", -0.16),
                    "Head": ("x", -0.08),
                    "Jaw": ("x", 0.55),
                },
                planted,
            ),
            (
                0.7,
                {
                    "Spine3": ("z", 0.04),
                    "Jaw": ("x", 0.12),
                },
                planted,
            ),
            (1.0, rest, planted),
        ]
        return pose_action(
            v4, rig, species, "Attack", 29, poses, "snap bite", blend_mode="smoothstep"
        )
    if style in {"bite_lunge", "pounce_bite"}:
        # Detector returns axis+sign that moves the paw toward -Y (forward).
        reach = profile.get("frontReachSign", -1.0)
        axis = profile.get("frontReachAxis", "x")
        pounce = style == "pounce_bite"
        # Swing the whole forelimb from the shoulder as one piece. Avoid large
        # FrontUpper/FrontLower counters that open a gap at the wrist.
        # Panther reference: bold pounce — chest reclaim keeps the brisket from
        # riding the shoulder, so we can push amplitude hard.
        if pounce and species == "panther":
            peak = 0.64
            poses = [
                (0.0, rest, planted),
                (
                    0.1,
                    {
                        "Spine2": ("x", 0.04),
                        "Spine3": ("x", 0.06),
                        "Neck1": ("x", 0.08),
                        "Head": ("x", 0.05),
                        "Jaw": ("x", 0.32),
                        "FrontShoulder.L": (axis, reach * 0.28),
                        "FrontShoulder.R": (axis, reach * 0.3),
                    },
                    planted,
                ),
                (
                    0.32,
                    {
                        "Spine2": ("x", 0.08),
                        "Spine3": ("x", 0.1),
                        "Neck1": ("x", -0.22),
                        "Head": ("x", -0.16),
                        "Jaw": ("x", 0.55),
                        "FrontShoulder.L": (axis, reach * (peak * 0.88)),
                        "FrontShoulder.R": (axis, reach * (peak * 0.94)),
                        "FrontUpper.L": (axis, -reach * 0.05),
                        "FrontUpper.R": (axis, -reach * 0.06),
                    },
                    planted,
                ),
                (
                    0.5,
                    {
                        "Spine2": ("x", 0.1),
                        "Spine3": ("x", 0.12),
                        "Neck1": ("x", -0.28),
                        "Head": ("x", -0.2),
                        "Jaw": ("x", 0.08),
                        "FrontShoulder.L": (axis, reach * peak),
                        "FrontShoulder.R": (axis, reach * (peak * 1.06)),
                        "FrontUpper.L": (axis, -reach * 0.07),
                        "FrontUpper.R": (axis, -reach * 0.08),
                    },
                    planted,
                ),
                (
                    0.72,
                    {
                        "Spine3": ("x", 0.04),
                        "Neck1": ("x", -0.08),
                        "Jaw": ("x", 0.1),
                        "FrontShoulder.L": (axis, reach * 0.22),
                        "FrontShoulder.R": (axis, reach * 0.24),
                    },
                    planted,
                ),
                (1.0, rest, planted),
            ]
            note = "reference whole-leg pounce-bite"
        else:
            peak = 0.5 if pounce else 0.44
            poses = [
                (0.0, rest, planted),
                (
                    0.12,
                    {
                        "Spine3": ("x", 0.02),
                        "Neck1": ("x", 0.05),
                        "Head": ("x", 0.03),
                        "Jaw": ("x", 0.24),
                        "FrontShoulder.L": (axis, reach * 0.2),
                        "FrontShoulder.R": (axis, reach * 0.22),
                    },
                    planted,
                ),
                (
                    0.34,
                    {
                        "Spine3": ("x", 0.03),
                        "Neck1": ("x", -0.14),
                        "Head": ("x", -0.1),
                        "Jaw": ("x", 0.42),
                        "FrontShoulder.L": (axis, reach * (peak * 0.9)),
                        "FrontShoulder.R": (axis, reach * (peak * 0.95)),
                        "FrontUpper.L": (axis, -reach * 0.06),
                        "FrontUpper.R": (axis, -reach * 0.07),
                    },
                    planted,
                ),
                (
                    0.5,
                    {
                        "Spine3": ("x", 0.035),
                        "Neck1": ("x", -0.18),
                        "Head": ("x", -0.12),
                        "Jaw": ("x", 0.05),
                        "FrontShoulder.L": (axis, reach * peak),
                        "FrontShoulder.R": (axis, reach * (peak * 1.05)),
                        "FrontUpper.L": (axis, -reach * 0.08),
                        "FrontUpper.R": (axis, -reach * 0.09),
                    },
                    planted,
                ),
                (
                    0.72,
                    {
                        "Neck1": ("x", -0.05),
                        "Jaw": ("x", 0.06),
                        "FrontShoulder.L": (axis, reach * 0.16),
                        "FrontShoulder.R": (axis, reach * 0.18),
                    },
                    planted,
                ),
                (1.0, rest, planted),
            ]
            note = (
                "planted whole-leg pounce-bite"
                if pounce
                else "planted whole-leg bite-lunge"
            )
    elif style == "head_charge":
        lower = profile["headLowerSign"]
        family_name = profile.get("family", "")
        bear = detect_horn_bear_angles(v4, rig, lower, family=family_name)
        # Reference lean tip (stag quality bar): tip Neck1 + Head together a little.
        # No big Head-only yank — that reads as the neck extending.
        sign = 1.0 if bear["neck"] >= 0.0 else -1.0
        if species == "stag":
            # Herbivore reference: neck+head nod only — no Spine2/3 arch.
            # Spine flex on a cervid withers plate reads as a giant back hump.
            neck_amt, head_amt, spine_amt = 0.28, 0.16, 0.0
            note = "reference lean headbutt"
        elif species == "ram":
            # Head-led ram: horns ride the skull. Keep Neck1 quiet so the charge
            # does not read as the neck stretching upward on windup.
            neck_amt, head_amt, spine_amt = 0.1, 0.24, 0.0
            note = "horn-ram head charge"
        elif species in {"deer", "elk", "moose"}:
            neck_amt, head_amt, spine_amt = 0.32, 0.18, 0.03
            note = "planted lean headbutt"
        elif family_name == "bovine" or species in {"buffalo", "bull", "cow"}:
            # Subtle Neck1-led lean (clears QA head floor 0.12). Large Head/Jaw
            # tips stretch the beard off the Spine3 neck on Tripo bison meshes.
            neck_amt, head_amt, spine_amt = 0.14, 0.08, 0.0
            note = "subtle planted lean head charge"
        else:
            neck_amt, head_amt, spine_amt = 0.3, 0.2, 0.08
            note = "planted lean headbutt"
        bear = {
            **bear,
            "amount": max(abs(bear["amount"]), 0.55),
            "neck": sign * neck_amt,
            "head": sign * head_amt,
            "spine": sign * spine_amt,
            "forcedHeadbutt": True,
            "leanTip": True,
            "referenceQuality": species == "stag",
        }
        neck = bear["neck"]
        skull = bear["head"]
        spine = bear["spine"]
        # Small windup lift, then lean the column down together.
        if species == "ram":
            # Minimal windup — large opposite Neck1 reads as the neck lifting.
            lift_neck = -0.12 * neck
            lift_head = -0.18 * skull
        else:
            lift_neck = -0.4 * neck
            lift_head = -0.35 * skull
        if (
            species in {"stag", "ram"}
            or family_name == "bovine"
            or abs(spine) < 1.0e-6
        ):
            poses = [
                (0.0, rest, planted),
                (
                    0.14,
                    {
                        "Neck1": ("x", lift_neck),
                        "Head": ("x", lift_head),
                    },
                    planted,
                ),
                (
                    0.34,
                    {
                        "Neck1": ("x", neck * 0.75),
                        "Head": ("x", skull * 0.7),
                    },
                    planted,
                ),
                (
                    0.52,
                    {
                        "Neck1": ("x", neck * 1.0),
                        "Head": ("x", skull * 1.0),
                    },
                    planted,
                ),
                (
                    0.7,
                    {
                        "Neck1": ("x", neck * 0.7),
                        "Head": ("x", skull * 0.75),
                    },
                    planted,
                ),
                (
                    0.86,
                    {
                        "Neck1": ("x", neck * 0.25),
                        "Head": ("x", skull * 0.28),
                    },
                    planted,
                ),
                (1.0, rest, planted),
            ]
        else:
            poses = [
                (0.0, rest, planted),
                (
                    0.14,
                    {
                        "Neck1": ("x", lift_neck),
                        "Head": ("x", lift_head),
                    },
                    planted,
                ),
                (
                    0.34,
                    {
                        "Spine2": ("x", spine * 0.4),
                        "Spine3": ("x", spine * 0.7),
                        "Neck1": ("x", neck * 0.75),
                        "Head": ("x", skull * 0.7),
                    },
                    planted,
                ),
                (
                    0.52,
                    {
                        "Spine2": ("x", spine * 0.55),
                        "Spine3": ("x", spine * 0.9),
                        "Neck1": ("x", neck * 1.0),
                        "Head": ("x", skull * 1.0),
                    },
                    planted,
                ),
                (
                    0.7,
                    {
                        "Spine3": ("x", spine * 0.55),
                        "Neck1": ("x", neck * 0.7),
                        "Head": ("x", skull * 0.75),
                    },
                    planted,
                ),
                (
                    0.86,
                    {
                        "Neck1": ("x", neck * 0.25),
                        "Head": ("x", skull * 0.28),
                        "Spine3": ("x", spine * 0.18),
                    },
                    planted,
                ),
                (1.0, rest, planted),
            ]
    elif style == "hind_kick":
        # Both hinds together — real defensive double kick. Keep amplitude firm
        # but not violent; hinge from hip (HindUpper), not mid-cannon.
        poses = [
            (0.0, rest, planted),
            (
                0.22,
                {
                    "Neck1": ("x", -0.08),
                    "Head": ("x", -0.05),
                    "FrontShoulder.L": ("x", -0.08),
                    "FrontShoulder.R": ("x", -0.08),
                    "HindUpper.L": ("x", 0.14),
                    "HindUpper.R": ("x", 0.14),
                },
                planted,
            ),
            (
                0.46,
                {
                    "Spine1": ("x", 0.08),
                    "HindUpper.L": ("x", -0.48),
                    "HindUpper.R": ("x", -0.44),
                    "HindLower.L": ("x", 0.28),
                    "HindLower.R": ("x", 0.26),
                    "HindFoot.L": ("x", -0.16),
                    "HindFoot.R": ("x", -0.14),
                },
                planted,
            ),
            (
                0.62,
                {
                    "HindUpper.L": ("x", -0.2),
                    "HindUpper.R": ("x", -0.18),
                    "HindLower.L": ("x", 0.14),
                    "HindLower.R": ("x", 0.12),
                },
                planted,
            ),
            (1.0, rest, planted),
        ]
        note = "planted defensive double hind kick"
    else:
        poses = [
            (0.0, rest, planted),
            (
                0.25,
                {
                    "Neck1": ("x", 0.1),
                    "Head": ("x", 0.07),
                    "FrontShoulder.L": ("x", -0.22),
                    "FrontUpper.L": ("x", -0.06),
                },
                planted,
            ),
            (
                0.48,
                {
                    "Neck1": ("x", -0.12),
                    "Head": ("x", -0.08),
                    "FrontShoulder.L": ("x", 0.32),
                    "FrontUpper.L": ("x", 0.08),
                    "FrontLower.L": ("x", -0.1),
                },
                planted,
            ),
            (
                0.66,
                {
                    "FrontShoulder.L": ("x", 0.1),
                    "FrontUpper.L": ("x", 0.04),
                },
                planted,
            ),
            (1.0, rest, planted),
        ]
        note = "planted camelid warning stomp"
    report = pose_action(v4, rig, species, "Attack", 48, poses, note)
    report["style"] = style
    report["rootLocked"] = True
    if style in {"bite_lunge", "pounce_bite"}:
        report["frontReachSign"] = profile.get("frontReachSign", -1.0)
        report["frontReachAxis"] = profile.get("frontReachAxis", "x")
        report["frontReach"] = profile.get("frontReach")
    if style == "head_charge":
        report["headLowerSign"] = profile["headLowerSign"]
        report["hornBear"] = bear
    return report


def author_death(v4, rig, species: str, target_height: float) -> dict:
    """Continuous side fall: no mid-key smoothstep pause, brief settle hold."""

    scale = target_height / 1.15
    rest = {}
    stagger = {
        "Root": ("z", -0.14),
        "Spine2": ("x", -0.05),
        "Spine3": ("x", -0.06),
        "Neck1": ("x", 0.1),
        "Head": ("x", 0.08),
        "Jaw": ("x", 0.08),
        "FrontUpper.L": ("x", -0.12),
        "HindUpper.R": ("x", 0.12),
    }
    tipping = {
        "Root": ("z", 0.38),
        "Spine2": ("x", -0.06),
        "Spine3": ("x", -0.08),
        "Neck1": ("x", -0.08),
        "Head": ("x", -0.06),
        "Jaw": ("x", 0.12),
        "FrontUpper.L": ("x", -0.18),
        "FrontUpper.R": ("x", -0.14),
        "FrontLower.L": ("x", 0.14),
        "FrontLower.R": ("x", 0.12),
        "HindUpper.L": ("x", 0.14),
        "HindUpper.R": ("x", 0.12),
        "HindLower.L": ("x", -0.16),
        "HindLower.R": ("x", -0.14),
        "Tail1": ("x", -0.06),
    }
    falling = {
        "Root": ("z", 0.95),
        "Spine2": ("x", -0.07),
        "Spine3": ("x", -0.1),
        "Neck1": ("x", -0.18),
        "Head": ("x", -0.14),
        "Jaw": ("x", 0.14),
        "FrontUpper.L": ("x", -0.26),
        "FrontUpper.R": ("x", -0.22),
        "FrontLower.L": ("x", 0.22),
        "FrontLower.R": ("x", 0.18),
        "HindUpper.L": ("x", 0.22),
        "HindUpper.R": ("x", 0.18),
        "HindLower.L": ("x", -0.26),
        "HindLower.R": ("x", -0.22),
        "HindFoot.L": ("x", 0.12),
        "HindFoot.R": ("x", 0.1),
        "Tail1": ("x", -0.1),
        "Tail2": ("x", -0.08),
    }
    settled = {
        "Root": ("z", 1.42),
        "Spine2": ("x", -0.08),
        "Spine3": ("x", -0.12),
        "Neck1": ("x", -0.28),
        "Head": ("x", -0.22),
        "Jaw": ("x", 0.16),
        "FrontShoulder.L": ("x", -0.1),
        "FrontShoulder.R": ("x", -0.08),
        "FrontUpper.L": ("x", -0.36),
        "FrontUpper.R": ("x", -0.3),
        "FrontLower.L": ("x", 0.32),
        "FrontLower.R": ("x", 0.28),
        "HindUpper.L": ("x", 0.34),
        "HindUpper.R": ("x", 0.28),
        "HindLower.L": ("x", -0.38),
        "HindLower.R": ("x", -0.32),
        "HindFoot.L": ("x", 0.18),
        "HindFoot.R": ("x", 0.14),
        "Tail1": ("x", -0.14),
        "Tail2": ("x", -0.1),
    }
    # Linear blend keeps tip velocity continuous across mid-fall keys.
    # Side-lying is reached near 78%, then only a short settle hold remains.
    poses = [
        (0.0, rest, (0.0, 0.0, 0.0)),
        (0.08, stagger, (0.03 * scale, 0.015 * scale, 0.0)),
        (0.28, tipping, (0.1 * scale, 0.015 * scale, -0.08 * scale)),
        (0.52, falling, (0.2 * scale, 0.01 * scale, -0.2 * scale)),
        (0.78, settled, (0.32 * scale, 0.01 * scale, -0.32 * scale)),
        (1.0, settled, (0.34 * scale, 0.01 * scale, -0.34 * scale)),
    ]
    return pose_action(
        v4,
        rig,
        species,
        "Death",
        71,
        poses,
        "continuous side fall then short settle hold",
        blend_mode="linear",
    )


def author_actions(
    v4, rig, species: str, family_name: str, family: dict, target_height: float, target=None
):
    profile = dict(family)
    profile["family"] = family_name
    profile["headLowerSign"] = detect_head_lower_sign(v4, rig, target=target)
    if species == "ram" and target is not None:
        profile["headLowerSign"] = detect_ram_horn_charge_sign(v4, rig, target)
    front_reach = detect_front_reach(v4, rig, target=target)
    profile["frontReachSign"] = front_reach["sign"]
    profile["frontReachAxis"] = front_reach["axis"]
    profile["frontReach"] = front_reach
    hind_reach = detect_hind_reach(v4, rig, target=target)
    profile["hindReachSign"] = hind_reach["sign"]
    profile["hindReach"] = hind_reach
    reports = [
        author_idle(v4, rig, species, profile),
        author_walk(v4, rig, species, profile),
        author_trot(v4, rig, species, profile),
        author_gallop(v4, rig, species, profile),
        author_attack(v4, rig, species, profile, target_height),
        author_death(v4, rig, species, target_height),
    ]
    rig.animation_data.action = None
    return reports


def orientation_report(rig, detection: dict) -> dict:
    root = rig.data.bones.get("Spine1")
    head = rig.data.bones.get("Head")
    bones_face_negative = bool(root and head and head.tail_local.y < root.head_local.y)
    return {
        "facingOk": bool(detection["headAtNegativeY"] and bones_face_negative),
        "expected": "-Y",
        "geometryHeadAtNegativeY": detection["headAtNegativeY"],
        "bonesFaceNegativeY": bones_face_negative,
        "confidence": detection["confidence"],
        "rotationDegrees": detection["rotationDegrees"],
    }


def render_previews(base, rig, target, preview_dir: Path) -> list[str]:
    import bpy

    preview_dir.mkdir(parents=True, exist_ok=True)
    base.setup_preview(target, rig)
    scene = bpy.context.scene
    actions = sorted(bpy.data.actions, key=lambda action: action.name)
    selected = [None] + actions
    rendered = []
    rig.animation_data_create()
    for action in selected:
        rig.animation_data.action = action
        if action is None:
            rig.data.pose_position = "REST"
            frame = 1
            label = "rest"
        else:
            rig.data.pose_position = "POSE"
            start, end = action.frame_range
            if action.name.endswith("Attack"):
                frame = round(start + (end - start) * 0.52)
            elif action.name.endswith("Death"):
                frame = round(start + (end - start) * 0.8)
            else:
                frame = round((start + end) * 0.5)
            label = action.name.rsplit("|", 1)[-1].lower()
        scene.frame_set(frame)
        scene.render.filepath = str(preview_dir / f"{label}.png")
        bpy.ops.render.render(write_still=True)
        rendered.append(scene.render.filepath)
    rig.data.pose_position = "REST"
    rig.animation_data.action = None
    scene.frame_set(1)
    return rendered


def main() -> int:
    import bpy

    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    v4 = load_module(script_dir / "rig_mesh_from_scratch_v4.py", "scratch_v4_for_v6")
    v5 = load_module(script_dir / "rig_mesh_from_scratch_v5.py", "scratch_v5_for_v6")
    base = v4.load_base_module()

    manifest_path = Path(args.manifest).resolve()
    manifest, build, family = load_manifest(manifest_path, args.species)
    repo_root = manifest_path.parents[3]
    source_path = (repo_root / build["source"]).resolve()
    actual_hash = sha256(source_path)
    if actual_hash != build["sourceSha256"]:
        raise RuntimeError(
            f"Source hash changed for {args.species}: expected {build['sourceSha256']} "
            f"got {actual_hash}"
        )

    out_fbx = Path(args.out_fbx).resolve()
    out_anims = Path(args.out_anims).resolve()
    out_blend = Path(args.out_blend).resolve()
    preview_dir = Path(args.preview_dir).resolve()
    palette_path = Path(args.palette_png).resolve() if args.palette_png else None

    base.clear_scene()
    add_fbx_import_compatibility()
    imported = base.import_fbx(source_path)
    target, proxy, sanitation, accessory_indices = sanitize_geometry(
        imported,
        mark_head_accessories=bool(build.get("joinAllMeshes")),
    )
    force_rotation = build.get("forceZRotation")
    detection = detect_orientation(
        proxy,
        force_z_rotation=None if force_rotation is None else float(force_rotation),
    )
    alignment = apply_alignment(
        target,
        proxy,
        detection["rotationDegrees"],
        float(build["targetHeight"]),
    )
    snout = validate_snout_at_negative_y(proxy)
    if not snout["ok"]:
        if force_rotation is not None:
            raise RuntimeError(
                f"Forced orientation still places cranial mass away from -Y: {snout}"
            )
        # One automatic 180° correction when the cranial score disagrees.
        apply_yaw_rotation(target, proxy, 180.0)
        detection = dict(detection)
        detection["rotationDegrees"] = (float(detection["rotationDegrees"]) + 180.0) % 360.0
        if detection["rotationDegrees"] > 180.0:
            detection["rotationDegrees"] -= 360.0
        detection["autoCorrectedByDegrees"] = 180.0
        detection["headAtNegativeY"] = True
        alignment["rotationDegrees"] = detection["rotationDegrees"]
        snout = validate_snout_at_negative_y(proxy)
        if not snout["ok"]:
            raise RuntimeError(f"Orientation auto-correct failed: {snout}")
    detection["snoutValidation"] = snout
    original_bounds, original_region_mean = configure_landmark_fallbacks(
        v4, proxy, build["family"]
    )

    palette = None
    if build["materialMode"] == "palette":
        if palette_path is None:
            raise RuntimeError("--palette-png is required for palette material builds")
        palette = create_palette_uv(target, palette_path)

    rig, rig_report = v4.build_scratch_armature(proxy)
    rig.name = build["rigName"]
    rig.data.name = build["rigName"]
    limb_repair = repair_scratch_limb_chains(rig, proxy, build["family"])
    rig_report["limbRepair"] = limb_repair
    orientation = orientation_report(rig, detection)
    if not orientation["facingOk"]:
        raise RuntimeError(f"Orientation gate failed: {orientation}")

    crown_indices = detect_head_crown_vertices(target, build["family"], rig)
    rigid_head_indices = detect_rigid_herbivore_head(target, build["family"], rig)
    try:
        skin = v4.bind_mesh(target, rig)
    except RuntimeError as error:
        skin = {
            "method": "scratch heat bind failed",
            "weightedVertices": 0,
            "unweightedVertices": len(target.data.vertices),
            "error": str(error),
        }
    if skin.get("unweightedVertices", 0):
        skin = bind_nearest_segments(target, rig)
    stabilize = stabilize_head_neck_by_bones(target, rig)
    if stabilize.get("stabilizedVertices", 0) < 8:
        stabilize = v4.stabilize_head_neck_weights(target)
    front_limbs = stabilize_front_limb_weights(target, rig)
    hind_limbs = stabilize_hind_limb_weights(target, rig)
    # Seal paws first, then reclaim chest — seal must not win over brisket verts.
    front_seal = seal_front_limb_spine_seams(target, rig)
    torso_reclaim = reclaim_torso_from_front_limbs(target, rig)
    head_reclaim = reclaim_head_from_front_limbs(target, rig)
    dorsum_reclaim = reclaim_dorsum_from_limbs(target, rig)
    front_limbs = {
        **front_limbs,
        "torsoReclaim": torso_reclaim,
        "headReclaim": head_reclaim,
        "dorsumReclaim": dorsum_reclaim,
        "spineSeal": front_seal,
    }
    skin["stabilize"] = stabilize
    skin["frontLimbStabilize"] = front_limbs
    skin["hindLimbStabilize"] = hind_limbs
    # Recompute crown after bind using final Head placement, then lock.
    crown_indices = detect_head_crown_vertices(target, build["family"], rig)
    # Lock antlers/horns (and a small skull pad) rigidly to Head so crowns do
    # not bend with Neck1. Neck column is reclaimed soft afterward.
    lock_indices = set(accessory_indices) | set(crown_indices) | set(rigid_head_indices)
    accessory_skin = rigidify_head_accessories(
        target,
        rig,
        sorted(lock_indices),
    )
    locked_set = set(accessory_skin.get("lockedIndices") or lock_indices)
    high_front = lock_high_front_to_head(target, rig, build["family"])
    # Fold any high-front picks into the rigid set and re-expand the tree so
    # swept tips that heat-bind left on Spine/Root cannot leave stretch ribbons.
    if high_front.get("vertices", 0):
        head_group = target.vertex_groups.get("Head")
        if head_group is not None:
            head_idx = head_group.index
            skull_top = max(
                float(rig.data.bones["Head"].head_local.z),
                float(rig.data.bones["Head"].tail_local.z),
            )
            for vertex in target.data.vertices:
                if vertex.co.z < skull_top - 0.1:
                    continue
                for assignment in vertex.groups:
                    if assignment.group == head_idx and assignment.weight >= 0.99:
                        locked_set.add(vertex.index)
                        break
        accessory_skin = rigidify_head_accessories(
            target,
            rig,
            sorted(locked_set),
        )
        locked_set = set(accessory_skin.get("lockedIndices") or locked_set)
    # Absorb antler flora ribbons misweighted onto Hind/Spine/Root.
    seal = seal_crown_appendages(target, locked_set, rig)
    locked_set = set(seal.get("lockedIndices") or locked_set)
    # Critical: crown flood must not keep the hanging neck rigid on Head.
    stripped = strip_neck_from_crown(target, locked_set, rig)
    locked_set = set(stripped.get("lockedIndices") or locked_set)
    # Exact lock only — no expand (expand re-paints the neck onto Head).
    accessory_skin = rigidify_head_accessories(
        target, rig, sorted(locked_set), expand=False
    )
    locked_set = set(accessory_skin.get("lockedIndices") or locked_set)
    reclaim = reclaim_neck_from_head(target, rig, protect_indices=locked_set)
    # Re-paint the soft neck from the Spine3→Head axis without touching the crown.
    # Critical for stag: short Neck1 makes nearest-bone paint dump the neck on Head.
    restabilize = stabilize_head_neck_by_bones(
        target, rig, protect_indices=locked_set
    )
    # Final crown assert without expand so reclaim/restabilize stick.
    accessory_skin = rigidify_head_accessories(
        target, rig, sorted(locked_set), expand=False
    )
    # Neck restabilize can recreate Spine↔Front seams — reseal paws, then
    # reclaim chest/dorsum last so limbs cannot yank the back or brisket.
    front_seal_final = seal_front_limb_spine_seams(target, rig)
    torso_reclaim_final = reclaim_torso_from_front_limbs(target, rig)
    head_reclaim_final = reclaim_head_from_front_limbs(target, rig)
    dorsum_reclaim_final = reclaim_dorsum_from_limbs(target, rig)
    bovine_hump = reclaim_bovine_hump_from_head(target, rig, build["family"])
    # Hump reclaim can leave Face/Front seams — head reclaim once more after.
    head_reclaim_post_hump = reclaim_head_from_front_limbs(target, rig)
    # Critical: strip Neck1 off everything behind the withers pivot so head-charge
    # cannot fling the torso/hump upward.
    neck1_caudal = reclaim_neck1_caudal_of_pivot(target, rig)
    head_caudal = reclaim_head_caudal_of_skull(target, rig)
    # After Neck1/Head caudal strips: pull beard/dewlap off Jaw so Attack leans
    # the neck column instead of rubber-banding the chin pad.
    jaw_caudal = reclaim_bovine_jaw_caudal_of_hinge(target, rig, build["family"])
    # Cranial reclaim often parks an entire forelimb on Spine (ram Front.R).
    # Reassert the Shoulder→Upper→Lower column last, then peel chest back off it.
    front_restabilize = stabilize_front_limb_weights(target, rig)
    front_seal_post = seal_front_limb_spine_seams(target, rig)
    torso_post = reclaim_torso_from_front_limbs(target, rig)
    # Final crown assert after limb restabilize — curled ram horns often land on
    # Spine/FrontShoulder once caudal Head reclaim and shin paint run.
    # expand=False: welded Tripo bodies flood Head into the brisket/neck when
    # expand walks "hanging" flora down from the crown (chest then gallops with Head).
    crown_indices_final = detect_head_crown_vertices(target, build["family"], rig)
    crown_final = rigidify_head_accessories(
        target,
        rig,
        sorted(set(crown_indices_final) | set(crown_indices)),
        expand=False,
    )
    head_caudal_post = reclaim_head_caudal_of_skull(target, rig)
    brisket_post = reclaim_brisket_from_head(target, rig)
    torso_after_crown = reclaim_torso_from_front_limbs(target, rig)
    # Soft neck column often still sits on Head after crown lock — restabilize.
    neck_post = reclaim_neck_from_head(target, rig, protect_indices=set(crown_indices_final) | set(crown_indices))
    neck_stabilize_post = stabilize_head_neck_by_bones(
        target,
        rig,
        protect_indices=set(crown_indices_final) | set(crown_indices),
    )
    # Re-lock horns only — neck restabilize must not soft-blend the curl.
    crown_assert = rigidify_head_accessories(
        target,
        rig,
        sorted(set(crown_indices_final) | set(crown_indices)),
        expand=False,
    )
    # Neck restabilize can re-paint the brisket onto Head — strip once more.
    brisket_final = reclaim_brisket_from_head(target, rig)
    torso_final = reclaim_torso_from_front_limbs(target, rig)
    skin["frontLimbStabilize"] = {
        **skin.get("frontLimbStabilize", {}),
        "spineSeal": front_limbs.get("spineSeal", {}),
        "torsoReclaim": front_limbs.get("torsoReclaim", {}),
        "headReclaim": front_limbs.get("headReclaim", {}),
        "dorsumReclaim": front_limbs.get("dorsumReclaim", {}),
        "spineSealFinal": front_seal_final,
        "torsoReclaimFinal": torso_reclaim_final,
        "headReclaimFinal": head_reclaim_final,
        "dorsumReclaimFinal": dorsum_reclaim_final,
        "bovineHumpReclaim": bovine_hump,
        "headReclaimPostHump": head_reclaim_post_hump,
        "neck1CaudalReclaim": neck1_caudal,
        "headCaudalReclaim": head_caudal,
        "bovineJawCaudalReclaim": jaw_caudal,
        "frontRestabilize": front_restabilize,
        "spineSealPost": front_seal_post,
        "torsoReclaimPost": torso_post,
        "crownRigidifyFinal": {
            "vertices": crown_final.get("vertices", 0),
            "detected": len(crown_indices_final),
            "assertVertices": crown_assert.get("vertices", 0),
        },
        "headCaudalPostCrown": head_caudal_post,
        "brisketReclaimPostCrown": brisket_post,
        "torsoReclaimAfterCrown": torso_after_crown,
        "neckReclaimPostCrown": neck_post,
        "neckStabilizePostCrown": neck_stabilize_post,
        "brisketReclaimFinal": brisket_final,
        "torsoReclaimFinalPost": torso_final,
    }
    accessory_skin["highFrontLocked"] = high_front
    accessory_skin["crownSealed"] = {
        "absorbed": seal.get("absorbed", 0),
        "neckStripped": stripped.get("stripped", 0),
    }
    accessory_skin["neckReclaimedFromHead"] = reclaim
    accessory_skin["neckRestabilized"] = restabilize
    accessory_skin["explicitAccessoryVertices"] = len(accessory_indices)
    accessory_skin["geometryCrownVertices"] = len(crown_indices)
    accessory_skin["rigidHerbivoreHeadVertices"] = len(rigid_head_indices)
    accessory_skin["weldedCervidSoftCrown"] = False
    accessory_skin["rigidCrownLocked"] = True
    accessory_skin.pop("lockedIndices", None)
    skin["headAccessories"] = accessory_skin

    bpy.data.objects.remove(proxy, do_unlink=True)
    v4.bounds = original_bounds
    v4.region_mean = original_region_mean

    bpy.context.scene.render.fps = int(manifest["rules"]["fps"])
    action_reports = author_actions(
        v4,
        rig,
        build["species"],
        build["family"],
        family,
        float(build["targetHeight"]),
        target=target,
    )
    previews = render_previews(base, rig, target, preview_dir)

    v5.export_mesh(out_fbx, target, rig, build["assetId"], build["rigName"])
    v5.export_anims(out_anims, rig)
    out_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))

    report = {
        "pipeline": manifest["pipeline"],
        "donorUsed": False,
        "sourceContribution": manifest["rules"]["sourceContribution"],
        "preservedPreviousVersions": True,
        "blenderVersion": bpy.app.version_string,
        "manifest": manifest_path.as_posix(),
        "species": build["species"],
        "family": build["family"],
        "familyProfile": family,
        "assetId": build["assetId"],
        "rigName": build["rigName"],
        "source": source_path.as_posix(),
        "sourceSha256": actual_hash,
        "materialMode": build["materialMode"],
        "palette": palette,
        "sanitation": sanitation,
        "orientationDetection": detection,
        "alignment": alignment,
        "orientation": orientation,
        "rig": rig_report,
        "skin": skin,
        "actions": action_reports,
        "actionOrderExpected": manifest["rules"]["actions"],
        "previews": previews,
        "outputs": {
            "meshFbx": out_fbx.as_posix(),
            "animationFbx": out_anims.as_posix(),
            "blend": out_blend.as_posix(),
        },
    }
    report_path = out_blend.with_suffix(".json")
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
