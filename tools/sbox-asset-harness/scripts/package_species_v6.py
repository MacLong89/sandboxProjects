#!/usr/bin/env python3
"""Install scratch-v6 creature outputs into scene_lab and update its catalog."""

from __future__ import annotations

import json
import shutil
from pathlib import Path


LOOPING = {"Gallop", "Idle", "Trot", "Walk"}


def vmat_text(texture: str) -> str:
    return f"""Layer0
{{
\tshader "shaders/complex.shader"

\tTextureColor "{texture}"
\tTextureAmbientOcclusion "materials/default/default_ao.tga"
\tTextureNormal "materials/default/default_normal.tga"
\tTextureRoughness "materials/default/default_rough.tga"

\tF_TRANSPARENT 0
\tF_ALPHA_TEST 0
\tF_RENDER_BACKFACES 0

\tg_flOpacity "1.000"
\tg_flModelTintAmount "0.000"
\tg_vColorTint "[1.000000 1.000000 1.000000 1.000000]"
\tg_flMetalness "0.000"
\tg_flRoughnessScaleFactor "1.000"
\tg_vTexCoordScale "[1.000 1.000]"
}}
"""


def animation_node(asset_id: str, species: str, action: str, take: int) -> str:
    loop = "true" if action in LOOPING else "false"
    return f"""\t\t\t\t\t{{
\t\t\t\t\t\t_class = "AnimFile"
\t\t\t\t\t\tname = "{species}_{action.lower()}"
\t\t\t\t\t\tactivity_name = ""
\t\t\t\t\t\tactivity_weight = 1
\t\t\t\t\t\tweight_list_name = ""
\t\t\t\t\t\tfade_in_time = 0.2
\t\t\t\t\t\tfade_out_time = 0.2
\t\t\t\t\t\tlooping = {loop}
\t\t\t\t\t\tdelta = false
\t\t\t\t\t\tworldSpace = false
\t\t\t\t\t\thidden = false
\t\t\t\t\t\tanim_markup_ordered = false
\t\t\t\t\t\tdisable_compression = false
\t\t\t\t\t\tdisable_interpolation = false
\t\t\t\t\t\tenable_scale = false
\t\t\t\t\t\tsource_filename = "models/{asset_id}/{asset_id}_anims.fbx"
\t\t\t\t\t\tstart_frame = -1
\t\t\t\t\t\tend_frame = -1
\t\t\t\t\t\tframerate = -1.0
\t\t\t\t\t\ttake = {take}
\t\t\t\t\t\treverse = false
\t\t\t\t\t}},
"""


def vmdl_text(asset_id: str, species: str, material: str, actions: list[str]) -> str:
    animations = "".join(
        animation_node(asset_id, species, action, take)
        for take, action in enumerate(actions)
    )
    return f"""<!-- kv3 encoding:text:version{{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d}} format:modeldoc30:version{{8c2d7a91-9c42-4bf0-883a-5a3b1762d4f1}} -->
{{
\trootNode =
\t{{
\t\t_class = "RootNode"
\t\tchildren =
\t\t[
\t\t\t{{
\t\t\t\t_class = "MaterialGroupList"
\t\t\t\tchildren =
\t\t\t\t[
\t\t\t\t\t{{
\t\t\t\t\t\t_class = "DefaultMaterialGroup"
\t\t\t\t\t\tremaps = [  ]
\t\t\t\t\t\tuse_global_default = true
\t\t\t\t\t\tglobal_default_material = "{material}"
\t\t\t\t\t}},
\t\t\t\t]
\t\t\t}},
\t\t\t{{
\t\t\t\t_class = "RenderMeshList"
\t\t\t\tchildren =
\t\t\t\t[
\t\t\t\t\t{{
\t\t\t\t\t\t_class = "RenderMeshFile"
\t\t\t\t\t\tfilename = "models/{asset_id}/{asset_id}.fbx"
\t\t\t\t\t\timport_translation = [ 0.0, 0.0, 0.0 ]
\t\t\t\t\t\timport_rotation = [ 0.0, 0.0, 0.0 ]
\t\t\t\t\t\timport_scale = 1.0
\t\t\t\t\t\talign_origin_x_type = "None"
\t\t\t\t\t\talign_origin_y_type = "None"
\t\t\t\t\t\talign_origin_z_type = "None"
\t\t\t\t\t\tparent_bone = ""
\t\t\t\t\t\timport_filter =
\t\t\t\t\t\t{{
\t\t\t\t\t\t\texclude_by_default = true
\t\t\t\t\t\t\texception_list =
\t\t\t\t\t\t\t[
\t\t\t\t\t\t\t\t"{asset_id}",
\t\t\t\t\t\t\t]
\t\t\t\t\t\t}}
\t\t\t\t\t}},
\t\t\t\t]
\t\t\t}},
\t\t\t{{
\t\t\t\t_class = "AnimationList"
\t\t\t\tchildren =
\t\t\t\t[
{animations}\t\t\t\t]
\t\t\t\tdefault_root_bone_name = ""
\t\t\t}},
\t\t]
\t\tmodel_archetype = ""
\t\tprimary_associated_entity = ""
\t\tanim_graph_name = ""
\t\tbase_model_name = ""
\t}}
}}
"""


