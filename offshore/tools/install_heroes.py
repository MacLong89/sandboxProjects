"""
Install generated hero PNGs into OFFSHORE textures (transparent game sprites).

No procedural art — only converts AI/hero plates: knock background, crop,
nearest-neighbor pixelize, optional outline. Prefer *_goal.png when present.
"""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
HEROES = ROOT / "Assets" / "Art" / "heroes"
CURSOR = Path(r"C:\Users\Macra\.cursor\projects\c-Users-Macra-Projects-sandboxProjects\assets")
ART = ROOT / "Assets" / "textures" / "art"
UI = ROOT / "Assets" / "textures" / "ui"
FISH = ROOT / "Assets" / "textures" / "fish"
BOATS = ROOT / "Assets" / "textures" / "boats"
ENV = ROOT / "Assets" / "textures" / "env"

for d in (ART, UI, FISH, BOATS, ENV, HEROES):
    d.mkdir(parents=True, exist_ok=True)


def sync_heroes():
    if not CURSOR.exists():
        return
    for src in CURSOR.glob("hero_*.png"):
        (HEROES / src.name).write_bytes(src.read_bytes())
        print(f"sync {src.name}")


def find_hero(*names: str) -> Path | None:
    for name in names:
        for base in (HEROES, CURSOR):
            p = base / name
            if p.exists():
                return p
    return None


def save_px(img: Image.Image, path: Path, scale: int = 4):
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    out = img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST)
    px = []
    for r, g, b, a in out.getdata():
        px.append((0, 0, 0, 0) if a < 12 else (r, g, b, 255 if a > 180 else a))
    out.putdata(px)
    path.parent.mkdir(parents=True, exist_ok=True)
    out.save(path)
    print(f"  -> {path.relative_to(ROOT)} ({out.width}x{out.height})")


def is_bg(r, g, b, a, seed_rgb, tol=38):
    if a < 8:
        return True
    if r > 235 and g > 235 and b > 235:
        return True
    if abs(r - g) < 10 and abs(g - b) < 10 and r > 210:
        return True
    if abs(r - g) < 12 and abs(g - b) < 12 and 150 < r < 230:
        return True
    dr, dg, db = abs(r - seed_rgb[0]), abs(g - seed_rgb[1]), abs(b - seed_rgb[2])
    if dr + dg + db < tol * 3 and max(dr, dg, db) < tol + 10:
        return True
    return False


def knock_bg(img: Image.Image, aggressive=True) -> Image.Image:
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1), (w // 2, 0), (0, h // 2)]
    visited = [[False] * h for _ in range(w)]
    q = deque()
    for sx, sy in seeds:
        r, g, b, a = px[sx, sy]
        seed = (r, g, b)
        if not is_bg(r, g, b, a, seed, tol=55):
            if r + g + b < 580:
                continue
        q.append((sx, sy, seed))
        visited[sx][sy] = True
    while q:
        x, y, seed = q.popleft()
        r, g, b, a = px[x, y]
        if is_bg(r, g, b, a, seed, tol=42):
            px[x, y] = (0, 0, 0, 0)
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny]:
                    visited[nx][ny] = True
                    q.append((nx, ny, seed))
    if aggressive:
        for y in range(h):
            for x in range(w):
                r, g, b, a = px[x, y]
                if a and is_bg(r, g, b, a, (255, 255, 255), tol=30):
                    px[x, y] = (0, 0, 0, 0)
    return img


def auto_crop(img: Image.Image, pad: int = 3) -> Image.Image:
    bbox = img.split()[-1].getbbox()
    if not bbox:
        return img
    l, t, r, b = bbox
    return img.crop((max(0, l - pad), max(0, t - pad), min(img.width, r + pad), min(img.height, b + pad)))


def outline(img: Image.Image, color=(36, 28, 32, 255)) -> Image.Image:
    img = img.convert("RGBA")
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


