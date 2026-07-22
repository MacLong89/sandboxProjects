#!/usr/bin/env python3
"""Generate or convert a 3D model with the Tripo OpenAPI (submit -> poll -> download).

Standard-library only (urllib), so no pip install is needed.

Reads the API key from the TRIPO_API_KEY environment variable.

Examples:
    # Full s&box pipeline: generate, FBX, loose maps, .vmat, and .vmdl
    python tools/tripo_generate.py "stylized low-poly wolf" --name wolf --format FBX \
        --sbox-dest thorns/Assets/models/wolf

    # Image -> 3D from a turnaround / concept sheet
    python tools/tripo_generate.py --image tools/tripo_output/buffalo_ref/buffalo_reference.png \
        --name buffalo --format FBX --smart-low-poly --sbox-dest shared_assets/Assets/models/buffalo

    # Text -> 3D only (GLB)
    python tools/tripo_generate.py "stylized low-poly wolf" --name wolf

    # Convert an existing task's model to FBX
    python tools/tripo_generate.py --from-task <task_id> --format FBX --name wolf
"""

import argparse
import json
import os
import shutil
import time
import urllib.error
import urllib.request
from pathlib import Path

try:
    from .tripo_extract_textures import extract as extract_textures
    from .tripo_make_vmat import asset_relative_path, write_vmat
except ImportError:
    # Direct script execution: `python tools/tripo_generate.py ...`
    from tripo_extract_textures import extract as extract_textures
    from tripo_make_vmat import asset_relative_path, write_vmat

API_BASE = "https://api.tripo3d.ai/v2/openapi"

# Terminal states returned by the task status endpoint.
DONE_OK = {"success"}
DONE_BAD = {"failed", "cancelled", "banned", "expired", "unknown"}


def _request(method: str, url: str, api_key: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Bearer {api_key}")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")
        raise SystemExit(f"HTTP {e.code} from {url}\n{detail}") from e


def _submit(api_key: str, payload: dict) -> str:
    resp = _request("POST", f"{API_BASE}/task", api_key, payload)
    if resp.get("code") != 0:
        raise SystemExit(f"Submit failed: {json.dumps(resp, indent=2)}")
    task_id = resp["data"]["task_id"]
    print(f"Submitted task: {task_id}")
    print(f"Payload: {json.dumps(payload)}")
    return task_id


def _upload_image(api_key: str, image_path: Path) -> str:
    """Upload an image and return the Tripo image/file token."""
    import mimetypes
    import uuid

    boundary = f"----TripoBoundary{uuid.uuid4().hex}"
    filename = image_path.name
    mime = mimetypes.guess_type(filename)[0] or "application/octet-stream"
    file_bytes = image_path.read_bytes()
    body = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="file"; filename="{filename}"\r\n'
        f"Content-Type: {mime}\r\n\r\n"
    ).encode("utf-8") + file_bytes + f"\r\n--{boundary}--\r\n".encode("utf-8")
    req = urllib.request.Request(
        f"{API_BASE}/upload",
        data=body,
        method="POST",
    )
    req.add_header("Authorization", f"Bearer {api_key}")
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            payload = json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")
        raise SystemExit(f"HTTP {error.code} from upload\n{detail}") from error
    if payload.get("code") != 0:
        raise SystemExit(f"Upload failed: {json.dumps(payload, indent=2)}")
    data = payload.get("data") or {}
    token = data.get("image_token") or data.get("file_token") or data.get("token")
    if not token:
        raise SystemExit(f"Upload returned no image token: {json.dumps(payload, indent=2)}")
    print(f"Uploaded image token: {token}")
    return token


def submit_text_to_model(api_key: str, args) -> str:
    payload: dict = {"type": "text_to_model", "prompt": args.prompt}
    if args.model_version:
        payload["model_version"] = args.model_version
    if args.face_limit:
        payload["face_limit"] = args.face_limit
    if args.no_texture:
        payload["texture"] = False
    if args.smart_low_poly:
        payload["smart_low_poly"] = True
    return _submit(api_key, payload)