def main() -> int:
    repo = Path.cwd().resolve()
    harness = repo / "tools" / "sbox-asset-harness"
    manifest_path = harness / "blender" / "species_v6_manifest.json"
    catalog_path = harness / "catalog" / "scene_lab.catalog.json"
    assets = repo / "scene_lab" / "Assets" / "models"
    output_root = harness / "out"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    actions = manifest["rules"]["actions"]
    new_entries = []
    package_report = []

    for build in manifest["builds"]:
        species = build["species"]
        asset_id = build["assetId"]
        source_dir = output_root / asset_id
        report_path = source_dir / f"{asset_id}.json"
        deformation_path = source_dir / "deformation_report.json"
        motion_path = source_dir / "motion_qa.json"
        required = [
            source_dir / f"{asset_id}.fbx",
            source_dir / f"{asset_id}_anims.fbx",
            report_path,
            deformation_path,
            motion_path,
        ]
        missing = [str(path) for path in required if not path.exists()]
        if missing:
            raise FileNotFoundError(f"{asset_id} missing build outputs: {missing}")

        report = json.loads(report_path.read_text(encoding="utf-8"))
        deformation = json.loads(deformation_path.read_text(encoding="utf-8"))
        motion = json.loads(motion_path.read_text(encoding="utf-8"))
        if not report["orientation"]["facingOk"] or not motion["passed"]:
            raise RuntimeError(f"{asset_id} failed orientation or motion QA")

        destination = assets / asset_id
        destination.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_dir / f"{asset_id}.fbx", destination / f"{asset_id}.fbx")
        shutil.copy2(
            source_dir / f"{asset_id}_anims.fbx",
            destination / f"{asset_id}_anims.fbx",
        )

        if build["materialMode"] == "palette":
            source_texture = Path(report["palette"]["path"])
            texture_name = f"{species}_palette.png"
            shutil.copy2(source_texture, destination / texture_name)
        else:
            source_texture = repo / build["textureSource"]
            texture_name = Path(build["textureSource"]).name
            # s&box rejects JPEG in TextureColor; normalize at package time.
            if source_texture.suffix.lower() in {".jpeg", ".jpg"}:
                texture_name = Path(texture_name).with_suffix(".png").name
                from PIL import Image

                with Image.open(source_texture) as img:
                    img.convert("RGB").save(destination / texture_name)
            else:
                shutil.copy2(source_texture, destination / texture_name)
        material_name = f"{asset_id}.vmat"
        material_resource = f"models/{asset_id}/{material_name}"
        texture_resource = f"models/{asset_id}/{texture_name}"
        (destination / material_name).write_text(
            vmat_text(texture_resource),
            encoding="utf-8",
        )
        (destination / f"{asset_id}.vmdl").write_text(
            vmdl_text(asset_id, species, material_resource, actions),
            encoding="utf-8",
        )

        tear_score = deformation.get("tearScore")
        warning = (
            " High deformation score; prioritize visual review."
            if isinstance(tear_score, int) and tear_score >= 300
            else ""
        )
        new_entries.append(
            {
                "id": asset_id,
                "games": ["scene_lab"],
                "lane": "mesh",
                "kind": "creature",
                "title": build["title"],
                "status": "ready",
                "vmdl": f"models/{asset_id}/{asset_id}.vmdl",
                "vmat": material_resource,
                "tags": [
                    "creature",
                    species,
                    build["family"],
                    "scratch",
                    "donor-free",
                    "rigged",
                    "animated",
                    "procedural",
                    "realistic-motion",
                    "v6",
                ],
                "notes": (
                    f"Donor-free scratch-v6 build; source contributes geometry, UVs, "
                    f"and appearance only. Family={build['family']}; "
                    f"walk={report['familyProfile']['walkPattern']}; "
                    f"trot={report['familyProfile']['trotPattern']}; "
                    f"gallop={report['familyProfile']['gallopPattern']}; "
                    f"attack={report['familyProfile']['attackStyle']}; "
                    f"facing=-Y; height={build['targetHeight']:.2f}m; "
                    f"tear={tear_score}; motion QA passed.{warning}"
                ),
            }
        )
        package_report.append(
            {
                "species": species,
                "assetId": asset_id,
                "vmdl": f"scene_lab/Assets/models/{asset_id}/{asset_id}.vmdl",
                "material": f"scene_lab/Assets/models/{asset_id}/{material_name}",
                "tearScore": tear_score,
                "motionQaPassed": motion["passed"],
                "facingOk": report["orientation"]["facingOk"],
            }
        )

    ids = {entry["id"] for entry in new_entries}
    catalog["entries"] = [
        entry for entry in catalog["entries"] if entry["id"] not in ids
    ] + new_entries
    catalog_path.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")
    package_report_path = output_root / "scratch_v6_package_summary.json"
    package_report_path.write_text(
        json.dumps(
            {
                "pipeline": manifest["pipeline"],
                "donorUsed": False,
                "preservedPreviousVersions": True,
                "packageCount": len(package_report),
                "packages": package_report,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    print(package_report_path)
    print(f"Packaged {len(package_report)} scratch-v6 creatures")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
