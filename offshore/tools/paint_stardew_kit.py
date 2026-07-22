"""DEPRECATED for shipping look — overwrites hero installs with tiny blocks.

Use: python tools/install_heroes.py
"""
raise SystemExit("Use tools/install_heroes.py instead of paint_stardew_kit.py")
# Hand-authored fallback (disabled):
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1] / "Assets" / "textures"
ENV, ART, BOATS = ROOT / "env", ROOT / "art", ROOT / "boats"
for d in (ENV, ART, BOATS):
    d.mkdir(parents=True, exist_ok=True)

INK = (36, 28, 32, 255)
W1, W2, W3, W4 = (90, 58, 38, 255), (130, 86, 52, 255), (170, 118, 70, 255), (210, 160, 100, 255)
SKY = None


def save(img, path, scale=4):
    out = img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST)
    out.putdata([(0, 0, 0, 0) if p[3] == 0 else p for p in out.getdata()])
    out.save(path)


def im(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def rect(img, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(img, x, y, c)


def outline_opaque(img):
    a = img.split()[-1]
    out = img.copy()
    for y in range(img.height):
        for x in range(img.width):
            if a.getpixel((x, y)) < 10:
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < img.width and 0 <= ny < img.height and a.getpixel((nx, ny)) > 20:
                        put(out, x, y, INK)
                        break
    return out


# --- Shop (side view, cozy) ---
s = im(64, 52)
# stilts
rect(s, 10, 38, 13, 50, W2)
rect(s, 50, 38, 53, 50, W2)
# floor
rect(s, 6, 36, 58, 40, W3)
# walls
rect(s, 8, 16, 56, 36, W3)
rect(s, 8, 16, 56, 18, W2)
# roof
for i, y in enumerate(range(6, 17)):
    x0 = 6 + i
    x1 = 58 - i
    rect(s, x0, y, x1, y, (70, 120, 80, 255) if i > 2 else (50, 90, 60, 255))
# open front shadow
rect(s, 14, 20, 40, 35, (50, 32, 24, 255))
# shelves + jars
for y in (22, 27, 32):
    rect(s, 16, y, 38, y + 1, W2)
    for j, col in enumerate([(220, 80, 80), (80, 160, 200), (230, 190, 70), (90, 180, 100)]):
        put(s, 18 + j * 5, y - 2, (*col, 255))
        put(s, 19 + j * 5, y - 2, (*col, 255))
# window
rect(s, 44, 22, 52, 30, INK)
rect(s, 45, 23, 51, 29, (150, 210, 230, 255))
# door glow
rect(s, 20, 28, 28, 35, (255, 200, 100, 255))
# lifering
for x, y in ((48, 32), (49, 32), (48, 33), (49, 33), (47, 32), (50, 32), (48, 31), (48, 34)):
    put(s, x, y, (220, 70, 70, 255))
save(outline_opaque(s), ENV / "shop_exterior.png", 3)

# --- Dock plank strip ---
d = im(128, 12)
for i in range(16):
    c = W3 if i % 2 == 0 else W2
    rect(d, i * 8, 2, i * 8 + 7, 10, c)
    for y in range(2, 11):
        put(d, i * 8, y, W1)
save(d, ENV / "dock_plank.png", 4)

# --- Pillar ---
p = im(10, 48)
rect(p, 3, 0, 6, 47, W2)
rect(p, 3, 0, 6, 3, W3)
rect(p, 3, 22, 6, 28, (50, 100, 70, 255))
save(outline_opaque(p), ENV / "dock_pillar.png", 4)

# --- Player (readable Stardew-ish) ---
def player_frame(mode="idle", f=0):
    a = im(20, 24)
    bob = [0, 1, 0, -1][f % 4]
    leg = [0, 1, 0, -1][f % 4] if mode == "walk" else 0
    # legs
    rect(a, 6 + leg, 16 + bob, 8 + leg, 22 + bob, (45, 48, 60, 255))
    rect(a, 11 - leg, 16 + bob, 13 - leg, 22 + bob, (45, 48, 60, 255))
    # body
    rect(a, 5, 9 + bob, 14, 17 + bob, (55, 120, 190, 255))
    rect(a, 5, 9 + bob, 14, 11 + bob, (40, 95, 160, 255))
    # head
    rect(a, 6, 4 + bob, 13, 10 + bob, (230, 180, 140, 255))
    # cap
    rect(a, 5, 3 + bob, 14, 6 + bob, (35, 80, 150, 255))
    rect(a, 13, 4 + bob, 16, 6 + bob, (35, 80, 150, 255))  # brim
    # arm
    arm = 0 if mode == "idle" else (-2 if mode == "cast" else [0, 1, 0, -1][f % 4])
    rect(a, 14, 10 + bob + arm, 17, 13 + bob + arm, (230, 180, 140, 255))
    if mode in ("cast", "reel"):
        for yy in range(4, 12):
            put(a, 17, yy + bob + arm, (120, 80, 50, 255))
    if mode == "hold":
        rect(a, 15, 12 + bob, 18, 15 + bob, (80, 160, 180, 255))
    return outline_opaque(a)


for f in range(4):
    save(player_frame("idle", f), ART / f"player_idle_{f}.png", 4)
    save(player_frame("walk", f), ART / f"player_walk_{f}.png", 4)
for f in range(3):
    save(player_frame("cast", f), ART / f"player_cast_{f}.png", 4)
    save(player_frame("reel", f), ART / f"player_reel_{f}.png", 4)
save(player_frame("hold"), ART / "player_hold.png", 4)
save(player_frame("cast", 1), ART / "player_hook.png", 4)
save(player_frame("hold"), ART / "player_celebrate.png", 4)

# --- Boats (shared waterline at bottom-8) ---
def boat(kind):
    sizes = {"dinghy": (48, 26), "fisher17": (56, 30), "seawolf": (64, 32), "triton": (72, 36)}
    w, h = sizes[kind]
    a = im(w, h)
    wl = h - 9
    hull = {"dinghy": (50, 100, 170), "fisher17": (45, 55, 70), "seawolf": (35, 55, 75), "triton": (30, 45, 65)}[kind]
    bottom = (180, 60, 55)
    # hull body
    for x in range(4, w - 4):
        for y in range(wl, h - 2):
            put(a, x, y, (*hull, 255))
    for x in range(6, w - 6):
        for y in range(wl + 2, h - 2):
            put(a, x, y, (*bottom, 255))
    # taper bow/stern
    for y in range(wl, h - 2):
        put(a, 3, y, (*hull, 255))
        put(a, w - 4, y, (*hull, 255))
    # deck
    rect(a, 5, wl - 2, w - 6, wl, (230, 220, 200, 255))
    # cabin
    if kind == "dinghy":
        rect(a, 18, wl - 9, 30, wl - 1, (240, 240, 235, 255))
        rect(a, 20, wl - 7, 26, wl - 4, (100, 180, 210, 255))
    else:
        cw = 14 if kind == "fisher17" else 18
        rect(a, w // 2 - 4, wl - 14, w // 2 - 4 + cw, wl - 1, (245, 245, 240, 255))
        rect(a, w // 2 - 1, wl - 11, w // 2 + 5, wl - 6, (90, 170, 200, 255))
    # mast
    rect(a, w - 14, wl - 16, w - 13, wl - 1, W2)
    # lifering
    put(a, 10, wl - 5, (220, 70, 70, 255))
    put(a, 11, wl - 5, (220, 70, 70, 255))
    put(a, 10, wl - 4, (240, 230, 210, 255))
    # engine
    rect(a, 2, wl - 4, 7, wl + 1, (55, 60, 70, 255))
    return outline_opaque(a)


for k in ("dinghy", "fisher17", "seawolf", "triton"):
    save(boat(k), BOATS / f"boat_{k}.png", 3)

# Sky sunset band
sky = im(160, 100)
d = ImageDraw.Draw(sky)
for y in range(100):
    t = y / 99
    if t < 0.4:
        u = t / 0.4
        c = tuple(int(a + (b - a) * u) for a, b in zip((70, 50, 120), (255, 110, 90)))
    elif t < 0.75:
        u = (t - 0.4) / 0.35
        c = tuple(int(a + (b - a) * u) for a, b in zip((255, 110, 90), (255, 170, 80)))
    else:
        u = (t - 0.75) / 0.25
        c = tuple(int(a + (b - a) * u) for a, b in zip((255, 170, 80), (40, 60, 120)))
    d.rectangle((0, y, 160, y), fill=c + (255,))
save(sky, ENV / "sky_sunset.png", 3)

# Water
water = im(96, 72)
d = ImageDraw.Draw(water)
for y in range(72):
    t = y / 71
    c = tuple(int(a + (b - a) * t) for a, b in zip((50, 150, 175), (15, 40, 85)))
    d.rectangle((0, y, 96, y), fill=c + (235,))
for x in range(0, 96, 2):
    put(water, x, 0, (230, 245, 255, 255))
save(water, ENV / "water_surface.png", 3)

# Foam
foam = im(128, 8)
for x in range(128):
    h = 1 + (x * 5) % 3
    for y in range(h):
        put(foam, x, 3 - y, (235, 245, 255, 210))
save(foam, ENV / "foam_line.png", 3)

print("Painted Stardew kit OK")
