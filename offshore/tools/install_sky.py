"""Install sky / sun / moon / star / cloud PNGs for OFFSHORE day-night cycle."""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
HEROES = ROOT / "Assets" / "Art" / "heroes"
CURSOR = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects\assets")
ENV = ROOT / "Assets" / "textures" / "env"
ENV.mkdir(parents=True, exist_ok=True)
HEROES.mkdir(parents=True, exist_ok=True)


def sync():
    if not CURSOR.exists():
        return
    for src in CURSOR.glob("hero_sky*.png"):
        (HEROES / src.name).write_bytes(src.read_bytes())
        print("sync", src.name)
    for name in ("hero_sun.png", "hero_moon.png", "hero_stars.png", "hero_clouds_day.png", "hero_clouds_sunset.png"):
        src = CURSOR / name
        if src.exists():
            (HEROES / name).write_bytes(src.read_bytes())
            print("sync", name)


def find(*names: str) -> Path | None:
    for n in names:
        for base in (HEROES, CURSOR):
            p = base / n
            if p.exists():
                return p
    return None


def save_nearest(img: Image.Image, path: Path, scale: int = 3):
    img = img.convert("RGBA")
    out = img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST)
    px = [(0, 0, 0, 0) if a < 12 else (r, g, b, 255 if a > 180 else a) for r, g, b, a in out.getdata()]
    out.putdata(px)
    out.save(path)
    print(f"  -> {path.relative_to(ROOT)} {out.size}")