def submit_image_to_model(api_key: str, args) -> str:
    image_path = Path(args.image)
    if not image_path.is_file():
        raise SystemExit(f"Image not found: {image_path}")
    token = _upload_image(api_key, image_path)
    payload: dict = {
        "type": "image_to_model",
        "file": {
            "type": image_path.suffix.lstrip(".").lower() or "png",
            "file_token": token,
        },
    }
    if args.model_version:
        payload["model_version"] = args.model_version
    if args.face_limit:
        payload["face_limit"] = args.face_limit
    if args.no_texture:
        payload["texture"] = False
    if args.smart_low_poly:
        payload["smart_low_poly"] = True
    if args.prompt:
        # Optional style nudge some Tripo versions accept alongside the image.
        payload["prompt"] = args.prompt
    payload["enable_image_autofix"] = True
    return _submit(api_key, payload)


def _jpeg_to_png(path: Path) -> Path:
    """Convert a JPEG texture to PNG (s&box VMAT-friendly) and remove the JPEG."""
    png = path.with_suffix(".png")
    try:
        from PIL import Image  # type: ignore
    except ImportError:
        Image = None
    if Image is not None:
        with Image.open(path) as img:
            img.convert("RGBA" if img.mode in ("RGBA", "P") else "RGB").save(png)
    else:
        # Fallback: Windows / .NET System.Drawing via PowerShell-free ctypes path
        # is awkward; use a tiny stdlib approach via subprocess to magick/ffmpeg
        # if present, otherwise raise with a clear install hint.
        import subprocess

        for cmd in (
            ["magick", str(path), str(png)],
            ["ffmpeg", "-y", "-i", str(path), str(png)],
        ):
            try:
                subprocess.run(cmd, check=True, capture_output=True)
                break
            except (FileNotFoundError, subprocess.CalledProcessError):
                continue
        else:
            raise SystemExit(
                f"Need Pillow (pip install pillow) to convert {path.name} → PNG "
                "for s&box materials."
            )
    if png.is_file() and path != png:
        path.unlink(missing_ok=True)
    return png


def submit_convert(api_key: str, original_task_id: str, args) -> str:
    payload: dict = {
        "type": "convert_model",
        "format": args.format.upper(),
        "original_model_task_id": original_task_id,
    }
    # FBX defaults texture to PNG (good for engines); allow overrides.
    if args.texture_format:
        payload["texture_format"] = args.texture_format.upper()
    if args.texture_size:
        payload["texture_size"] = args.texture_size
    if args.fbx_preset:
        payload["fbx_preset"] = args.fbx_preset
    return _submit(api_key, payload)


def poll(api_key: str, task_id: str) -> dict:
    last = None
    while True:
        resp = _request("GET", f"{API_BASE}/task/{task_id}", api_key)
        data = resp.get("data", {})
        status = data.get("status")
        progress = data.get("progress", 0)
        line = f"status={status} progress={progress}%"
        if line != last:
            print(line)
            last = line
        if status in DONE_OK:
            return data
        if status in DONE_BAD:
            raise SystemExit(f"Task ended as '{status}':\n{json.dumps(data, indent=2)}")
        time.sleep(3)


def download_outputs(data: dict, out_dir: Path, name: str) -> list[Path]:
    out_dir.mkdir(parents=True, exist_ok=True)
    output = data.get("output", {}) or {}
    saved: list[Path] = []
    for key, url in output.items():
        if not isinstance(url, str) or not url.startswith("http"):
            continue
        # Derive an extension from the URL path (strip query string).
        tail = url.split("?", 1)[0]
        ext = os.path.splitext(tail)[1] or f".{key}"
        dest = out_dir / f"{name}_{key}{ext}"
        print(f"Downloading {key} -> {dest}")
        urllib.request.urlretrieve(url, dest)
        saved.append(dest)
    return saved


def run_and_download(api_key: str, task_id: str, out_dir: Path, name: str) -> tuple[dict, list[Path]]:
    data = poll(api_key, task_id)
    print(f"Done. consumed_credit={data.get('consumed_credit')}")
    saved = download_outputs(data, out_dir, name)
    return data, saved


def find_output(paths: list[Path], *, suffix: str = "", token: str = "") -> Path | None:
    for path in paths:
        if suffix and path.suffix.lower() != suffix:
            continue
        if token and token not in path.stem:
            continue
        return path
    return None


