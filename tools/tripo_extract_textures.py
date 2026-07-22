#!/usr/bin/env python3
"""Extract embedded PBR textures from a Tripo GLB into loose image files.

Tripo API models ship with textures baked into the .glb (glTF binary). s&box
wants loose maps (e.g. wolf_basecolor.png) to author a .vmat, so this pulls the
baseColor / normal / metallic-roughness / emissive / occlusion images back out.

Standard-library only. Names outputs by role using the Thorns convention:
    <name>_basecolor.<ext>, <name>_normal.<ext>, <name>_metallic_roughness.<ext>, ...

Example:
    python tools/tripo_extract_textures.py tools/tripo_output/wolf_pbr_model.glb --name wolf
"""

import argparse
import json
import struct
from pathlib import Path

_MIME_EXT = {
    "image/png": ".png",
    "image/jpeg": ".jpeg",
    "image/jpg": ".jpeg",
    "image/webp": ".webp",
}


def _read_glb(path: Path) -> tuple[dict, bytes]:
    raw = path.read_bytes()
    magic, version, _length = struct.unpack_from("<III", raw, 0)
    if magic != 0x46546C67:  # "glTF"
        raise SystemExit(f"{path} is not a binary GLB (bad magic).")
    if version != 2:
        raise SystemExit(f"Unsupported GLB version {version} (expected 2).")

    gltf: dict = {}
    bin_chunk = b""
    offset = 12
    while offset < len(raw):
        chunk_len, chunk_type = struct.unpack_from("<II", raw, offset)
        offset += 8
        chunk_data = raw[offset : offset + chunk_len]
        offset += chunk_len
        if chunk_type == 0x4E4F534A:  # "JSON"
            gltf = json.loads(chunk_data.decode("utf-8"))
        elif chunk_type == 0x004E4942:  # "BIN\0"
            bin_chunk = chunk_data
    if not gltf:
        raise SystemExit("No JSON chunk found in GLB.")
    return gltf, bin_chunk


def _image_bytes(gltf: dict, bin_chunk: bytes, image_index: int) -> tuple[bytes, str]:
    img = gltf["images"][image_index]
    mime = img.get("mimeType", "image/png")
    ext = _MIME_EXT.get(mime, ".png")
    if "bufferView" in img:
        bv = gltf["bufferViews"][img["bufferView"]]
        start = bv.get("byteOffset", 0)
        data = bin_chunk[start : start + bv["byteLength"]]
        return data, ext
    raise SystemExit(f"Image {image_index} uses an external/data URI (not embedded); unsupported.")


def _role_map(gltf: dict) -> dict[int, str]:
    """Map glTF image index -> semantic role name."""
    textures = gltf.get("textures", [])

    def img_of(tex_ref: dict | None) -> int | None:
        if not tex_ref or "index" not in tex_ref:
            return None
        return textures[tex_ref["index"]].get("source")

    roles: dict[int, str] = {}
    for mat in gltf.get("materials", []):
        pbr = mat.get("pbrMetallicRoughness", {})
        for role, ref in (
            ("basecolor", pbr.get("baseColorTexture")),
            ("metallic_roughness", pbr.get("metallicRoughnessTexture")),
            ("normal", mat.get("normalTexture")),
            ("occlusion", mat.get("occlusionTexture")),
            ("emissive", mat.get("emissiveTexture")),
        ):
            idx = img_of(ref)
            if idx is not None and idx not in roles:
                roles[idx] = role
    return roles


def extract(glb_path: Path, name: str, out_dir: Path | None = None) -> list[Path]:
    gltf, bin_chunk = _read_glb(glb_path)
    images = gltf.get("images", [])
    if not images:
        print("No embedded images found in this GLB.")
        return []

    out_dir = out_dir or glb_path.parent
    out_dir.mkdir(parents=True, exist_ok=True)
    roles = _role_map(gltf)

    saved: list[Path] = []
    for i in range(len(images)):
        data, ext = _image_bytes(gltf, bin_chunk, i)
        role = roles.get(i, f"texture{i}")
        dest = out_dir / f"{name}_{role}{ext}"
        dest.write_bytes(data)
        print(f"Extracted image {i} ({role}) -> {dest}")
        saved.append(dest)
    return saved


def main() -> None:
    ap = argparse.ArgumentParser(description="Extract PBR textures from a Tripo GLB")
    ap.add_argument("glb", help="Path to the .glb file")
    ap.add_argument("--name", default="", help="Base filename (default: GLB stem)")
    ap.add_argument("--out", default="", help="Output directory (default: alongside the GLB)")
    args = ap.parse_args()

    glb_path = Path(args.glb)
    if not glb_path.is_file():
        raise SystemExit(f"File not found: {glb_path}")
    name = args.name or glb_path.stem.replace("_pbr_model", "").replace("_model", "")
    out_dir = Path(args.out) if args.out else None

    saved = extract(glb_path, name, out_dir)
    if saved:
        print("\nSaved textures:")
        for p in saved:
            print(f"  {p}")


if __name__ == "__main__":
    main()
