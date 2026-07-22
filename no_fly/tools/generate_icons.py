from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    import subprocess, sys
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pillow", "-q"])
    from PIL import Image, ImageDraw

out = Path(r"C:\Users\Macra\Projects\sandboxProjects\no_fly\Assets\ui\icons")
mat = Path(r"C:\Users\Macra\Projects\sandboxProjects\no_fly\Assets\materials\nofly")
out.mkdir(parents=True, exist_ok=True)
mat.mkdir(parents=True, exist_ok=True)

icons = [
    ("shirt", (90, 160, 255)),
    ("shoe", (120, 70, 40)),
    ("book", (220, 100, 70)),
    ("laptop", (110, 120, 130)),
    ("knife", (190, 190, 200)),
    ("usb", (60, 210, 120)),
    ("jewel", (80, 220, 255)),
    ("package", (180, 130, 70)),
    ("passport", (40, 90, 180)),
    ("seal", (240, 190, 40)),
    ("nofly_logo", (255, 140, 40)),
    ("tsa_badge", (255, 200, 40)),
    ("headphones", (40, 40, 50)),
    ("teddy", (240, 190, 100)),
    ("gadget", (240, 80, 60)),
    ("vial", (120, 240, 80)),
    ("idol", (210, 160, 40)),
    ("bottle", (80, 180, 255)),
]

for name, rgb in icons:
    img = Image.new("RGBA", (128, 128), rgb + (255,))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((8, 8, 120, 120), radius=18, outline=(255, 255, 255, 200), width=4)
    d.ellipse((40, 40, 88, 88), outline=(255, 255, 255, 160), width=3)
    img.save(out / f"{name}.png")
    r, g, b = [c / 255 for c in rgb]
    vmat = (
        "<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} "
        "format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->\n"
        "{\n"
        '\tshader = "shaders/complex.shader"\n'
        f'\tTextureColor = "ui/icons/{name}.png"\n'
        "\tF_DIRECT_LIGHT_ONLY = 1\n"
        f"\tg_vColorTint = [{r:.3f}, {g:.3f}, {b:.3f}, 1.0]\n"
        "}\n"
    )
    (mat / f"{name}.vmat").write_text(vmat, encoding="utf-8")

print(f"wrote {len(icons)} icons")