def to_sprite(src: Path, tw: int, th: int, do_outline=True, knock=True) -> Image.Image:
    img = Image.open(src).convert("RGBA")
    if knock:
        img = knock_bg(img)
    img = auto_crop(img, 6)
    rgb = Image.new("RGB", img.size, (0, 0, 0))
    rgb.paste(img, mask=img.split()[-1])
    rgb = ImageEnhance.Contrast(rgb).enhance(1.18)
    rgb = ImageEnhance.Color(rgb).enhance(1.1)
    rgb = rgb.filter(ImageFilter.UnsharpMask(radius=1.1, percent=110, threshold=2))
    composed = Image.new("RGBA", img.size, (0, 0, 0, 0))
    composed.paste(rgb, mask=img.split()[-1])
    small = composed.resize((tw, th), Image.Resampling.BOX)
    px = [(0, 0, 0, 0) if a < 40 else (r, g, b, 255) for r, g, b, a in small.getdata()]
    small.putdata(px)
    if do_outline:
        small = outline(small)
    return small


def connected_components(img: Image.Image, min_area=80):
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
    comps.sort(key=lambda c: (c[1] // 80, c[0]))
    return comps


def slice_named_pack(src: Path, names: list[str], size_map: dict, min_area=100, scale=4):
    img = knock_bg(Image.open(src).convert("RGBA"))
    work = knock_bg(img.resize((img.width // 2, img.height // 2), Image.Resampling.BILINEAR))
    comps = connected_components(work, min_area=min_area)
    comps = sorted(comps, key=lambda c: -c[4])[: len(names)]
    comps.sort(key=lambda c: (c[1] // 60, c[0]))
    for i, (x0, y0, x1, y1, _) in enumerate(comps):
        if i >= len(names):
            break
        name = names[i]
        if name not in size_map:
            continue
        dst, tw, th = size_map[name]
        crop = auto_crop(img.crop((x0 * 2, y0 * 2, (x1 + 1) * 2, (y1 + 1) * 2)), 2)
        spr = crop.resize((tw, th), Image.Resampling.BOX)
        spr.putdata([(0, 0, 0, 0) if a < 40 else (r, g, b, 255) for r, g, b, a in spr.getdata()])
        save_px(outline(spr), dst, scale)


def install_player_anims(low: Image.Image):
    for f in range(4):
        frame = Image.new("RGBA", low.size, (0, 0, 0, 0))
        dy = [0, 1, 0, -1][f]
        frame.paste(low, (0, dy))
        save_px(frame, ART / f"player_idle_{f}.png", 4)
        walk = Image.new("RGBA", low.size, (0, 0, 0, 0))
        walk.paste(low, ([0, 1, 0, -1][f], dy))
        save_px(walk, ART / f"player_walk_{f}.png", 4)
    for f in range(3):
        cast = Image.new("RGBA", low.size, (0, 0, 0, 0))
        cast.paste(low, (1 + f, -1))
        save_px(cast, ART / f"player_cast_{f}.png", 4)
        reel = Image.new("RGBA", low.size, (0, 0, 0, 0))
        reel.paste(low, (0, f % 2))
        save_px(reel, ART / f"player_reel_{f}.png", 4)
    for name in ("player_hook", "player_hold", "player_celebrate"):
        save_px(low, ART / f"{name}.png", 4)


def main():
    sync_heroes()
    print("Installing GOAL hero sprites...")

    jobs = [
        (("hero_shop_dock_v2.png", "hero_shop_dock.png", "hero_shop_goal.png"), ENV / "shop_dock.png", 480, 210, True, 2, True),
        (("hero_shop_dock_v2.png", "hero_shop_dock.png", "hero_shop_goal.png"), ENV / "shop_exterior.png", 480, 210, True, 2, True),
        (("hero_boat_clean.png", "hero_boat_goal.png", "hero_dinghy.png"), BOATS / "boat_dinghy.png", 72, 40, True, 3, True),
        (("hero_fisher17.png", "hero_boat_clean.png"), BOATS / "boat_fisher17.png", 80, 44, True, 3, True),
        (("hero_triton.png", "hero_boat_clean.png"), BOATS / "boat_triton.png", 88, 48, True, 3, True),
        (("hero_player_goal.png", "hero_player.png"), ART / "player_idle_0.png", 32, 40, True, 4, True),
        (("hero_sky_goal.png", "hero_sky.png"), ENV / "sky_sunset.png", 220, 130, False, 3, False),
        (("hero_dock_goal.png",), ENV / "dock_plank.png", 160, 72, True, 3, True),
        (("hero_pillar.png",), ENV / "dock_pillar.png", 20, 64, True, 4, True),
        (("hero_chest_goal.png",), ENV / "treasure_chest.png", 28, 24, True, 4, True),
        (("hero_coral_goal.png", "hero_coral.png"), ENV / "coral.png", 28, 24, True, 4, True),
        (("hero_kelp_goal.png",), ENV / "kelp.png", 24, 40, True, 4, True),
        (("hero_godrays_goal.png",), ENV / "underwater_ray.png", 48, 96, False, 3, True),
    ]

    player_low = None
    for names, dst, tw, th, ol, scale, knock in jobs:
        src = find_hero(*names)
        if src is None:
            print(f"MISSING {names[0]}")
            continue
        spr = to_sprite(src, tw, th, do_outline=ol, knock=knock)
        if "player" in names[0]:
            player_low = spr
        save_px(spr, dst, scale)

    # Larger boats variants from dinghy/fisher
    if (BOATS / "boat_fisher17.png").exists():
        base = Image.open(BOATS / "boat_fisher17.png").convert("RGBA")
        px = [(0, 0, 0, 0) if a < 16 else (max(0, r - 25), max(0, g - 12), min(255, b + 18), a) for r, g, b, a in base.getdata()]
        base.putdata(px)
        base.save(BOATS / "boat_seawolf.png")
        print("  -> boats/boat_seawolf.png")

    if player_low is not None:
        print("Player anims...")
        install_player_anims(player_low)

    # Underwater plate → water + seabed layers (keep full plate colors, light knock)
    uw = find_hero("hero_underwater_goal.png", "hero_water.png")
    if uw:
        print("Underwater layers...")
        full = Image.open(uw).convert("RGBA")
        # Don't flood-fill blue water away — only knock pure white corners
        full = knock_bg(full, aggressive=False)
        w, h = full.size
        # surface band (top third) with alpha for overlay feel
        surf = full.crop((0, 0, w, int(h * 0.45))).resize((180, 72), Image.Resampling.BOX)
        save_px(surf, ENV / "water_surface.png", 3)
        mid = full.crop((0, int(h * 0.2), w, int(h * 0.75))).resize((180, 90), Image.Resampling.BOX)
        save_px(mid, ENV / "water_deep.png", 3)
        bed = full.crop((0, int(h * 0.55), w, h)).resize((180, 56), Image.Resampling.BOX)
        save_px(bed, ENV / "seabed_sand.png", 3)
        dark = Image.new("RGBA", bed.size)
        dark.putdata([(0, 0, 0, 0) if a < 16 else (r * 3 // 4, g * 3 // 4, b * 3 // 4, a) for r, g, b, a in bed.getdata()])
        save_px(dark, ENV / "seabed_rock.png", 3)
        save_px(dark, ENV / "seabed_deep.png", 3)

    # Island from sky lower-right
    sky = find_hero("hero_sky_goal.png", "hero_sky.png")
    if sky:
        s = Image.open(sky).convert("RGBA")
        w, h = s.size
        crop = auto_crop(s.crop((int(w * 0.55), int(h * 0.4), int(w * 0.98), int(h * 0.88))), 2)
        # isolate dark island vs bright sky: keep darker pixels
        out = Image.new("RGBA", crop.size, (0, 0, 0, 0))
        op = out.load()
        cp = crop.load()
        for y in range(crop.height):
            for x in range(crop.width):
                r, g, b, a = cp[x, y]
                if a < 10:
                    continue
                # sky is bright warm; island is darker silhouette
                if r + g + b < 420 or (b > r and b > 100):
                    op[x, y] = (r, g, b, 255)
                elif r < 160 and g < 140:
                    op[x, y] = (r, g, b, 255)
        out = auto_crop(out, 2)
        if out.split()[-1].getbbox():
            spr = out.resize((72, 40), Image.Resampling.BOX)
            spr.putdata([(0, 0, 0, 0) if a < 40 else (r, g, b, 255) for r, g, b, a in spr.getdata()])
            save_px(outline(spr), ENV / "island_far.png", 3)
            save_px(outline(spr), ENV / "lighthouse.png", 3)

    # Clouds pack
    clouds = find_hero("hero_clouds_goal.png")
    if clouds:
        print("Clouds...")
        slice_named_pack(
            clouds,
            ["cloud_a", "cloud_b", "cloud_c"],
            {
                "cloud_a": (ENV / "cloud_a.png", 48, 24),
                "cloud_b": (ENV / "cloud_b.png", 52, 26),
                "cloud_c": (ENV / "cloud_c.png", 44, 22),
            },
            min_area=200,
        )

    # Props leftovers from older pack if present
    props = find_hero("hero_props.png")
    if props:
        slice_named_pack(
            props,
            ["barrel", "crate", "lamp", "lifering", "rope", "kelp2", "coral2", "rock"],
            {
                "barrel": (ENV / "barrel.png", 20, 24),
                "crate": (ENV / "crate.png", 22, 18),
                "lamp": (ENV / "lamp.png", 16, 28),
                "lifering": (ENV / "lifering.png", 18, 18),
                "rope": (ENV / "rope.png", 18, 14),
                "rock": (ENV / "rock.png", 22, 16),
            },
            min_area=120,
        )

    # UI hotbar icons
    ui = find_hero("hero_ui_icons.png")
    if ui:
        print("UI icons...")
        slice_named_pack(
            ui,
            ["icon_rod", "icon_reel", "icon_hook", "icon_line", "icon_bait_worm", "icon_net", "icon_lantern", "boat_icon"],
            {
                "icon_rod": (UI / "icon_rod.png", 24, 24),
                "icon_reel": (UI / "icon_reel.png", 24, 24),
                "icon_hook": (UI / "icon_hook.png", 24, 24),
                "icon_line": (UI / "icon_line.png", 24, 24),
                "icon_bait_worm": (UI / "icon_bait_worm.png", 24, 24),
                "icon_net": (UI / "icon_net.png", 24, 24),
                "icon_lantern": (UI / "icon_lantern.png", 24, 24),
                "boat_icon": (UI / "icon_boat.png", 24, 24),
            },
            min_area=80,
            scale=4,
        )
        # aliases used by catalog / HUD
        for src, dst in (
            ("icon_hook.png", "hook_icon.png"),
            ("icon_bait_worm.png", "icon_bait_worm.png"),
        ):
            p = UI / src
            if p.exists():
                (UI / dst).write_bytes(p.read_bytes())

    fish = find_hero("hero_fish.png")
    if fish:
        print("Fish...")
        slice_named_pack(
            fish,
            ["sardine", "mackerel", "seabass", "redsnapper"],
            {
                "sardine": (FISH / "sardine.png", 28, 14),
                "mackerel": (FISH / "mackerel.png", 32, 16),
                "seabass": (FISH / "seabass.png", 34, 18),
                "redsnapper": (FISH / "redsnapper.png", 32, 18),
            },
            min_area=80,
        )

    # Foam line from a thin white strip — use soft semi-transparent from water surface top
    foam = Image.new("RGBA", (160, 8), (0, 0, 0, 0))
    fp = foam.load()
    for x in range(160):
        hgt = 2 + (x * 5) % 3
        for y in range(hgt):
            fp[x, 4 - y] = (230, 245, 255, 190 - y * 40)
    save_px(foam, ENV / "foam_line.png", 3)

    # Sun from sky
    if sky:
        s = Image.open(sky).convert("RGBA")
        w, h = s.size
        # bright circle region — crop center-right warm area
        sun = s.crop((int(w * 0.55), int(h * 0.35), int(w * 0.85), int(h * 0.7)))
        # keep only very bright warm pixels
        out = Image.new("RGBA", sun.size, (0, 0, 0, 0))
        op, sp = out.load(), sun.load()
        cx, cy = sun.width // 2, sun.height // 2
        for y in range(sun.height):
            for x in range(sun.width):
                r, g, b, a = sp[x, y]
                if r > 200 and g > 140 and (x - cx) ** 2 + (y - cy) ** 2 < (min(cx, cy) * 0.7) ** 2:
                    op[x, y] = (r, g, b, 255)
        out = auto_crop(out, 2)
        if out.split()[-1].getbbox():
            save_px(out.resize((32, 32), Image.Resampling.BOX), ENV / "sun.png", 3)

    print("Done. Fully reopen offshore.scene so textures remount.")


if __name__ == "__main__":
    main()