def write_sbox_sources(source_paths: list[Path], dest: Path, name: str) -> list[Path]:
    """Copy clean source files and author a simple material/model for s&box."""
    dest.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []

    fbx = find_output(source_paths, suffix=".fbx")
    if not fbx:
        raise SystemExit(
            "--sbox-dest requires FBX output; add --format FBX to the command."
        )
    clean_fbx = dest / f"{name}.fbx"
    shutil.copy2(fbx, clean_fbx)
    written.append(clean_fbx)

    # Keep the GLB as an inspectable archive/source, but ModelDoc uses the FBX.
    glb = find_output(source_paths, suffix=".glb")
    if glb:
        clean_glb = dest / f"{name}.glb"
        shutil.copy2(glb, clean_glb)
        written.append(clean_glb)

    copied_maps: dict[str, Path] = {}
    for role in ("basecolor", "normal", "metallic_roughness", "occlusion", "emissive"):
        texture = find_output(source_paths, token=f"_{role}")
        if not texture:
            continue
        clean_texture = dest / f"{name}_{role}{texture.suffix.lower()}"
        shutil.copy2(texture, clean_texture)
        # s&box materials expect PNG for TextureColor / normals; Tripo often emits JPEG.
        if role in ("basecolor", "normal") and clean_texture.suffix.lower() in {
            ".jpeg",
            ".jpg",
        }:
            clean_texture = _jpeg_to_png(clean_texture)
        copied_maps[role] = clean_texture
        written.append(clean_texture)

    basecolor = copied_maps.get("basecolor")
    if not basecolor:
        raise SystemExit("No extracted basecolor texture was found for s&box packaging.")

    material_path = write_vmat(
        basecolor,
        normal=copied_maps.get("normal"),
        dest=dest,
        name=name,
    )
    written.append(material_path)

    model_path = dest / f"{name}.vmdl"
    fbx_resource = asset_relative_path(clean_fbx)
    material_resource = asset_relative_path(material_path)
    model_path.write_text(
        f"""<!-- kv3 encoding:text:version{{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d}} format:modeldoc30:version{{8c2d7a91-9c42-4bf0-883a-5a3b1762d4f1}} -->
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
\t\t\t\t\t\tglobal_default_material = "{material_resource}"
\t\t\t\t\t}},
\t\t\t\t]
\t\t\t}},
\t\t\t{{
\t\t\t\t_class = "RenderMeshList"
\t\t\t\tchildren =
\t\t\t\t[
\t\t\t\t\t{{
\t\t\t\t\t\t_class = "RenderMeshFile"
\t\t\t\t\t\tfilename = "{fbx_resource}"
\t\t\t\t\t\timport_translation = [ 0.0, 0.0, 0.0 ]
\t\t\t\t\t\timport_rotation = [ 0.0, 0.0, 0.0 ]
\t\t\t\t\t\timport_scale = 1.0
\t\t\t\t\t\talign_origin_x_type = "None"
\t\t\t\t\t\talign_origin_y_type = "None"
\t\t\t\t\t\talign_origin_z_type = "None"
\t\t\t\t\t\tparent_bone = ""
\t\t\t\t\t}},
\t\t\t\t]
\t\t\t}},
\t\t]
\t\tmodel_archetype = ""
\t\tprimary_associated_entity = ""
\t\tanim_graph_name = ""
\t\tbase_model_name = ""
\t}}
}}
""",
        encoding="utf-8",
    )
    written.append(model_path)
    return written