def knock_white(img: Image.Image, tol: int = 32) -> Image.Image:
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    q = deque()
    vis = [[False] * h for _ in range(w)]

    def bg(r, g, b, a):
        if a < 8:
            return True
        if r > 255 - tol and g > 255 - tol and b > 255 - tol:
            return True
        if abs(r - g) < 12 and abs(g - b) < 12 and r > 210:
            return True
        return False

    for sx, sy in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1), (w // 2, 0)):
        q.append((sx, sy))
        vis[sx][sy] = True
    while q:
        x, y = q.popleft()
        r, g, b, a = px[x, y]
        if bg(r, g, b, a):
            px[x, y] = (0, 0, 0, 0)
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= nx < w and 0 <= ny < h and not vis[nx][ny]:
                    vis[nx][ny] = True
                    q.append((nx, ny))
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a and bg(r, g, b, a):
                px[x, y] = (0, 0, 0, 0)
    return img


def auto_crop(img: Image.Image, pad: int = 2) -> Image.Image:
    bbox = img.split()[-1].getbbox()
    if not bbox:
        return img
    l, t, r, b = bbox
    return img.crop((max(0, l - pad), max(0, t - pad), min(img.width, r + pad), min(img.height, b + pad)))


def outline(img: Image.Image, color=(40, 32, 36, 255)) -> Image.Image:
    a = img.split()[-1]
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    op, ip, ap = out.load(), img.load(), a.load()
    for y in range(img.height):
        for x in range(img.width):
            if ap[x, y] < 20:
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < img.width and 0 <= ny < img.height and ap[nx, ny] > 40:
                        op[x, y] = color
                        break
            else:
                op[x, y] = ip[x, y]
    return out


def install_sky_plate(src_name: str, dst_name: str, tw=160, th=96):
    src = find(src_name)
    if not src:
        print("MISSING", src_name)
        return
    img = Image.open(src).convert("RGBA")
    # Keep full plate — no knockout (sky is opaque gradient)
    small = img.resize((tw, th), Image.Resampling.BOX)
    save_nearest(small, ENV / dst_name, 3)


def install_sprite(src_name: str, dst: Path, tw: int, th: int, do_outline=True):
    src = find(src_name)
    if not src:
        print("MISSING", src_name)
        return
    img = knock_white(Image.open(src))
    img = auto_crop(img, 4)
    small = img.resize((tw, th), Image.Resampling.BOX)
    small.putdata([(0, 0, 0, 0) if a < 40 else (r, g, b, 255) for r, g, b, a in small.getdata()])
    if do_outline:
        small = outline(small)
    save_nearest(small, dst, 4)


def connected(img: Image.Image, min_area=80):
    w, h = img.size
    ap = img.split()[-1].load()
    seen = [[False] * h for _ in range(w)]
    comps = []
    for y in range(h):
        for x in range(w):
            if seen[x][y] or ap[x, y] < 40:
                continue
            q = deque([(x, y)])
            seen[x][y] = True
            cells = []
            while q:
                cx, cy = q.popleft()
                cells.append((cx, cy))
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and ap[nx, ny] >= 40:
                        seen[nx][ny] = True
                        q.append((nx, ny))
            if len(cells) >= min_area:
                xs = [c[0] for c in cells]
                ys = [c[1] for c in cells]
                comps.append((min(xs), min(ys), max(xs), max(ys), len(cells)))
    comps.sort(key=lambda c: (c[1] // 60, c[0]))
    return comps


def slice_pack(src_name: str, names: list[str], size: tuple[int, int], prefix: str):
    src = find(src_name)
    if not src:
        print("MISSING", src_name)
        return
    img = knock_white(Image.open(src))
    work = knock_white(img.resize((img.width // 2, img.height // 2), Image.Resampling.BILINEAR))
    comps = connected(work, min_area=100)
    comps = sorted(comps, key=lambda c: -c[4])[: len(names)]
    comps.sort(key=lambda c: (c[1] // 50, c[0]))
    tw, th = size
    for i, (x0, y0, x1, y1, _) in enumerate(comps):
        if i >= len(names):
            break
        crop = auto_crop(img.crop((x0 * 2, y0 * 2, (x1 + 1) * 2, (y1 + 1) * 2)), 2)
        spr = crop.resize((tw, th), Image.Resampling.BOX)
        spr.putdata([(0, 0, 0, 0) if a < 40 else (r, g, b, 255) for r, g, b, a in spr.getdata()])
        spr = outline(spr)
        save_nearest(spr, ENV / f"{prefix}{names[i]}.png", 4)


def main():
    sync()
    print("Sky plates...")
    install_sky_plate("hero_sky_day.png", "sky_day.png", 180, 100)
    install_sky_plate("hero_sky_dawn.png", "sky_dawn.png", 180, 100)
    install_sky_plate("hero_sky_sunset.png", "sky_sunset.png", 180, 100)
    install_sky_plate("hero_sky_night.png", "sky_night.png", 180, 100)
    # legacy alias
    if (ENV / "sky_sunset.png").exists():
        pass

    print("Celestial...")
    # Prefer single clean sprites; pack-sliced stars look broken.
    sun = "hero_sun_clean.png" if find("hero_sun_clean.png") else "hero_sun.png"
    moon = "hero_moon_clean.png" if find("hero_moon_clean.png") else "hero_moon.png"
    install_sprite(sun, ENV / "sun.png", 32, 32, do_outline=False)
    install_sprite(moon, ENV / "moon.png", 28, 28, do_outline=True)

    print("Stars...")
    if find("hero_star_clean.png") and find("hero_star_dot.png"):
        install_sprite("hero_star_clean.png", ENV / "star_a.png", 10, 10, do_outline=False)
        install_sprite("hero_star_dot.png", ENV / "star_b.png", 6, 6, do_outline=False)
        (ENV / "star_c.png").write_bytes((ENV / "star_a.png").read_bytes())
        (ENV / "star_d.png").write_bytes((ENV / "star_b.png").read_bytes())
    else:
        slice_pack("hero_stars.png", ["a", "b", "c", "d"], (8, 8), "star_")
    if (ENV / "star_a.png").exists():
        (ENV / "star.png").write_bytes((ENV / "star_a.png").read_bytes())

    print("Clouds...")
    slice_pack("hero_clouds_day.png", ["a", "b", "c"], (56, 28), "cloud_")
    slice_pack("hero_clouds_sunset.png", ["a", "b", "c"], (56, 28), "cloud_sunset_")

    print("Done sky kit.")


if __name__ == "__main__":
    main()
