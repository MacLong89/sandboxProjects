import random
from PIL import Image, ImageDraw, ImageFilter

W, H = 4096, 2048
random.seed(42)

img = Image.new("RGB", (W, H))
px = img.load()

for y in range(H):
    v = y / (H - 1)
    zen = 1.0 - min(v * 1.25, 1.0)
    for x in range(W):
        # Rich saturated cyan-blue at zenith, warm golden haze near horizon.
        r = int(20 + 55 * (1 - zen) + 30 * v * v)
        g = int(120 + 110 * (1 - zen) + 40 * v)
        b = int(248 + 7 * (1 - zen) - 20 * v * v)
        px[x, y] = (min(r, 255), min(g, 255), min(b, 255))

cloud_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
draw = ImageDraw.Draw(cloud_layer, "RGBA")

# Sparse, smaller clouds kept in the lower third of the sky so blue dominates above.
for band_y in range(3):
    base_y = int(H * (0.42 + band_y * 0.07))
    count = 5 + band_y
    for i in range(count):
        if random.random() < 0.35:
            continue

        cx = int((i + random.random()) * (W / count)) % W
        cy = base_y + random.randint(-30, 40)
        scale = random.uniform(0.35, 0.72)
        w = int(220 * scale)
        h = int(68 * scale)
        alpha = random.randint(95, 155)
        warm = random.randint(4, 22)
        color = (255 - warm, 250 - warm // 2, 255 - warm * 4, alpha)

        draw.ellipse((cx - w, cy - h, cx + w, cy + h), fill=color)
        draw.ellipse((cx - w * 0.55, cy - h * 0.95, cx + w * 0.55, cy + h * 0.30), fill=color)

        for offset in (-W, W):
            draw.ellipse((cx - w + offset, cy - h, cx + w + offset, cy + h), fill=color)
            draw.ellipse((cx - w * 0.55 + offset, cy - h * 0.95, cx + w * 0.55 + offset, cy + h * 0.30), fill=color)

cloud_layer = cloud_layer.filter(ImageFilter.GaussianBlur(radius=9))
out = Image.alpha_composite(img.convert("RGBA"), cloud_layer).convert("RGB")

out_px = out.load()
blend = 12
for y in range(H):
    for i in range(blend):
        t = (i + 1) / (blend + 1)
        c0 = out_px[i, y]
        c1 = out_px[W - blend + i, y]
        m = tuple(int(c0[j] * (1 - t) + c1[j] * t) for j in range(3))
        out_px[i, y] = m
        out_px[W - blend + i, y] = m

path = r"C:\_s&box\under_pressure\Assets\textures\sky\up_vibrant_sky.png"
out.save(path, optimize=True)
print("saved", path, out.size)
