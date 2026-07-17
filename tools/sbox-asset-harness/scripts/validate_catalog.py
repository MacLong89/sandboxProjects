#!/usr/bin/env python3
"""Validate s&box asset harness catalog JSON files."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG_DIR = ROOT / "catalog"
ALLOWED_LANES = {"kit", "mesh", "place"}
ALLOWED_STATUS = {"ready", "placeholder", "blocked_no_blender", "needs_import"}


def main() -> int:
    files = sorted(CATALOG_DIR.glob("*.catalog.json"))
    if not files:
        print("ERROR: no *.catalog.json files found", file=sys.stderr)
        return 1

    errors: list[str] = []
    seen_ids: dict[str, str] = {}

    for path in files:
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            errors.append(f"{path.name}: invalid JSON ({e})")
            continue

        if not isinstance(data.get("version"), int):
            errors.append(f"{path.name}: missing integer version")
        entries = data.get("entries")
        if not isinstance(entries, list):
            errors.append(f"{path.name}: entries must be a list")
            continue

        for i, entry in enumerate(entries):
            prefix = f"{path.name}[{i}]"
            if not isinstance(entry, dict):
                errors.append(f"{prefix}: entry must be object")
                continue

            eid = entry.get("id")
            if not isinstance(eid, str) or not eid:
                errors.append(f"{prefix}: missing id")
            else:
                if eid in seen_ids:
                    errors.append(f"{prefix}: duplicate id '{eid}' (also in {seen_ids[eid]})")
                else:
                    seen_ids[eid] = path.name

            games = entry.get("games")
            if not isinstance(games, list) or not games:
                errors.append(f"{prefix}: games must be non-empty list")

            lane = entry.get("lane")
            if lane not in ALLOWED_LANES:
                errors.append(f"{prefix}: lane must be one of {sorted(ALLOWED_LANES)}")

            status = entry.get("status")
            if status not in ALLOWED_STATUS:
                errors.append(f"{prefix}: status must be one of {sorted(ALLOWED_STATUS)}")

            for key in ("kind", "title"):
                if not isinstance(entry.get(key), str) or not entry.get(key):
                    errors.append(f"{prefix}: missing {key}")

            vmdl = entry.get("vmdl", None)
            if vmdl is not None and not isinstance(vmdl, str):
                errors.append(f"{prefix}: vmdl must be string or null")
            if isinstance(vmdl, str) and vmdl and not vmdl.endswith(".vmdl"):
                errors.append(f"{prefix}: vmdl should end with .vmdl")

            if lane == "kit" and not isinstance(entry.get("kit"), dict):
                errors.append(f"{prefix}: kit lane requires kit object")

            if status == "ready" and lane == "mesh" and not vmdl:
                errors.append(f"{prefix}: mesh+ready requires non-null vmdl")

    if errors:
        print("Catalog validation FAILED:")
        for e in errors:
            print(f"  - {e}")
        return 1

    print(f"OK ({len(files)} catalog file(s), {len(seen_ids)} entr{'y' if len(seen_ids)==1 else 'ies'})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
