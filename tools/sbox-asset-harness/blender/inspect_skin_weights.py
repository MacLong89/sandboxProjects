#!/usr/bin/env python3
"""Summarize dominant vertex groups in spatial regions of a rigged Blender file."""

from __future__ import annotations

import json
import sys
from collections import defaultdict

import bpy


def summarize(obj, predicate) -> dict:
    totals = defaultdict(float)
    count = 0
    for vertex in obj.data.vertices:
        if not predicate(vertex.co):
            continue
        count += 1
        for assignment in vertex.groups:
            totals[obj.vertex_groups[assignment.group].name] += assignment.weight
    return {
        "vertices": count,
        "groups": sorted(totals.items(), key=lambda item: item[1], reverse=True)[:12],
    }


mesh = next(obj for obj in bpy.data.objects if obj.name == "tripo_wolf_rigged")
result = {
    "tailTip": summarize(mesh, lambda co: co.y > 2.3),
    "tailMid": summarize(mesh, lambda co: co.y > 1.5 and co.z > 1.5),
    "hindUpper": summarize(mesh, lambda co: 0.5 < co.y <= 1.5 and co.z > 1.0),
    "head": summarize(mesh, lambda co: co.y < -1.5),
}
print("WEIGHT_SUMMARY=" + json.dumps(result))
