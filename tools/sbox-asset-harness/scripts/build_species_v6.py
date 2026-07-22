#!/usr/bin/env python3
"""Reproducible batch builder for donor-free scratch-v6 animal packages."""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import subprocess
import sys
import time
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--manifest",
        default="tools/sbox-asset-harness/blender/species_v6_manifest.json",
    )
    parser.add_argument(
        "--blender",
        default=r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    )
    parser.add_argument("--species", nargs="*", default=["all"])
    parser.add_argument("--jobs", type=int, default=2)
    parser.add_argument("--deformation-frames", type=int, default=8)
    return parser.parse_args()


def run_logged(command: list[str], log: Path, cwd: Path) -> tuple[int, str]:
    result = subprocess.run(
        command,
        cwd=cwd,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        timeout=600,
        check=False,
    )
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text(result.stdout, encoding="utf-8")
    return result.returncode, result.stdout


def build_one(
    repo: Path,
    blender: Path,
    manifest_path: Path,
    build: dict,
    deformation_frames: int,
) -> dict:
    started = time.time()
    asset_id = build["assetId"]
    species = build["species"]
    output = repo / "tools" / "sbox-asset-harness" / "out" / asset_id
    mesh_fbx = output / f"{asset_id}.fbx"
    anim_fbx = output / f"{asset_id}_anims.fbx"
    blend = output / f"{asset_id}.blend"
    palette = output / f"{species}_palette.png"
    build_log = output / "build.log"
    script = repo / "tools" / "sbox-asset-harness" / "blender" / "rig_mesh_from_scratch_v6.py"

    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(script),
        "--",
        "--manifest",
        str(manifest_path),
        "--species",
        species,
        "--out-fbx",
        str(mesh_fbx),
        "--out-anims",
        str(anim_fbx),
        "--out-blend",
        str(blend),
        "--preview-dir",
        str(output / "previews"),
    ]
    if build["materialMode"] == "palette":
        command.extend(["--palette-png", str(palette)])
    build_code, build_output = run_logged(command, build_log, repo)
    result = {
        "species": species,
        "assetId": asset_id,
        "buildExitCode": build_code,
        "buildLog": build_log.as_posix(),
        "passed": False,
    }
    if build_code:
        result["error"] = build_output[-4000:]
        result["elapsedSeconds"] = round(time.time() - started, 3)
        return result

    measure_script = repo / "tools" / "sbox-asset-harness" / "blender" / "measure_deformation.py"
    deformation = output / "deformation_report.json"
    measure_code, measure_output = run_logged(
        [
            str(blender),
            "--background",
            str(blend),
            "--python",
            str(measure_script),
            "--",
            "--out",
            str(deformation),
            "--frames",
            str(deformation_frames),
        ],
        output / "deformation.log",
        repo,
    )
    result["deformationExitCode"] = measure_code
    if deformation.exists():
        deformation_data = json.loads(deformation.read_text(encoding="utf-8"))
        result["tearScore"] = deformation_data.get("tearScore")
        result["tornEdgeSamples"] = deformation_data.get("tornEdgeSamples")
    if measure_code:
        result["error"] = measure_output[-4000:]

    qa_script = repo / "tools" / "sbox-asset-harness" / "blender" / "qa_scratch_v6.py"
    motion_qa = output / "motion_qa.json"
    qa_code, qa_output = run_logged(
        [
            str(blender),
            "--background",
            str(blend),
            "--python",
            str(qa_script),
            "--",
            "--out",
            str(motion_qa),
        ],
        output / "motion_qa.log",
        repo,
    )
    result["motionQaExitCode"] = qa_code
    if motion_qa.exists():
        qa_data = json.loads(motion_qa.read_text(encoding="utf-8"))
        result["motionQaPassed"] = qa_data.get("passed", False)
        result["motionQaErrors"] = qa_data.get("errors", [])
    if qa_code:
        result["error"] = qa_output[-4000:]

    inspect_script = repo / "tools" / "sbox-asset-harness" / "blender" / "inspect_fbx.py"
    inspect_results = {}
    for kind, source in (("mesh", mesh_fbx), ("animations", anim_fbx)):
        destination = output / f"{kind}_fbx_inspection.json"
        code, inspection_output = run_logged(
            [
                str(blender),
                "--background",
                "--factory-startup",
                "--python",
                str(inspect_script),
                "--",
                "--fbx",
                str(source),
                "--out",
                str(destination),
            ],
            output / f"{kind}_fbx_inspection.log",
            repo,
        )
        inspect_results[kind] = {"exitCode": code, "report": destination.as_posix()}
        if code:
            result["error"] = inspection_output[-4000:]
    result["inspection"] = inspect_results
    result["passed"] = (
        build_code == 0
        and measure_code == 0
        and qa_code == 0
        and all(item["exitCode"] == 0 for item in inspect_results.values())
    )
    result["elapsedSeconds"] = round(time.time() - started, 3)
    return result


def main() -> int:
    args = parse_args()
    repo = Path.cwd().resolve()
    manifest_path = (repo / args.manifest).resolve()
    blender = Path(args.blender).resolve()
    if not blender.exists():
        raise FileNotFoundError(blender)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    requested = set(args.species)
    builds = manifest["builds"]
    if "all" not in requested:
        builds = [build for build in builds if build["species"] in requested]
        missing = requested - {build["species"] for build in builds}
        if missing:
            raise ValueError(f"Unknown species: {sorted(missing)}")

    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.jobs)) as pool:
        futures = {
            pool.submit(
                build_one,
                repo,
                blender,
                manifest_path,
                build,
                args.deformation_frames,
            ): build["species"]
            for build in builds
        }
        for future in concurrent.futures.as_completed(futures):
            result = future.result()
            results.append(result)
            state = "PASS" if result["passed"] else "FAIL"
            print(
                f"{state} {result['species']} "
                f"tear={result.get('tearScore')} "
                f"seconds={result['elapsedSeconds']}",
                flush=True,
            )

    results.sort(key=lambda item: item["species"])
    summary = {
        "pipeline": manifest["pipeline"],
        "donorUsed": False,
        "buildCount": len(results),
        "passed": all(result["passed"] for result in results),
        "results": results,
    }
    summary_path = (
        repo
        / "tools"
        / "sbox-asset-harness"
        / "out"
        / "scratch_v6_batch_summary.json"
    )
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0 if summary["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