def main() -> None:
    ap = argparse.ArgumentParser(description="Tripo text-to-3D generator / converter")
    ap.add_argument("prompt", nargs="?", help="Text prompt (omit when using --from-task/--image)")
    ap.add_argument(
        "--image",
        default="",
        help="Reference image for image_to_model (png/jpg/webp)",
    )
    ap.add_argument("--name", default="model", help="Base filename for outputs")
    ap.add_argument(
        "--from-task",
        default="",
        help="Convert an existing task's model instead of generating",
    )
    ap.add_argument(
        "--format",
        default="",
        help="Convert to format after generate (or with --from-task): FBX, OBJ, GLTF, USDZ, STL, 3MF",
    )
    ap.add_argument(
        "--texture-format",
        default="",
        help="Texture format for conversion (default PNG for FBX)",
    )
    ap.add_argument(
        "--texture-size",
        type=int,
        default=0,
        help="Texture size in pixels for conversion (<= 4096)",
    )
    ap.add_argument(
        "--fbx-preset",
        default="",
        help="FBX target: blender (default), mixamo, 3dsmax",
    )
    ap.add_argument(
        "--out",
        default=str(Path(__file__).parent / "tripo_output"),
        help="Output directory",
    )
    ap.add_argument(
        "--model-version",
        default="",
        help="e.g. v3.1-20260211 (blank = server default)",
    )
    ap.add_argument(
        "--face-limit",
        type=int,
        default=0,
        help="Cap polygon count for low-poly output (0 = auto)",
    )
    ap.add_argument(
        "--smart-low-poly",
        action="store_true",
        help="Request Tripo smart low-poly topology (extra credits)",
    )
    ap.add_argument(
        "--no-texture",
        action="store_true",
        help="Skip texture generation (cheaper: 10 vs 20 credits)",
    )
    ap.add_argument(
        "--no-extract-textures",
        action="store_true",
        help="Do not extract loose PBR maps from the downloaded GLB",
    )
    ap.add_argument(
        "--sbox-dest",
        default="",
        help="Place clean FBX/maps plus generated .vmat/.vmdl in an Assets subfolder",
    )
    args = ap.parse_args()

    api_key = os.environ.get("TRIPO_API_KEY")
    if not api_key:
        raise SystemExit("Set TRIPO_API_KEY in your environment first.")

    out_dir = Path(args.out)
    all_saved: list[Path] = []

    if args.from_task:
        if not args.format:
            raise SystemExit("--format is required with --from-task (e.g. FBX)")
        convert_id = submit_convert(api_key, args.from_task, args)
        _, saved = run_and_download(api_key, convert_id, out_dir, args.name)
        all_saved.extend(saved)
    elif args.image:
        gen_id = submit_image_to_model(api_key, args)
        print("\n--- Generating from image ---")
        _, saved = run_and_download(api_key, gen_id, out_dir, args.name)
        all_saved.extend(saved)

        if not args.no_extract_textures:
            glb = find_output(saved, suffix=".glb")
            if glb:
                print("\n--- Extracting PBR textures ---")
                all_saved.extend(extract_textures(glb, args.name, out_dir))
            else:
                print("No GLB output found; skipping texture extraction.")

        if args.format:
            print(f"\n--- Converting to {args.format.upper()} ---")
            convert_id = submit_convert(api_key, gen_id, args)
            _, saved = run_and_download(api_key, convert_id, out_dir, args.name)
            all_saved.extend(saved)
    elif args.prompt:
        gen_id = submit_text_to_model(api_key, args)
        print("\n--- Generating ---")
        _, saved = run_and_download(api_key, gen_id, out_dir, args.name)
        all_saved.extend(saved)

        if not args.no_extract_textures:
            glb = find_output(saved, suffix=".glb")
            if glb:
                print("\n--- Extracting PBR textures ---")
                all_saved.extend(extract_textures(glb, args.name, out_dir))
            else:
                print("No GLB output found; skipping texture extraction.")

        # Optional auto-convert (e.g. --format FBX) after generation succeeds.
        if args.format:
            print(f"\n--- Converting to {args.format.upper()} ---")
            convert_id = submit_convert(api_key, gen_id, args)
            _, saved = run_and_download(api_key, convert_id, out_dir, args.name)
            all_saved.extend(saved)
    else:
        raise SystemExit("Provide a prompt, --image <path>, or --from-task <id> --format FBX")

    if args.sbox_dest:
        print("\n--- Packaging s&box sources ---")
        packaged = write_sbox_sources(all_saved, Path(args.sbox_dest), args.name)
        print("Packaged files:")
        for path in packaged:
            print(f"  {path}")

    if all_saved:
        print("\nSaved files:")
        for p in all_saved:
            print(f"  {p}")
    else:
        print("No downloadable outputs found.")


if __name__ == "__main__":
    main()
