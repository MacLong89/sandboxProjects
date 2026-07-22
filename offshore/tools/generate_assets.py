"""Generate all OFFSHORE pixel-art sprites and simple WAV sounds.

Cozy sunset fishing aesthetic: orange/pink/purple sky, wooden dock & shop,
side-on boats (no player baked in), readable blue-cap fisherman, foam water.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math, random, struct, wave

ROOT = Path(__file__).resolve().parents[1] / "Assets"
ART = ROOT / "textures" / "art"
UI = ROOT / "textures" / "ui"
FISH = ROOT / "textures" / "fish"
BOATS = ROOT / "textures" / "boats"
ENV = ROOT / "textures" / "env"
SND = ROOT / "sounds"
for d in (ART, UI, FISH, BOATS, ENV, SND):
    d.mkdir(parents=True, exist_ok=True)

# Sunset / dock palette (hex)
C = {
    "ink": "#1a1020",
    "wood": "#9a6238",
    "wood2": "#6e3c20",
    "wood3": "#c89254",
    "wood4": "#4a2814",
    "plank": "#b07840",
    "foam": "#e8f8f4",
    "foam2": "#b8e8e0",
    "teal": "#2a8a9a",
    "teal2": "#1a6a7a",
    "deep": "#123858",
    "abyss": "#0a1830",
    "water": "#1e6a88",
    "water2": "#3a9ab0",
    "sand": "#d4b878",
    "sand2": "#a88850",
    "kelp": "#2f7a4a",
    "kelp2": "#1e5a32",
    "coral": "#d4587a",
    "sky_top": "#3a1a5a",
    "sky_mid": "#c04888",
    "sky_low": "#ff7a3a",
    "sky_horizon": "#ffb060",
    "pink": "#e06a9a",
    "purple": "#6a3a8a",
    "purple2": "#4a2868",
    "blue": "#3a6aaa",
    "cap": "#2a5aaa",
    "cap2": "#1a3a7a",
    "shirt": "#3a6aaa",
    "shirt2": "#2a4a88",
    "skin": "#e8b898",
    "skin2": "#c89070",
    "gold": "#e9ae35",
    "gold2": "#ffd070",
    "white": "#f4f1e8",
    "cream": "#fff0d8",
    "red": "#c84a4a",
    "red2": "#a03030",
    "green": "#4cae73",
    "gray": "#6a7080",
    "gray2": "#4a5060",
    "dark": "#243040",
    "shadow": "#1a1420",
}


def hex_rgba(h, a=255):
    h = h.lstrip("#")
    if len(h) == 8:
        return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), int(h[6:8], 16))
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def lerp_col(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)


def im(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def save(img, path, scale=4):
    """NEAREST upscale; force zero RGB on fully transparent pixels."""
    out = img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST)
    px = out.load()
    w, h = out.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                px[x, y] = (0, 0, 0, 0)
    out.save(path)


def rect(d, xy, c):
    if isinstance(c, str):
        c = hex_rgba(c)
    d.rectangle(xy, fill=c)


def ell(d, xy, c):
    if isinstance(c, str):
        c = hex_rgba(c)
    d.ellipse(xy, fill=c)


def poly(d, pts, c):
    if isinstance(c, str):
        c = hex_rgba(c)
    d.polygon(pts, fill=c)


def px(img, x, y, c):
    if isinstance(c, str):
        c = hex_rgba(c)
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)


def outline_poly(d, pts, fill, edge=None):
    poly(d, pts, fill)
    if edge:
        if isinstance(edge, str):
            edge = hex_rgba(edge)
        d.line(pts + [pts[0]], fill=edge, width=1)


def vgrad(img, colors):
    """Vertical gradient through list of (stop 0..1, hex) stops."""
    d = ImageDraw.Draw(img)
    h = img.height
    stops = [(s, hex_rgba(c)[:3]) for s, c in colors]
    for y in range(h):
        t = y / max(1, h - 1)
        for i in range(len(stops) - 1):
            t0, c0 = stops[i]
            t1, c1 = stops[i + 1]
            if t0 <= t <= t1 or i == len(stops) - 2:
                u = 0 if t1 == t0 else (t - t0) / (t1 - t0)
                u = max(0.0, min(1.0, u))
                col = lerp_col(c0 + (255,), c1 + (255,), u)
                d.line([(0, y), (img.width - 1, y)], fill=col)
                break


# ---------------------------------------------------------------------------
# Player (blue-cap fisherman) — side-on, readable silhouette
# ---------------------------------------------------------------------------
def player(frame, mode="idle"):
    a = im(28, 32)
    d = ImageDraw.Draw(a)
    bob = [0, 1, 0, -1][frame % 4]
    step = [0, 2, 0, -2][frame % 4] if mode == "walk" else 0
    arm = 0
    rod_up = False
    if mode == "cast":
        arm = [-2, -5, -7][min(frame, 2)]
        rod_up = frame >= 1
    elif mode == "reel":
        arm = [0, 2, -1][frame % 3]
    elif mode == "hook":
        arm = -3
        rod_up = True
    elif mode == "celebrate":
        arm = -8

    by = bob
    # shadow
    ell(d, (8, 29 + by, 20, 32 + by), hex_rgba(C["ink"], 60))

    # boots / legs
    lx0, lx1 = 9 + step, 12 + step
    rx0, rx1 = 15 - step, 18 - step
    rect(d, (lx0, 22 + by, lx1, 29 + by), C["dark"])
    rect(d, (rx0, 22 + by, rx1, 29 + by), C["dark"])
    rect(d, (lx0 - 1, 28 + by, lx1 + 1, 30 + by), C["ink"])  # boots
    rect(d, (rx0 - 1, 28 + by, rx1 + 1, 30 + by), C["ink"])

    # pants
    rect(d, (9, 18 + by, 18, 23 + by), C["gray2"])

    # torso (blue shirt)
    rect(d, (8, 11 + by, 19, 19 + by), C["shirt"])
    rect(d, (8, 11 + by, 10, 19 + by), C["shirt2"])  # shade
    # overalls straps
    rect(d, (10, 11 + by, 11, 16 + by), C["dark"])
    rect(d, (16, 11 + by, 17, 16 + by), C["dark"])

    # head
    ell(d, (9, 4 + by, 19, 14 + by), C["skin"])
    rect(d, (11, 10 + by, 13, 12 + by), C["skin2"])  # cheek
    px(a, 16, 8 + by, C["ink"])  # eye

    # blue baseball cap
    rect(d, (8, 3 + by, 20, 7 + by), C["cap"])
    rect(d, (8, 3 + by, 20, 5 + by), C["cap2"])
    poly(d, [(18, 5 + by), (24, 6 + by), (18, 8 + by)], C["cap"])  # brim
    # bill highlight
    px(a, 10, 4 + by, C["blue"])

    # arms + rod
    ay = 13 + by + arm
    if mode == "celebrate":
        # both arms up
        rect(d, (5, 6 + by, 8, 12 + by), C["skin"])
        rect(d, (19, 5 + by, 22, 12 + by), C["skin"])
        # fish held high
        ell(d, (20, 2 + by, 26, 7 + by), C["teal"])
        poly(d, [(20, 4 + by), (17, 2 + by), (17, 6 + by)], C["teal"])
    elif mode == "hold":
        rect(d, (18, ay, 23, ay + 3), C["skin"])
        ell(d, (20, 14 + by, 27, 20 + by), C["water2"])
        poly(d, [(21, 17 + by), (18, 14 + by), (18, 19 + by)], C["water2"])
        px(a, 25, 16 + by, C["ink"])
    else:
        # right arm forward
        rect(d, (18, ay, 24, ay + 3), C["skin"])
        # left arm tucked
        rect(d, (6, 13 + by, 9, 16 + by), C["skin"])
        if mode in ("cast", "reel", "hook", "idle", "walk"):
            # fishing rod
            rx, ry = 23, ay + 1
            if rod_up:
                d.line([(rx, ry), (rx + 1, ry - 10)], fill=hex_rgba(C["wood"]), width=1)
                d.line([(rx + 1, ry - 10), (rx + 2, ry - 14)], fill=hex_rgba(C["wood3"]), width=1)
            else:
                tip_y = ry - (2 if mode == "reel" else 0)
                tip_x = rx + 8
                d.line([(rx, ry), (tip_x, tip_y)], fill=hex_rgba(C["wood"]), width=1)
                # reel
                ell(d, (rx - 1, ry, rx + 3, ry + 4), C["gray"])
                if mode == "reel":
                    px(a, tip_x, tip_y, C["foam"])
    return a


# ---------------------------------------------------------------------------
# Boats — shared waterline, distinct silhouettes, no player, transparent BG
# ---------------------------------------------------------------------------
def boat(kind):
    # canvas tall enough for masts; waterline locked at y=28 for all
    w = {"dinghy": 56, "fisher17": 68, "seawolf": 80, "triton": 92}[kind]
    h = 40
    waterline = 28
    a = im(w, h)
    d = ImageDraw.Draw(a)

    hull = {
        "dinghy": C["blue"],
        "fisher17": "#2a3548",
        "seawolf": "#1e3848",
        "triton": "#1a2840",
    }[kind]
    hull_shade = {
        "dinghy": "#2a4a7a",
        "fisher17": "#1a2438",
        "seawolf": "#142830",
        "triton": "#101828",
    }[kind]
    accent = {
        "dinghy": C["cream"],
        "fisher17": C["red"],
        "seawolf": "#3aa0c0",
        "triton": C["gold"],
    }[kind]
    gunwale = {
        "dinghy": C["wood3"],
        "fisher17": C["white"],
        "seawolf": "#80c8d8",
        "triton": C["gold2"],
    }[kind]

    # keel / underwater hull
    bow_x = w - 6
    stern_x = 4
    poly(d, [
        (stern_x + 4, waterline),
        (bow_x - 2, waterline),
        (bow_x - 8, h - 4),
        (stern_x + 10, h - 4),
    ], hull_shade)
    # main hull above waterline strip
    poly(d, [
        (stern_x, waterline - 1),
        (bow_x, waterline - 1),
        (bow_x - 6, waterline + 6),
        (stern_x + 6, waterline + 6),
    ], hull)
    # gunwale stripe
    rect(d, (stern_x + 2, waterline - 3, bow_x - 2, waterline), gunwale)
    # accent stripe
    rect(d, (stern_x + 4, waterline + 1, bow_x - 8, waterline + 3), accent)

    # foam kiss at bow
    for i in range(3):
        ell(d, (bow_x - 10 - i * 3, waterline - 1, bow_x - 6 - i * 3, waterline + 3),
            hex_rgba(C["foam"], 160 - i * 40))

    if kind == "dinghy":
        # open rowboat — thwarts, oarlock, no cabin
        rect(d, (16, waterline - 6, 20, waterline - 1), C["wood"])
        rect(d, (30, waterline - 6, 34, waterline - 1), C["wood"])
        rect(d, (22, waterline - 8, 28, waterline - 2), C["wood3"])  # seat
        # small mast stub
        rect(d, (38, waterline - 12, 40, waterline - 1), C["wood2"])
        # outboard
        rect(d, (6, waterline - 5, 12, waterline + 2), C["gray2"])
        rect(d, (7, waterline + 2, 10, waterline + 5), C["dark"])
        # lifering on gunwale
        ell(d, (14, waterline - 9, 20, waterline - 3), C["red"])
        ell(d, (16, waterline - 7, 18, waterline - 5), (0, 0, 0, 0))

    elif kind == "fisher17":
        # center-console bay boat
        rect(d, (28, waterline - 14, 46, waterline - 1), C["white"])
        rect(d, (30, waterline - 12, 40, waterline - 7), C["teal"])  # windshield
        rect(d, (32, waterline - 11, 38, waterline - 8), hex_rgba("#80d0e8", 200))
        # T-top poles
        rect(d, (30, waterline - 20, 32, waterline - 14), C["gray"])
        rect(d, (42, waterline - 20, 44, waterline - 14), C["gray"])
        rect(d, (28, waterline - 22, 46, waterline - 19), C["dark"])
        # rod holders
        for x in (48, 52, 56):
            rect(d, (x, waterline - 10, x + 1, waterline - 1), C["gray2"])
        # console box
        rect(d, (34, waterline - 8, 42, waterline - 1), C["gray"])
        # outboard twin look
        rect(d, (6, waterline - 6, 14, waterline + 2), C["gray2"])
        rect(d, (8, waterline + 2, 12, waterline + 6), C["dark"])
        # name stripe only (no text)
        rect(d, (20, waterline + 2, 36, waterline + 3), C["red"])

    elif kind == "seawolf":
        # cabin cruiser
        rect(d, (30, waterline - 18, 58, waterline - 1), C["white"])
        rect(d, (32, waterline - 16, 54, waterline - 10), "#2a5070")  # windows
        for x in (34, 42, 50):
            rect(d, (x, waterline - 15, x + 5, waterline - 11), "#60a0c0")
        # flying bridge
        rect(d, (36, waterline - 24, 52, waterline - 18), C["white"])
        rect(d, (38, waterline - 22, 48, waterline - 19), C["teal"])
        # radar arch
        d.arc((40, waterline - 30, 56, waterline - 18), 200, 340, fill=hex_rgba(C["gray"]), width=1)
        # antenna
        rect(d, (50, waterline - 32, 51, waterline - 24), C["gray2"])
        # cabin door shade
        rect(d, (40, waterline - 9, 46, waterline - 1), C["gray2"])
        # hull portholes
        for x in (18, 24):
            ell(d, (x, waterline + 1, x + 4, waterline + 4), "#80c0d0")
        # engines
        rect(d, (4, waterline - 5, 14, waterline + 3), C["gray2"])
        # teal accent
        rect(d, (16, waterline - 2, bow_x - 10, waterline), "#3aa0c0")

    else:  # triton — luxury sportfisher
        # long foredeck
        poly(d, [
            (40, waterline - 1),
            (bow_x - 2, waterline - 1),
            (bow_x - 10, waterline - 6),
            (48, waterline - 8),
            (40, waterline - 6),
        ], "#2a3850")
        # tower / cabin
        rect(d, (34, waterline - 20, 62, waterline - 1), C["white"])
        rect(d, (36, waterline - 18, 58, waterline - 12), "#1a4060")
        for x in (38, 46, 54):
            rect(d, (x, waterline - 17, x + 5, waterline - 13), "#70b8d8")
        # tuna tower
        rect(d, (42, waterline - 30, 44, waterline - 20), C["gray"])
        rect(d, (54, waterline - 30, 56, waterline - 20), C["gray"])
        rect(d, (40, waterline - 32, 58, waterline - 29), C["dark"])
        rect(d, (44, waterline - 36, 54, waterline - 32), C["white"])
        # outriggers
        d.line([(62, waterline - 18), (78, waterline - 28)], fill=hex_rgba(C["gray"]), width=1)
        d.line([(34, waterline - 18), (20, waterline - 26)], fill=hex_rgba(C["gray"]), width=1)
        # gold trim
        rect(d, (stern_x + 4, waterline - 3, bow_x - 4, waterline - 1), C["gold"])
        rect(d, (stern_x + 6, waterline + 2, 30, waterline + 3), C["gold2"])
        # cockpit
        rect(d, (22, waterline - 8, 34, waterline - 1), "#304050")
        # engines
        rect(d, (4, waterline - 6, 16, waterline + 3), C["gray2"])
        rect(d, (6, waterline + 3, 10, waterline + 7), C["dark"])
        rect(d, (11, waterline + 3, 15, waterline + 7), C["dark"])

    return a


# ---------------------------------------------------------------------------
# Fish
# ---------------------------------------------------------------------------
def fish_sprite(name, color, long=False):
    w, h = (40, 16) if long else (32, 16)
    a = im(w, h)
    d = ImageDraw.Draw(a)
    body = color
    shade = C["ink"]
    # body
    ell(d, (8, 3, w - 5, h - 2), body)
    # belly highlight
    ell(d, (12, h // 2, w - 10, h - 3), hex_rgba(C["cream"], 90))
    # tail
    mid = h // 2
    poly(d, [(10, mid), (0, 2), (2, mid), (0, h - 2)], body)
    # dorsal
    if name not in ("flounder", "giantsquid"):
        poly(d, [(w // 2 - 2, 3), (w // 2 + 4, 0), (w // 2 + 6, 4)], body)
    # fins
    poly(d, [(w // 2, mid + 2), (w // 2 + 4, h - 1), (w // 2 - 2, mid + 3)], body)

    if name in ("swordfish", "marlin"):
        d.line([(w - 6, mid), (w - 1, mid - 4)], fill=hex_rgba(C["cream"]), width=1)
        d.line([(w - 6, mid), (w - 1, mid - 2)], fill=hex_rgba(C["white"]), width=1)
        # sail for marlin
        if name == "marlin":
            poly(d, [(14, 4), (22, 0), (28, 4)], "#1a4a6a")
    if name == "anglerfish":
        d.line([(w - 8, 2), (w - 2, 0)], fill=hex_rgba(C["gold"]), width=1)
        ell(d, (w - 3, 0, w - 1, 2), C["gold"])
        ell(d, (w - 14, 5, w - 10, 9), C["gold2"])  # lure glow eye area
    if name == "giantsquid":
        a = im(40, 20)
        d = ImageDraw.Draw(a)
        ell(d, (14, 2, 36, 16), body)
        ell(d, (30, 4, 38, 12), C["purple"])
        px(a, 34, 7, C["gold"])
        for i in range(5):
            x0 = 14
            y0 = 8 + i
            d.line([(x0, y0), (2 + i, 12 + i * 2)], fill=hex_rgba(body), width=1)
        return a
    if name == "oarfish":
        # long ribbon
        for x in range(8, w - 2, 3):
            rect(d, (x, 4 + (x // 3) % 2, x + 2, h - 3), body)
        poly(d, [(w - 4, 2), (w - 1, mid), (w - 4, h - 2)], C["pink"])
    if name == "mahi":
        poly(d, [(10, 3), (16, 0), (20, 4)], "#30a060")
        rect(d, (14, 5, 24, 8), "#f0d040")
    if name == "redsnapper":
        ell(d, (10, 4, w - 8, h - 3), "#e05050")
        ell(d, (14, mid, w - 12, h - 2), hex_rgba(C["cream"], 100))
    if name == "tuna":
        poly(d, [(w - 8, mid - 2), (w - 2, mid), (w - 8, mid + 2)], body)

    # eye
    ex = w - 12
    ell(d, (ex, mid - 2, ex + 3, mid + 1), C["white"])
    px(a, ex + 1, mid - 1, C["ink"])
    # gill line
    d.arc((ex - 6, 4, ex, h - 3), 90, 270, fill=hex_rgba(shade, 120), width=1)
    return a


# ---------------------------------------------------------------------------
# UI icons
# ---------------------------------------------------------------------------
def icon(kind):
    a = im(16, 16)
    d = ImageDraw.Draw(a)
    if kind == "coin":
        ell(d, (1, 1, 15, 15), C["gold"])
        ell(d, (3, 3, 13, 13), C["gold2"])
        ell(d, (5, 5, 11, 11), "#c48a20")
        px(a, 8, 7, C["gold2"])
    elif kind.startswith("weather"):
        if "storm" in kind:
            ell(d, (2, 2, 14, 11), C["gray2"])
            poly(d, [(8, 9), (5, 14), (9, 12), (7, 15), (12, 10)], C["gold"])
        elif "rain" in kind:
            ell(d, (2, 2, 14, 10), C["gray"])
            for x in (5, 8, 11):
                rect(d, (x, 11, x + 1, 14), C["teal"])
        elif "fog" in kind:
            for y in (5, 8, 11):
                rect(d, (2, y, 14, y + 2), hex_rgba(C["gray"], 180))
        elif "wind" in kind:
            d.arc((2, 3, 14, 13), 200, 340, fill=hex_rgba(C["foam"]), width=2)
            d.arc((4, 6, 12, 14), 210, 330, fill=hex_rgba(C["foam2"]), width=1)
        elif "cloudy" in kind:
            ell(d, (2, 5, 11, 13), C["white"])
            ell(d, (7, 3, 15, 12), C["cream"])
        else:  # clear
            ell(d, (3, 2, 13, 12), C["gold"])
            ell(d, (6, 5, 10, 9), C["gold2"])
            rect(d, (2, 12, 14, 14), C["teal"])
    elif "bait" in kind:
        key = kind.split("_")[-1]
        if key == "worm":
            d.line([(4, 8), (7, 5), (10, 9), (13, 6)], fill=hex_rgba(C["pink"]), width=2)
        elif key == "minnow":
            ell(d, (3, 5, 13, 12), C["teal"])
            poly(d, [(3, 8), (0, 5), (0, 11)], C["teal"])
            px(a, 11, 7, C["ink"])
        elif key == "shrimp":
            ell(d, (4, 5, 12, 12), C["coral"])
            d.line([(12, 7), (15, 4)], fill=hex_rgba(C["coral"]), width=1)
        elif key == "squid":
            ell(d, (5, 3, 12, 10), C["purple"])
            for i in range(3):
                d.line([(6 + i * 2, 10), (5 + i * 2, 14)], fill=hex_rgba(C["purple"]), width=1)
        elif key == "crab":
            ell(d, (5, 6, 11, 12), C["red"])
            d.line([(5, 8), (1, 5)], fill=hex_rgba(C["red"]), width=1)
            d.line([(11, 8), (15, 5)], fill=hex_rgba(C["red"]), width=1)
        elif key == "sardine":
            ell(d, (3, 5, 13, 11), C["blue"])
            poly(d, [(3, 8), (0, 5), (0, 11)], C["blue"])
        elif key == "mackerel":
            ell(d, (3, 5, 13, 11), C["green"])
            for x in (6, 8, 10):
                px(a, x, 7, C["ink"])
        elif key == "jelly":
            ell(d, (4, 3, 12, 10), hex_rgba("#80f0ff", 200))
            for x in (6, 8, 10):
                d.line([(x, 10), (x, 14)], fill=hex_rgba("#80f0ff", 160), width=1)
        else:
            ell(d, (3, 4, 13, 13), C["gold"])
    elif kind == "clipboard":
        rect(d, (3, 2, 13, 15), C["wood3"])
        rect(d, (5, 1, 11, 3), C["gray"])
        rect(d, (5, 5, 11, 7), C["dark"])
        rect(d, (5, 9, 11, 11), C["dark"])
        rect(d, (5, 12, 9, 13), C["dark"])
    elif kind == "hook_icon":
        d.arc((4, 3, 12, 14), 20, 200, fill=hex_rgba(C["gray"]), width=2)
        px(a, 10, 12, C["gray2"])
    elif kind == "icon_rod":
        d.line([(3, 13), (13, 2)], fill=hex_rgba(C["wood"]), width=2)
        ell(d, (3, 11, 7, 15), C["gray"])
    elif kind == "icon_reel":
        ell(d, (3, 3, 13, 13), C["gray"])
        ell(d, (6, 6, 10, 10), C["dark"])
        rect(d, (12, 7, 15, 9), C["gray2"])
    elif kind == "icon_hook":
        d.arc((4, 2, 12, 14), 30, 210, fill=hex_rgba(C["gray"]), width=2)
    elif kind == "icon_line":
        for i, y in enumerate((4, 7, 10, 13)):
            d.line([(3, y), (13, y + (1 if i % 2 == 0 else -1))], fill=hex_rgba(C["foam2"]), width=1)
    elif kind == "icon_net":
        d.rectangle((3, 3, 13, 13), outline=hex_rgba(C["foam"]), width=1)
        d.line((3, 3, 13, 13), fill=hex_rgba(C["foam"]))
        d.line((13, 3, 3, 13), fill=hex_rgba(C["foam"]))
        d.line((8, 3, 8, 13), fill=hex_rgba(C["foam2"]))
    elif kind == "icon_lantern":
        rect(d, (6, 2, 10, 4), C["dark"])
        ell(d, (4, 4, 12, 12), C["gold"])
        ell(d, (6, 6, 10, 10), C["gold2"])
        rect(d, (6, 12, 10, 15), C["wood"])
    else:
        ell(d, (3, 3, 13, 13), C["teal"])
    return a


# ---------------------------------------------------------------------------
# Environment props
# ---------------------------------------------------------------------------
def env_prop(kind):
    if kind == "sky_sunset":
        a = im(64, 96)
        vgrad(a, [
            (0.0, C["sky_top"]),
            (0.25, C["purple"]),
            (0.45, C["sky_mid"]),
            (0.7, C["sky_low"]),
            (0.88, C["sky_horizon"]),
            (1.0, "#ffd090"),
        ])
        # soft haze bands
        d = ImageDraw.Draw(a)
        for y, col, alpha in ((50, C["pink"], 40), (62, "#ff9050", 35), (78, "#ffc080", 30)):
            d.rectangle((0, y, 63, y + 3), fill=hex_rgba(col, alpha))
        return a

    if kind == "foam_line":
        a = im(64, 8)
        d = ImageDraw.Draw(a)
        for x in range(0, 64, 4):
            h = 2 + (x // 4) % 3
            ell(d, (x, 4 - h, x + 5, 7), C["foam"])
            px(a, x + 2, 3, C["foam2"])
        return a

    if kind == "sun_glint":
        a = im(32, 16)
        d = ImageDraw.Draw(a)
        # horizontal sparkle streak
        for x in range(32):
            dist = abs(x - 16) / 16
            alpha = int(220 * (1 - dist) ** 2)
            if alpha > 20:
                rect(d, (x, 6, x, 9), hex_rgba(C["gold2"], alpha))
        ell(d, (14, 5, 18, 11), hex_rgba(C["cream"], 200))
        return a

    if kind == "underwater_ray":
        a = im(24, 64)
        d = ImageDraw.Draw(a)
        for y in range(64):
            t = y / 63
            w = int(2 + t * 10)
            cx = 12
            alpha = int(90 * (1 - t * 0.7))
            rect(d, (cx - w, y, cx + w, y), hex_rgba("#c8e8ff", alpha))
        return a

    if kind == "shop_exterior":
        # Detailed bait & tackle shack — open front, shelves, lifering, fish on roof
        # NO baked text
        a = im(80, 64)
        d = ImageDraw.Draw(a)
        # ground shadow
        ell(d, (8, 56, 72, 63), hex_rgba(C["ink"], 50))

        # roof (shingles)
        poly(d, [(4, 22), (40, 4), (76, 22)], C["wood2"])
        poly(d, [(8, 22), (40, 8), (72, 22)], C["wood4"])
        # shingle rows
        for y, x0, x1 in ((12, 28, 52), (16, 18, 62), (20, 10, 70)):
            d.line([(x0, y), (x1, y)], fill=hex_rgba(C["wood"]), width=1)
        # chimney
        rect(d, (58, 8, 66, 20), C["gray2"])
        rect(d, (57, 6, 67, 9), C["dark"])

        # fish silhouette on roof ridge (decorative, no text)
        ell(d, (34, 2, 48, 9), C["teal2"])
        poly(d, [(34, 5), (30, 2), (30, 8)], C["teal2"])
        px(a, 45, 4, C["ink"])

        # walls
        rect(d, (8, 22, 72, 56), C["wood"])
        rect(d, (8, 22, 12, 56), C["wood2"])  # left post shade
        rect(d, (68, 22, 72, 56), C["wood2"])
        # plank lines
        for y in range(26, 56, 4):
            d.line([(12, y), (68, y)], fill=hex_rgba(C["wood2"], 100), width=1)

        # open storefront (dark interior)
        rect(d, (16, 28, 54, 54), "#1a1410")
        # counter
        rect(d, (16, 46, 54, 54), C["wood4"])
        rect(d, (16, 46, 54, 48), C["wood3"])

        # shelves with tackle (colored blobs — bait jars, reels)
        rect(d, (18, 30, 50, 32), C["wood2"])
        rect(d, (18, 36, 50, 38), C["wood2"])
        for x, col in ((20, C["red"]), (26, C["teal"]), (32, C["gold"]), (38, C["coral"]), (44, C["green"])):
            ell(d, (x, 31, x + 4, 35), col)
        for x, col in ((22, C["blue"]), (30, C["pink"]), (38, C["gray"]), (46, C["gold2"])):
            rect(d, (x, 38, x + 3, 42), col)

        # hanging nets inside
        for x in (19, 25):
            d.line([(x, 28), (x + 2, 44)], fill=hex_rgba(C["foam2"], 120), width=1)

        # window on right
        rect(d, (56, 30, 68, 42), "#3a6878")
        rect(d, (58, 32, 66, 40), "#70b0c8")
        d.line([(62, 30), (62, 42)], fill=hex_rgba(C["wood2"]), width=1)
        d.line([(56, 36), (68, 36)], fill=hex_rgba(C["wood2"]), width=1)

        # lifering on wall
        ell(d, (58, 44, 70, 56), C["red"])
        ell(d, (61, 47, 67, 53), (0, 0, 0, 0))
        rect(d, (63, 44, 65, 56), C["white"])
        rect(d, (58, 49, 70, 51), C["white"])

        # porch posts
        rect(d, (14, 22, 17, 56), C["wood3"])
        rect(d, (53, 22, 56, 56), C["wood3"])
        # awning stripe (no text)
        rect(d, (14, 24, 56, 28), C["red2"])
        for x in range(14, 56, 6):
            rect(d, (x, 24, x + 3, 28), C["cream"])

        # crates outside
        rect(d, (4, 48, 14, 56), C["wood3"])
        d.rectangle((4, 48, 14, 56), outline=hex_rgba(C["wood2"]), width=1)
        # barrel
        ell(d, (70, 46, 78, 58), C["wood"])
        rect(d, (70, 50, 78, 53), C["dark"])

        return a

    if kind == "shop_interior":
        a = im(80, 56)
        d = ImageDraw.Draw(a)
        # back wall
        rect(d, (0, 0, 80, 56), "#2a1c14")
        vgrad(a, [(0.0, "#3a2818"), (1.0, "#1a1008")])
        d = ImageDraw.Draw(a)
        # shelf units
        rect(d, (6, 8, 36, 40), C["wood2"])
        for y in (12, 20, 28):
            rect(d, (8, y, 34, y + 2), C["wood"])
            for x, col in ((10, C["red"]), (16, C["teal"]), (22, C["gold"]), (28, C["coral"])):
                ell(d, (x, y + 2, x + 4, y + 6), col)
        # counter
        rect(d, (40, 28, 74, 48), C["wood4"])
        rect(d, (40, 28, 74, 32), C["wood3"])
        # register blob (no text/prices)
        rect(d, (56, 20, 70, 28), C["dark"])
        ell(d, (60, 22, 66, 26), C["gold"])
        # hanging lamp
        rect(d, (38, 0, 40, 10), C["dark"])
        ell(d, (32, 8, 46, 18), C["gold"])
        # floorboards
        for x in range(0, 80, 8):
            rect(d, (x, 48, x + 7, 55), C["wood"] if (x // 8) % 2 == 0 else C["wood2"])
        return a

    if kind == "dock_plank":
        a = im(96, 14)
        d = ImageDraw.Draw(a)
        for i in range(0, 96, 12):
            col = C["plank"] if (i // 12) % 2 == 0 else C["wood"]
            rect(d, (i, 1, i + 11, 12), col)
            rect(d, (i, 1, i + 11, 3), C["wood3"])  # highlight
            rect(d, (i, 10, i + 11, 12), C["wood2"])  # edge
            # nail
            px(a, i + 2, 4, C["dark"])
            px(a, i + 9, 4, C["dark"])
            # gap
            rect(d, (i + 11, 1, i + 11, 12), C["wood4"])
        return a

    if kind == "dock_pillar":
        a = im(12, 48)
        d = ImageDraw.Draw(a)
        rect(d, (3, 0, 9, 48), C["wood2"])
        rect(d, (3, 0, 5, 48), C["wood"])  # highlight
        rect(d, (7, 0, 9, 48), C["wood4"])
        # water stain bands
        for y in (18, 28, 38):
            rect(d, (2, y, 10, y + 2), hex_rgba("#3a6048", 120))
        # barnacles near bottom
        for y, x in ((40, 4), (43, 7), (46, 5)):
            px(a, x, y, C["foam2"])
        # top cap
        rect(d, (1, 0, 11, 3), C["wood3"])
        return a

    if kind == "lamp":
        a = im(20, 32)
        d = ImageDraw.Draw(a)
        rect(d, (8, 12, 12, 30), C["dark"])
        rect(d, (6, 10, 14, 13), C["gray2"])
        ell(d, (4, 2, 16, 14), C["gold"])
        ell(d, (7, 5, 13, 11), C["gold2"])
        # glow
        ell(d, (2, 0, 18, 16), hex_rgba(C["gold"], 40))
        return a

    if kind == "crate":
        a = im(24, 22)
        d = ImageDraw.Draw(a)
        rect(d, (2, 6, 22, 20), C["wood3"])
        d.rectangle((2, 6, 22, 20), outline=hex_rgba(C["wood2"]), width=1)
        d.line([(2, 12), (22, 12)], fill=hex_rgba(C["wood2"]))
        d.line([(12, 6), (12, 20)], fill=hex_rgba(C["wood2"]))
        poly(d, [(2, 6), (12, 2), (22, 6)], C["wood"])
        return a

    if kind == "barrel":
        a = im(20, 26)
        d = ImageDraw.Draw(a)
        ell(d, (2, 2, 18, 24), C["wood"])
        rect(d, (2, 8, 18, 10), C["dark"])
        rect(d, (2, 16, 18, 18), C["dark"])
        ell(d, (4, 4, 10, 12), hex_rgba(C["wood3"], 100))
        return a

    if kind == "net":
        a = im(28, 28)
        d = ImageDraw.Draw(a)
        for y in range(4, 26, 4):
            d.line((4, y, 24, y), fill=hex_rgba(C["foam2"], 180))
        for x in range(4, 26, 4):
            d.line((x, 4, x, 24), fill=hex_rgba(C["foam"], 160))
        return a

    if kind == "rope":
        a = im(24, 24)
        d = ImageDraw.Draw(a)
        ell(d, (3, 3, 21, 21), C["wood3"])
        ell(d, (8, 8, 16, 16), (0, 0, 0, 0))
        return a

    if kind == "lifering":
        a = im(28, 28)
        d = ImageDraw.Draw(a)
        ell(d, (2, 2, 26, 26), C["red"])
        ell(d, (8, 8, 20, 20), (0, 0, 0, 0))
        rect(d, (12, 2, 16, 26), C["white"])
        rect(d, (2, 12, 26, 16), C["white"])
        return a

    if kind == "buoy":
        a = im(16, 28)
        d = ImageDraw.Draw(a)
        ell(d, (2, 2, 14, 16), C["red"])
        rect(d, (2, 8, 14, 11), C["white"])
        rect(d, (6, 16, 10, 26), C["dark"])
        ell(d, (5, 24, 11, 28), C["dark"])
        return a

    if kind == "crab_trap":
        a = im(28, 22)
        d = ImageDraw.Draw(a)
        rect(d, (4, 6, 24, 20), C["dark"])
        d.rectangle((4, 6, 24, 20), outline=hex_rgba(C["gray"]), width=1)
        for x in range(6, 24, 4):
            d.line([(x, 6), (x, 20)], fill=hex_rgba(C["gray2"]))
        rect(d, (10, 2, 18, 6), C["wood"])
        return a

    if kind == "water_surface":
        a = im(64, 32)
        # teal → deep with foam crest
        top = hex_rgba(C["water2"])[:3]
        bot = hex_rgba(C["deep"])[:3]
        d = ImageDraw.Draw(a)
        for y in range(32):
            t = y / 31
            col = lerp_col(top + (255,), bot + (255,), t)
            # slight horizontal wave darkening
            d.line([(0, y), (63, y)], fill=col)
        # foam crest
        for x in range(0, 64, 3):
            h = 1 + (x // 3) % 3
            ell(d, (x, 0, x + 4, h + 2), C["foam"])
            px(a, x + 1, 1, C["cream"])
        # secondary ripple
        for x in range(1, 64, 5):
            px(a, x, 4, hex_rgba(C["foam2"], 140))
        return a

    if kind == "water_deep":
        a = im(64, 48)
        top = hex_rgba(C["deep"])[:3]
        bot = hex_rgba(C["abyss"])[:3]
        d = ImageDraw.Draw(a)
        for y in range(48):
            t = y / 47
            col = lerp_col(top + (255,), bot + (255,), t)
            d.line([(0, y), (63, y)], fill=col)
        # faint caustic streaks
        for i in range(6):
            x = 4 + i * 10
            for y in range(4, 40, 3):
                px(a, x + (y % 5) - 2, y, hex_rgba("#2a6080", 50))
        return a

    if kind.startswith("seabed"):
        a = im(64, 28)
        d = ImageDraw.Draw(a)
        if "sand" in kind:
            base, hi, lo = C["sand"], C["sand2"], C["wood2"]
            rect(d, (0, 10, 64, 28), base)
            for i in range(12):
                x = i * 5 + (i % 3)
                poly(d, [(x, 14), (x + 4, 8 + i % 3), (x + 8, 14)], hi)
            for i in range(8):
                px(a, 3 + i * 7, 18 + i % 4, lo)
        elif "rock" in kind:
            rect(d, (0, 12, 64, 28), C["gray2"])
            for pts in (
                [(0, 20), (8, 10), (18, 14), (16, 28), (0, 28)],
                [(20, 22), (28, 8), (40, 12), (38, 28), (20, 28)],
                [(42, 18), (50, 6), (63, 14), (63, 28), (42, 28)],
            ):
                poly(d, pts, C["gray"])
                # highlight
                if len(pts) >= 3:
                    px(a, pts[1][0], pts[1][1] + 2, C["foam2"])
        else:  # deep
            rect(d, (0, 8, 64, 28), C["abyss"])
            for i in range(8):
                x = i * 8
                poly(d, [(x, 20), (x + 5, 10), (x + 10, 20)], "#152040")
            for i in range(5):
                ell(d, (10 + i * 12, 16, 16 + i * 12, 22), hex_rgba("#0e2038", 200))
        return a

    if kind == "rock":
        a = im(32, 28)
        d = ImageDraw.Draw(a)
        poly(d, [(2, 24), (6, 10), (16, 4), (28, 12), (26, 26), (4, 26)], C["gray"])
        poly(d, [(8, 14), (16, 6), (22, 12), (18, 16)], C["gray2"])
        px(a, 12, 10, C["foam2"])
        return a

    if kind == "kelp":
        a = im(28, 40)
        d = ImageDraw.Draw(a)
        for x, phase in ((8, 0), (14, 1), (20, 2)):
            pts = []
            for y in range(36, 2, -2):
                ox = int(math.sin((36 - y) * 0.35 + phase) * 2)
                pts.append((x + ox, y))
            if len(pts) >= 2:
                d.line(pts, fill=hex_rgba(C["kelp"]), width=2)
            # leaves
            for y in range(8, 34, 6):
                ox = int(math.sin((36 - y) * 0.35 + phase) * 2)
                poly(d, [(x + ox, y), (x + ox + 4, y - 2), (x + ox, y + 2)], C["kelp2"])
        return a

    if kind == "coral":
        a = im(28, 28)
        d = ImageDraw.Draw(a)
        for x, hh, col in ((6, 14, C["coral"]), (14, 20, C["pink"]), (22, 12, "#e08060")):
            rect(d, (x, 26 - hh, x + 3, 26), col)
            ell(d, (x - 1, 26 - hh - 2, x + 4, 26 - hh + 3), col)
        return a

    if kind == "wreckage":
        a = im(36, 24)
        d = ImageDraw.Draw(a)
        rect(d, (2, 12, 30, 20), C["dark"])
        poly(d, [(30, 12), (34, 16), (30, 20)], C["gray2"])
        rect(d, (18, 4, 22, 12), C["wood2"])
        d.line([(6, 10), (14, 6)], fill=hex_rgba(C["wood"]), width=1)
        return a

    if kind == "bubble":
        a = im(16, 16)
        d = ImageDraw.Draw(a)
        d.ellipse((3, 2, 13, 12), outline=hex_rgba("#b4e6ff", 200), width=2)
        px(a, 6, 4, hex_rgba(C["white"], 220))
        return a

    if kind == "particle":
        a = im(12, 12)
        d = ImageDraw.Draw(a)
        ell(d, (4, 4, 8, 8), hex_rgba("#c8e6ff", 160))
        return a

    if kind == "island_far":
        a = im(56, 24)
        d = ImageDraw.Draw(a)
        poly(d, [(0, 22), (10, 10), (22, 6), (34, 8), (48, 14), (56, 22)], C["purple2"])
        poly(d, [(8, 22), (18, 12), (28, 10), (40, 16), (48, 22)], C["purple"])
        # tree nubs
        for x in (16, 26, 34):
            rect(d, (x, 8, x + 2, 14), C["kelp2"])
        return a

    if kind == "lighthouse":
        a = im(20, 40)
        d = ImageDraw.Draw(a)
        rect(d, (6, 10, 14, 38), C["white"])
        rect(d, (6, 18, 14, 24), C["red"])
        rect(d, (6, 30, 14, 36), C["red"])
        rect(d, (4, 6, 16, 12), C["dark"])
        ell(d, (7, 2, 13, 8), C["gold"])
        poly(d, [(4, 10), (10, 4), (16, 10)], C["red2"])
        return a

    if kind == "oil_rig":
        a = im(36, 28)
        d = ImageDraw.Draw(a)
        rect(d, (4, 12, 32, 20), C["gray"])
        rect(d, (8, 4, 12, 12), C["dark"])
        rect(d, (24, 4, 28, 12), C["dark"])
        rect(d, (14, 6, 22, 12), C["gray2"])
        # legs
        for x in (8, 16, 24):
            d.line([(x, 20), (x - 2, 27)], fill=hex_rgba(C["dark"]), width=2)
        return a

    if kind == "cargo_ship":
        a = im(56, 22)
        d = ImageDraw.Draw(a)
        poly(d, [(2, 14), (52, 14), (48, 20), (6, 20)], C["dark"])
        rect(d, (30, 4, 46, 14), C["red"])
        rect(d, (32, 6, 40, 10), C["teal"])
        for x in (8, 14, 20):
            rect(d, (x, 8, x + 4, 14), C["gray"])
        return a

    if kind == "npc_boat":
        a = im(40, 20)
        d = ImageDraw.Draw(a)
        poly(d, [(2, 12), (38, 12), (34, 18), (6, 18)], C["wood"])
        rect(d, (4, 11, 36, 13), C["wood3"])
        rect(d, (16, 4, 28, 12), C["white"])
        rect(d, (18, 6, 24, 10), C["teal"])
        return a

    if kind == "sun":
        a = im(32, 32)
        d = ImageDraw.Draw(a)
        # soft glow rings
        ell(d, (0, 0, 32, 32), hex_rgba("#ff9040", 50))
        ell(d, (4, 4, 28, 28), hex_rgba(C["gold"], 120))
        ell(d, (8, 8, 24, 24), C["gold"])
        ell(d, (12, 12, 20, 20), C["gold2"])
        ell(d, (14, 14, 18, 18), C["cream"])
        return a

    if kind == "moon":
        a = im(28, 28)
        d = ImageDraw.Draw(a)
        ell(d, (2, 2, 26, 26), "#d0d8e8")
        ell(d, (10, 2, 28, 20), (0, 0, 0, 0))
        # craters
        ell(d, (8, 10, 12, 14), hex_rgba("#a8b0c0", 180))
        ell(d, (14, 16, 17, 19), hex_rgba("#a8b0c0", 140))
        return a

    if kind == "star":
        a = im(12, 12)
        d = ImageDraw.Draw(a)
        poly(d, [(6, 0), (7, 5), (12, 6), (7, 7), (6, 12), (5, 7), (0, 6), (5, 5)], C["cream"])
        return a

    if kind.startswith("cloud"):
        a = im(48, 24)
        d = ImageDraw.Draw(a)
        # warm sunset-tinted clouds
        tint = {
            "cloud_a": C["cream"],
            "cloud_b": "#ffd0b0",
            "cloud_c": "#e8a0c0",
        }.get(kind, C["cream"])
        shade = {
            "cloud_a": "#e8c8a0",
            "cloud_b": "#e09070",
            "cloud_c": "#c070a0",
        }.get(kind, "#e0b090")
        ell(d, (4, 8, 28, 22), tint)
        ell(d, (16, 4, 44, 20), tint)
        ell(d, (10, 10, 36, 22), shade)
        if kind == "cloud_b":
            ell(d, (0, 10, 18, 20), tint)
        if kind == "cloud_c":
            ell(d, (30, 8, 48, 22), "#d080b0")
        return a

    if kind == "rain_drop":
        a = im(8, 14)
        d = ImageDraw.Draw(a)
        poly(d, [(4, 0), (6, 8), (4, 12), (2, 8)], C["teal"])
        return a

    if kind == "lightning":
        a = im(20, 32)
        d = ImageDraw.Draw(a)
        poly(d, [(12, 0), (4, 14), (10, 14), (6, 30), (18, 10), (12, 10)], C["gold2"])
        return a

    if kind.startswith("bird"):
        a = im(24, 16)
        d = ImageDraw.Draw(a)
        flap = 0 if kind.endswith("0") else 3
        d.arc((2, 4 + flap, 12, 14 - flap), 200, 340, fill=hex_rgba(C["ink"]), width=2)
        d.arc((10, 4 + flap, 22, 14 - flap), 200, 340, fill=hex_rgba(C["ink"]), width=2)
        return a

    if kind == "dolphin":
        a = im(40, 18)
        d = ImageDraw.Draw(a)
        ell(d, (4, 4, 32, 15), C["blue"])
        poly(d, [(20, 4), (24, 0), (26, 5)], C["blue"])
        poly(d, [(30, 8), (38, 4), (38, 12), (30, 10)], C["blue"])
        px(a, 10, 8, C["ink"])
        ell(d, (8, 10, 20, 14), hex_rgba(C["cream"], 80))
        return a

    if kind == "turtle":
        a = im(28, 20)
        d = ImageDraw.Draw(a)
        ell(d, (6, 4, 22, 16), C["kelp"])
        ell(d, (8, 6, 20, 14), C["kelp2"])
        ell(d, (20, 6, 28, 14), C["green"])
        px(a, 25, 9, C["ink"])
        for x, y in ((4, 8), (4, 12), (14, 16), (18, 16)):
            ell(d, (x, y, x + 4, y + 3), C["green"])
        return a

    # fallback
    a = im(32, 32)
    d = ImageDraw.Draw(a)
    rect(d, (8, 8, 24, 24), C["teal"])
    return a


def rarity_frame(tier):
    a = im(20, 20)
    d = ImageDraw.Draw(a)
    col = {
        "common": C["gray"],
        "uncommon": C["green"],
        "rare": C["blue"],
        "epic": C["purple"],
        "legendary": C["gold"],
    }[tier]
    d.rectangle((1, 1, 18, 18), outline=hex_rgba(col), width=2)
    if tier == "legendary":
        d.rectangle((3, 3, 16, 16), outline=hex_rgba(C["gold2"]), width=1)
    return a


def logo():
    """OFFSHORE brand mark — may include the word OFFSHORE."""
    a = im(96, 40)
    d = ImageDraw.Draw(a)
    # sunset disc
    ell(d, (8, 4, 40, 36), C["gold"])
    ell(d, (14, 10, 34, 30), C["sky_low"])
    ell(d, (18, 14, 30, 26), C["pink"])
    # wave
    d.arc((4, 18, 44, 42), 200, 340, fill=hex_rgba(C["foam"]), width=2)
    d.arc((10, 22, 50, 44), 210, 330, fill=hex_rgba(C["teal"]), width=2)
    # brand text — pixel block letters (OFFSHORE allowed on logo only)
    text = "OFFSHORE"
    glyphs = {
        "O": ["01110", "10001", "10001", "10001", "01110"],
        "F": ["11111", "10000", "11110", "10000", "10000"],
        "S": ["01111", "10000", "01110", "00001", "11110"],
        "H": ["10001", "10001", "11111", "10001", "10001"],
        "R": ["11110", "10001", "11110", "10010", "10001"],
        "E": ["11111", "10000", "11110", "10000", "11111"],
    }
    x0 = 48
    y0 = 14
    for ch in text:
        g = glyphs.get(ch)
        if not g:
            x0 += 6
            continue
        for row, bits in enumerate(g):
            for col, bit in enumerate(bits):
                if bit == "1":
                    px(a, x0 + col, y0 + row, C["cream"])
                    px(a, x0 + col, y0 + row + 1, hex_rgba(C["gold"], 80))
        x0 += 6
    return a


def wav(path, seconds, fn):
    rate = 22050
    n = int(rate * seconds)
    with wave.open(str(path), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            t = i / rate
            v = max(-1.0, min(1.0, fn(t, i)))
            frames += struct.pack("<h", int(v * 30000))
        w.writeframes(frames)


# ---------------------------------------------------------------------------
# Generate everything
# ---------------------------------------------------------------------------
count = 0

for f in range(4):
    save(player(f, "idle"), ART / f"player_idle_{f}.png")
    count += 1
    save(player(f, "walk"), ART / f"player_walk_{f}.png")
    count += 1
for f in range(3):
    save(player(f, "cast"), ART / f"player_cast_{f}.png")
    count += 1
    save(player(f, "reel"), ART / f"player_reel_{f}.png")
    count += 1
save(player(0, "hook"), ART / "player_hook.png")
count += 1
save(player(0, "hold"), ART / "player_hold.png")
count += 1
save(player(0, "celebrate"), ART / "player_celebrate.png")
count += 1

for k in ("dinghy", "fisher17", "seawolf", "triton"):
    save(boat(k), BOATS / f"boat_{k}.png", 3)
    count += 1
for i in range(3):
    a = im(28, 12)
    d = ImageDraw.Draw(a)
    for x in range(0, 28, 4):
        yy = 2 + (i + x) % 3
        ell(d, (x, yy, x + 5, yy + 5), hex_rgba(C["foam"], 150 - i * 30))
    save(a, BOATS / f"wake_{i}.png")
    count += 1
a = im(16, 8)
d = ImageDraw.Draw(a)
ell(d, (1, 1, 7, 6), C["foam"])
ell(d, (8, 2, 15, 7), C["foam2"])
save(a, BOATS / "foam.png")
count += 1

fish_colors = {
    "sardine": "#a0c0d0",
    "mackerel": "#5a8a7a",
    "bluegill": "#4a8ac0",
    "flounder": "#c2a46a",
    "seabass": "#3a4a5a",
    "redsnapper": "#c84a4a",
    "grouper": "#6a8a5a",
    "cobia": "#708090",
    "barracuda": "#8aa0a8",
    "mahi": "#40c070",
    "tuna": "#2a4a6a",
    "kingmackerel": "#4a7060",
    "swordfish": "#4a6080",
    "marlin": "#2a6a9a",
    "gianttrevally": "#c09040",
    "oarfish": "#d080a0",
    "anglerfish": "#4a3060",
    "giantsquid": "#6a4070",
}
for name, col in fish_colors.items():
    long = name in ("oarfish", "swordfish", "marlin", "giantsquid", "barracuda")
    save(fish_sprite(name, col, long=long), FISH / f"{name}.png")
    count += 1
a = im(32, 16)
d = ImageDraw.Draw(a)
ell(d, (8, 3, 28, 13), "#152030")
poly(d, [(10, 8), (0, 2), (0, 14)], "#152030")
save(a, FISH / "fish_silhouette.png")
count += 1

env_kinds = [
    "sky_sunset", "foam_line", "sun_glint", "underwater_ray",
    "shop_exterior", "shop_interior", "dock_plank", "dock_pillar", "lamp", "crate",
    "barrel", "net", "rope", "lifering", "buoy", "crab_trap",
    "water_surface", "water_deep", "seabed_sand", "seabed_rock", "seabed_deep",
    "rock", "kelp", "coral", "wreckage", "bubble", "particle",
    "island_far", "lighthouse", "oil_rig", "cargo_ship", "npc_boat",
    "sun", "moon", "star", "cloud_a", "cloud_b", "cloud_c",
    "rain_drop", "lightning", "bird_0", "bird_1", "dolphin", "turtle",
]
scale_3 = {
    "shop_exterior", "shop_interior", "water_surface", "water_deep",
    "seabed_sand", "seabed_rock", "seabed_deep", "dock_plank", "sky_sunset",
    "island_far", "cargo_ship",
}
for k in env_kinds:
    save(env_prop(k), ENV / f"{k}.png", 3 if k in scale_3 else 4)
    count += 1

ui_icons = [
    "coin", "hook_icon", "clipboard",
    "weather_clear", "weather_cloudy", "weather_rain", "weather_storm",
    "weather_fog", "weather_wind",
    "icon_rod", "icon_reel", "icon_hook", "icon_line",
    "icon_bait_worm", "icon_bait_minnow", "icon_bait_shrimp", "icon_bait_squid",
    "icon_bait_crab", "icon_bait_sardine", "icon_bait_mackerel", "icon_bait_jelly",
    "icon_net", "icon_lantern",
]
for k in ui_icons:
    save(icon(k), UI / f"{k}.png", 3)
    count += 1
for tier in ("common", "uncommon", "rare", "epic", "legendary"):
    save(rarity_frame(tier), UI / f"rarity_{tier}.png", 3)
    count += 1

a = Image.new("RGBA", (32, 32), (12, 22, 34, 210))
d = ImageDraw.Draw(a)
d.rectangle((0, 0, 31, 31), outline=(212, 162, 90, 255), width=2)
save(a, UI / "panel.png", 2)
count += 1
save(logo(), UI / "logo_offshore.png", 3)
count += 1

# sounds
def tone(freq, t, dur=0.05):
    return math.sin(2 * math.pi * freq * t) * max(0, 1 - t / dur)

sounds = {
    "ui_click": (0.06, lambda t, i: tone(880, t, 0.06) * 0.5),
    "ui_hover": (0.04, lambda t, i: tone(660, t, 0.04) * 0.3),
    "purchase": (0.18, lambda t, i: tone(523, t, 0.1) * 0.4 + tone(784, t, 0.18) * 0.3),
    "sell": (0.2, lambda t, i: tone(440, t, 0.2) * 0.35 + tone(660, max(0, t - 0.05), 0.15) * 0.3),
    "cast": (0.15, lambda t, i: (random.random() * 2 - 1) * 0.2 * max(0, 1 - t / 0.15) + tone(200, t, 0.15) * 0.2),
    "splash": (0.25, lambda t, i: (random.random() * 2 - 1) * 0.35 * max(0, 1 - t / 0.25)),
    "bite": (0.12, lambda t, i: tone(180, t, 0.12) * 0.5),
    "hook": (0.1, lambda t, i: tone(720, t, 0.1) * 0.45),
    "reel": (0.08, lambda t, i: tone(140 + (i % 20), t, 0.08) * 0.25),
    "tension": (0.15, lambda t, i: tone(90, t, 0.15) * 0.4 + (random.random() * 2 - 1) * 0.1),
    "line_break": (0.2, lambda t, i: (random.random() * 2 - 1) * 0.5 * max(0, 1 - t / 0.2)),
    "escape": (0.15, lambda t, i: tone(300 - t * 400, t, 0.15) * 0.35),
    "catch": (0.3, lambda t, i: tone(523, t, 0.15) * 0.35 + tone(784, max(0, t - 0.08), 0.2) * 0.35),
    "engine_loop": (0.4, lambda t, i: math.sin(2 * math.pi * 60 * t) * 0.15 + math.sin(2 * math.pi * 90 * t) * 0.1),
    "waves_loop": (0.5, lambda t, i: (random.random() * 2 - 1) * 0.08 * (0.5 + 0.5 * math.sin(t * 3))),
    "rain_loop": (0.4, lambda t, i: (random.random() * 2 - 1) * 0.12),
    "thunder": (0.5, lambda t, i: (random.random() * 2 - 1) * 0.5 * max(0, 1 - t / 0.5)),
    "seagull": (0.25, lambda t, i: tone(900 + math.sin(t * 20) * 80, t, 0.25) * 0.2),
    "wood_creak": (0.2, lambda t, i: tone(120 + random.random() * 40, t, 0.2) * 0.25),
    "night_ambience": (0.5, lambda t, i: math.sin(2 * math.pi * 80 * t) * 0.05 + (random.random() * 2 - 1) * 0.03),
}
random.seed(42)
for name, (dur, fn) in sounds.items():
    wav(SND / f"{name}.wav", dur, fn)
    count += 1

print(f"Generated {count} asset files under {ROOT}")
sky = ENV / "sky_sunset.png"
print(f"sky_sunset.png exists: {sky.is_file()} ({sky})")
