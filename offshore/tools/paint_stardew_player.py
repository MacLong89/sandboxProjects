"""
Paint a Stardew Valley-esque side-view farmer from scratch (true RGBA alpha).

Original pixel kit — no reference to existing player sprites.
Logical grid ~16×32 character (classic SDV scale), ×5 → runtime textures.
Writes paths expected by WorldPresenter.ResolvePlayerAnim.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "textures" / "art"
HEROES = ROOT / "Assets" / "Art" / "heroes"

# Character lives in a 22×32 logical box; walk canvas 28×40 for foot room.
CW, CH = 22, 32
WALK_W, WALK_H = 28, 40
ROD_W, ROD_H = 52, 48
SCALE = 4  # walk canvas 28×40 → 112×160 runtime footprint

# Soft Stardew-like palette (warm light from upper-left)
OUT = (61, 40, 28, 255)
OUT_SOFT = (90, 62, 44, 255)
SKIN = (241, 194, 155, 255)
SKIN_M = (222, 168, 128, 255)
SKIN_S = (196, 138, 100, 255)
HAIR = (102, 62, 38, 255)
HAIR_H = (132, 84, 52, 255)
HAT = (226, 188, 86, 255)
HAT_H = (242, 212, 120, 255)
HAT_S = (186, 142, 54, 255)
HAT_BAND = (138, 88, 48, 255)
SHIRT = (74, 140, 196, 255)
SHIRT_H = (106, 168, 220, 255)
SHIRT_S = (48, 102, 154, 255)
OVERALL = (58, 98, 168, 255)  # overall strap accent
PANTS = (86, 102, 132, 255)
PANTS_H = (110, 126, 156, 255)
PANTS_S = (58, 72, 98, 255)
BOOT = (86, 54, 38, 255)
BOOT_H = (112, 74, 52, 255)
BOOT_S = (58, 36, 26, 255)
ROD = (148, 104, 58, 255)
ROD_HI = (176, 132, 78, 255)
ROD_S = (112, 76, 42, 255)
ROD_TIP = (188, 192, 198, 255)
LINE = (230, 232, 236, 255)
FISH = (232, 128, 72, 255)
FISH_H = (246, 164, 102, 255)
FISH_S = (196, 92, 48, 255)
FISH_BELLY = (248, 210, 168, 255)
EYE = (36, 26, 20, 255)
MOUTH = (168, 96, 82, 255)


def new_canvas(w: int, h: int) -> Image.Image:
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def P(px, x, y, c, w, h):
    if 0 <= x < w and 0 <= y < h and c[3]:
        px[x, y] = c


def rect(px, x0, y0, x1, y1, c, w, h):
    for y in range(y0, y1):
        for x in range(x0, x1):
            P(px, x, y, c, w, h)


def hline(px, x0, x1, y, c, w, h):
    for x in range(x0, x1 + 1):
        P(px, x, y, c, w, h)


def vline(px, x, y0, y1, c, w, h):
    for y in range(y0, y1 + 1):
        P(px, x, y, c, w, h)


def disk(px, cx, cy, r, c, w, h):
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r + r * 0.2:
                P(px, x, y, c, w, h)


def line(px, x0, y0, x1, y1, c, w, h, thick=1):
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err, x, y = dx + dy, x0, y0
    while True:
        for t in range(-(thick // 2), thick // 2 + 1):
            P(px, x + t, y, c, w, h)
            P(px, x, y + t, c, w, h)
        if x == x1 and y == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x += sx
        if e2 <= dx:
            err += dx
            y += sy


def outline(img: Image.Image, color=OUT) -> Image.Image:
    w, h = img.size
    src = img.load()
    out = img.copy()
    dst = out.load()
    op = [[src[x, y][3] > 200 for y in range(h)] for x in range(w)]
    for y in range(h):
        for x in range(w):
            if op[x][y]:
                continue
            n = 0
            for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and op[nx][ny]:
                    n += 1
            if n:
                dst[x, y] = color if n >= 2 else OUT_SOFT
    return out


def draw_hat(px, ox, oy, w, h, bob=0):
    y = oy + bob
    # brim
    hline(px, ox - 2, ox + 13, y + 5, HAT_S, w, h)
    hline(px, ox - 1, ox + 12, y + 4, HAT, w, h)
    hline(px, ox - 1, ox + 12, y + 6, HAT_S, w, h)
    # crown
    rect(px, ox + 1, y + 0, ox + 11, y + 5, HAT, w, h)
    rect(px, ox + 2, y + 0, ox + 10, y + 2, HAT_H, w, h)
    rect(px, ox + 9, y + 2, ox + 11, y + 5, HAT_S, w, h)
    hline(px, ox + 1, ox + 10, y + 5, HAT_BAND, w, h)
    # dent highlight
    P(px, ox + 4, y + 1, HAT_H, w, h)
    P(px, ox + 5, y + 1, HAT_H, w, h)


def draw_head(px, ox, oy, w, h, bob=0):
    y = oy + bob
    draw_hat(px, ox, y, w, h, 0)
    # hair under brim
    rect(px, ox + 1, y + 6, ox + 11, y + 8, HAIR, w, h)
    P(px, ox + 2, y + 6, HAIR_H, w, h)
    P(px, ox + 3, y + 6, HAIR_H, w, h)
    # face oval-ish
    rect(px, ox + 2, y + 7, ox + 11, y + 15, SKIN, w, h)
    rect(px, ox + 1, y + 8, ox + 12, y + 14, SKIN, w, h)
    # shade / jaw
    rect(px, ox + 1, y + 12, ox + 3, y + 15, SKIN_S, w, h)
    rect(px, ox + 9, y + 11, ox + 12, y + 15, SKIN_M, w, h)
    # ear
    rect(px, ox + 1, y + 10, ox + 2, y + 13, SKIN_M, w, h)
    # eye (profile facing right)
    P(px, ox + 9, y + 10, EYE, w, h)
    P(px, ox + 8, y + 10, SKIN_M, w, h)
    # nose tip
    P(px, ox + 11, y + 11, SKIN_M, w, h)
    # smile
    P(px, ox + 9, y + 13, MOUTH, w, h)


def draw_body(px, ox, oy, w, h, bob=0, lean=0):
    """oy = top of torso under neck."""
    x = ox + lean
    y = oy + bob
    # neck
    rect(px, x + 5, y - 1, x + 8, y + 1, SKIN, w, h)
    # shirt
    rect(px, x + 3, y, x + 12, y + 9, SHIRT, w, h)
    rect(px, x + 3, y, x + 12, y + 3, SHIRT_H, w, h)
    rect(px, x + 3, y + 6, x + 12, y + 9, SHIRT_S, w, h)
    # side-view overall strap (one visible) + bib edge
    vline(px, x + 6, y + 1, y + 7, OVERALL, w, h)
    hline(px, x + 6, x + 10, y + 3, OVERALL, w, h)


def draw_arm_near(px, ox, oy, w, h, pose: str, bob=0, lean=0):
    """Near (visible) arm. oy = torso top."""
    x = ox + lean
    y = oy + bob
    # shoulder
    sx, sy = x + 11, y + 1
    if pose == "down":
        rect(px, sx, sy, sx + 3, sy + 7, SKIN, w, h)
        rect(px, sx, sy + 5, sx + 3, sy + 8, SKIN_S, w, h)
        # hand
        rect(px, sx, sy + 7, sx + 3, sy + 9, SKIN_M, w, h)
        return sx + 1, sy + 8
    if pose == "swing_back":
        rect(px, sx - 1, sy - 1, sx + 2, sy + 4, SKIN, w, h)
        rect(px, sx - 3, sy + 2, sx, sy + 5, SKIN_M, w, h)
        return sx - 2, sy + 4
    if pose == "swing_fwd":
        rect(px, sx, sy, sx + 4, sy + 3, SKIN, w, h)
        rect(px, sx + 3, sy + 2, sx + 6, sy + 5, SKIN_M, w, h)
        return sx + 5, sy + 4
    if pose == "up_back":
        # cast charge — arm up/back
        rect(px, sx - 1, sy - 4, sx + 2, sy + 1, SKIN, w, h)
        rect(px, sx - 4, sy - 7, sx - 1, sy - 3, SKIN_M, w, h)
        return sx - 3, sy - 6
    if pose == "up_high":
        # mid cast
        rect(px, sx, sy - 5, sx + 3, sy, SKIN, w, h)
        rect(px, sx + 1, sy - 9, sx + 4, sy - 5, SKIN_M, w, h)
        return sx + 2, sy - 8
    if pose == "fwd_cast":
        rect(px, sx, sy - 1, sx + 5, sy + 2, SKIN, w, h)
        rect(px, sx + 4, sy - 2, sx + 8, sy + 1, SKIN_M, w, h)
        return sx + 7, sy - 1
    if pose == "hold_rod":
        rect(px, sx, sy, sx + 4, sy + 3, SKIN, w, h)
        rect(px, sx + 3, sy + 1, sx + 6, sy + 4, SKIN_M, w, h)
        return sx + 5, sy + 3
    if pose == "reel":
        rect(px, sx, sy + 1, sx + 3, sy + 5, SKIN, w, h)
        rect(px, sx + 2, sy + 4, sx + 5, sy + 7, SKIN_M, w, h)
        return sx + 4, sy + 5
    if pose == "celebrate":
        rect(px, sx, sy - 6, sx + 3, sy, SKIN, w, h)
        rect(px, sx + 1, sy - 9, sx + 4, sy - 5, SKIN_M, w, h)
        return sx + 2, sy - 8
    if pose == "hold_fish":
        rect(px, sx, sy + 1, sx + 4, sy + 4, SKIN, w, h)
        rect(px, sx + 3, sy + 3, sx + 6, sy + 6, SKIN_M, w, h)
        return sx + 5, sy + 5
    # default
    rect(px, sx, sy, sx + 3, sy + 7, SKIN, w, h)
    return sx + 1, sy + 7


def draw_arm_far(px, ox, oy, w, h, pose: str, bob=0, lean=0):
    x = ox + lean
    y = oy + bob
    sx, sy = x + 2, y + 2
    if pose in ("swing_fwd", "fwd_cast", "hold_rod", "reel"):
        rect(px, sx - 1, sy, sx + 2, sy + 5, SKIN_S, w, h)
    elif pose in ("up_back", "up_high", "celebrate"):
        rect(px, sx, sy - 3, sx + 2, sy + 1, SKIN_S, w, h)
    else:
        rect(px, sx, sy, sx + 2, sy + 5, SKIN_S, w, h)


def draw_legs(px, ox, oy, w, h, gait: int, bob=0, lean=0):
    """oy = waist. gait -1 idle, 0..3 walk."""
    x = ox + lean
    y = oy + bob
    # hips / belt
    rect(px, x + 3, y, x + 12, y + 3, PANTS, w, h)
    hline(px, x + 3, x + 11, y, HAT_BAND, w, h)

    # (back_dx, back_dy, front_dx, front_dy, bob_extra)
    table = {
        -1: (0, 0, 0, 0, 0),
        0: (-1, 0, 1, 0, 0),
        1: (-2, 1, 2, -1, 1),
        2: (0, 0, 0, 0, 0),
        3: (2, -1, -2, 1, 1),
    }
    bdx, bdy, fdx, fdy, _ = table.get(gait, (0, 0, 0, 0, 0))

    # back leg (shaded)
    rect(px, x + 3 + bdx, y + 2 + bdy, x + 6 + bdx, y + 11 + bdy, PANTS_S, w, h)
    rect(px, x + 3 + bdx, y + 9 + bdy, x + 7 + bdx, y + 12 + bdy, BOOT_S, w, h)
    P(px, x + 6 + bdx, y + 10 + bdy, BOOT_H, w, h)

    # front leg
    rect(px, x + 7 + fdx, y + 2 + fdy, x + 11 + fdx, y + 11 + fdy, PANTS, w, h)
    rect(px, x + 7 + fdx, y + 2 + fdy, x + 9 + fdx, y + 6 + fdy, PANTS_H, w, h)
    rect(px, x + 7 + fdx, y + 9 + fdy, x + 12 + fdx, y + 12 + fdy, BOOT, w, h)
    hline(px, x + 7 + fdx, x + 11 + fdx, y + 10 + fdy, BOOT_H, w, h)


def compose_farmer(
    gait: int = -1,
    arm: str = "down",
    bob: int = 0,
    lean: int = 0,
) -> Image.Image:
    img = new_canvas(CW, CH)
    px = img.load()
    ox, head_y = 4, 1
    # walk bob
    if gait in (1, 3):
        bob = max(bob, 1)
    draw_arm_far(px, ox, head_y + 14, CW, CH, arm, bob, lean)
    draw_head(px, ox, head_y, CW, CH, bob)
    draw_body(px, ox, head_y + 14, CW, CH, bob, lean)
    hand = draw_arm_near(px, ox, head_y + 14, CW, CH, arm, bob, lean)
    draw_legs(px, ox, head_y + 22, CW, CH, gait, bob, lean)
    img = outline(img)
    return img, hand


def place(dst: Image.Image, src: Image.Image, ox: int, oy: int):
    dst.alpha_composite(src, (ox, oy))


def paint_idle() -> Image.Image:
    canv = new_canvas(WALK_W, WALK_H)
    farmer, _ = compose_farmer(gait=-1, arm="down")
    place(canv, farmer, 3, WALK_H - CH)
    return canv


def paint_walk(frame: int) -> Image.Image:
    arms = ("swing_back", "swing_fwd", "swing_back", "down")
    canv = new_canvas(WALK_W, WALK_H)
    farmer, _ = compose_farmer(gait=frame % 4, arm=arms[frame % 4])
    place(canv, farmer, 3, WALK_H - CH)
    return canv


def paint_rod(kind: str) -> Image.Image:
    img = new_canvas(ROD_W, ROD_H)
    px = img.load()

    arm = "hold_rod"
    lean = 0
    gait = -1
    if kind == "charge":
        arm, lean = "up_back", -1
    elif kind == "swing":
        arm, lean = "up_high", 0
    elif kind == "release":
        arm, lean = "fwd_cast", 1
    elif kind == "wait":
        arm = "hold_rod"
    elif kind == "fight":
        arm, lean = "up_back", -1
    elif kind.startswith("reel"):
        arm = "reel"
    elif kind == "keep":
        arm = "hold_fish"
    elif kind == "celebrate":
        arm = "celebrate"

    farmer, hand = compose_farmer(gait=gait, arm=arm, lean=lean)
    # Leave headroom for overhead casts; farmer left so rod extends right
    fox, foy = 8, ROD_H - CH - 2
    if kind in ("charge", "swing", "fight", "celebrate"):
        foy = max(2, foy - 6)
    place(img, farmer, fox, foy)
    hx = fox + hand[0]
    hy = foy + hand[1]

    if kind == "charge":
        # rod cocked over shoulder, tip up-back
        line(px, hx, hy, hx - 10, hy - 14, ROD_S, ROD_W, ROD_H, 2)
        line(px, hx - 1, hy, hx - 9, hy - 13, ROD, ROD_W, ROD_H, 1)
        line(px, hx - 10, hy - 14, hx - 14, hy - 18, ROD_HI, ROD_W, ROD_H, 1)
        P(px, hx - 14, hy - 18, ROD_TIP, ROD_W, ROD_H)
    elif kind == "swing":
        line(px, hx, hy, hx + 4, hy - 16, ROD, ROD_W, ROD_H, 2)
        line(px, hx + 4, hy - 16, hx + 8, hy - 20, ROD_HI, ROD_W, ROD_H, 1)
        P(px, hx + 8, hy - 20, ROD_TIP, ROD_W, ROD_H)
    elif kind == "release":
        line(px, hx, hy, hx + 18, hy - 6, ROD, ROD_W, ROD_H, 2)
        line(px, hx + 1, hy - 1, hx + 17, hy - 7, ROD_HI, ROD_W, ROD_H, 1)
        P(px, hx + 18, hy - 6, ROD_TIP, ROD_W, ROD_H)
        line(px, hx + 18, hy - 6, hx + 24, hy + 10, LINE, ROD_W, ROD_H, 1)
    elif kind == "wait":
        line(px, hx, hy, hx + 20, hy - 3, ROD, ROD_W, ROD_H, 2)
        line(px, hx + 1, hy - 1, hx + 19, hy - 4, ROD_HI, ROD_W, ROD_H, 1)
        P(px, hx + 20, hy - 3, ROD_TIP, ROD_W, ROD_H)
        line(px, hx + 20, hy - 3, hx + 22, hy + 12, LINE, ROD_W, ROD_H, 1)
        # tiny bobber
        P(px, hx + 22, hy + 12, (220, 72, 64, 255), ROD_W, ROD_H)
        P(px, hx + 22, hy + 13, (240, 220, 210, 255), ROD_W, ROD_H)
    elif kind == "fight":
        # bent rod
        line(px, hx, hy, hx + 8, hy - 12, ROD_S, ROD_W, ROD_H, 2)
        line(px, hx + 8, hy - 12, hx + 18, hy - 8, ROD, ROD_W, ROD_H, 2)
        line(px, hx + 18, hy - 8, hx + 22, hy - 2, ROD_HI, ROD_W, ROD_H, 1)
        P(px, hx + 22, hy - 2, ROD_TIP, ROD_W, ROD_H)
        line(px, hx + 22, hy - 2, hx + 24, hy + 12, LINE, ROD_W, ROD_H, 1)
    elif kind.startswith("reel"):
        n = int(kind.rsplit("_", 1)[-1])
        tip = [(20, -4), (21, -6), (19, -3)][n % 3]
        crank = [(0, 0), (1, 2), (-1, 1)][n % 3]
        line(px, hx, hy, hx + tip[0], hy + tip[1], ROD, ROD_W, ROD_H, 2)
        P(px, hx + tip[0], hy + tip[1], ROD_TIP, ROD_W, ROD_H)
        line(px, hx + tip[0], hy + tip[1], hx + tip[0] + 2, hy + tip[1] + 11, LINE, ROD_W, ROD_H, 1)
        # reel crank knob
        rect(
            px,
            hx - 1 + crank[0],
            hy + 3 + crank[1],
            hx + 2 + crank[0],
            hy + 6 + crank[1],
            ROD_HI,
            ROD_W,
            ROD_H,
        )
        P(px, hx + crank[0], hy + 4 + crank[1], SKIN_M, ROD_W, ROD_H)
    elif kind == "keep":
        # rod on shoulder
        line(px, hx - 4, hy - 2, hx - 12, hy - 12, ROD_S, ROD_W, ROD_H, 2)
        P(px, hx - 12, hy - 12, ROD_TIP, ROD_W, ROD_H)
        # fish held forward
        fx, fy = hx + 2, hy + 1
        rect(px, fx, fy - 1, fx + 9, fy + 3, FISH, ROD_W, ROD_H)
        rect(px, fx + 1, fy - 1, fx + 6, fy + 1, FISH_H, ROD_W, ROD_H)
        rect(px, fx + 1, fy + 2, fx + 7, fy + 3, FISH_S, ROD_W, ROD_H)
        rect(px, fx + 2, fy + 1, fx + 6, fy + 2, FISH_BELLY, ROD_W, ROD_H)
        # tail
        P(px, fx + 9, fy, FISH_S, ROD_W, ROD_H)
        P(px, fx + 10, fy - 1, FISH, ROD_W, ROD_H)
        P(px, fx + 10, fy + 1, FISH, ROD_W, ROD_H)
        P(px, fx, fy, EYE, ROD_W, ROD_H)
    elif kind == "celebrate":
        # second arm up (far side) + sparkles
        rect(px, fox + 5, foy + 8, fox + 8, foy + 14, SKIN_S, ROD_W, ROD_H)
        rect(px, fox + 4, foy + 4, fox + 7, foy + 8, SKIN_M, ROD_W, ROD_H)
        for sx, sy, col in (
            (hx - 5, hy - 4, HAT_H),
            (hx + 2, hy - 8, FISH_H),
            (hx + 6, hy - 2, (255, 255, 255, 255)),
            (fox + 3, foy + 2, FISH),
            (hx - 1, hy - 11, (255, 240, 160, 255)),
        ):
            P(px, sx, sy, col, ROD_W, ROD_H)
            P(px, sx + 1, sy, col, ROD_W, ROD_H)

    return outline(img)


def harden(img: Image.Image) -> Image.Image:
    img = img.convert("RGBA")
    data = []
    for r, g, b, a in img.getdata():
        data.append((0, 0, 0, 0) if a < 40 else (r, g, b, 255))
    out = Image.new("RGBA", img.size)
    out.putdata(data)
    return out


def upscale(img: Image.Image, scale: int = SCALE) -> Image.Image:
    img = harden(img)
    return harden(img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST))


def pad_feet(img: Image.Image, tw: int, th: int) -> Image.Image:
    canv = new_canvas(tw, th)
    x = (tw - img.width) // 2
    y = th - img.height
    canv.alpha_composite(img, (max(0, x), max(0, y)))
    return canv


def tight_crop(img: Image.Image, pad: int = 3) -> Image.Image:
    bb = img.getbbox()
    if not bb:
        return img
    x0, y0, x1, y1 = bb
    return img.crop((max(0, x0 - pad), max(0, y0 - pad), min(img.width, x1 + pad), min(img.height, y1 + pad)))


def save(img: Image.Image, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    img = harden(img)
    img.save(path, format="PNG")
    clear = any(a == 0 for *_, a in img.getdata())
    print(f"  -> {path.relative_to(ROOT)} ({img.width}x{img.height}) clear_bg={clear}")


def main():
    ART.mkdir(parents=True, exist_ok=True)
    HEROES.mkdir(parents=True, exist_ok=True)
    print("Painting Stardew-esque player kit (v2)…")

    # Match prior footprint class (~112×160)
    TW, TH = 112, 160

    idle = pad_feet(upscale(paint_idle()), TW, TH)
    # If upscale overshoots, letterbox by resizing nearest after pad crop
    if idle.width != TW or idle.height != TH:
        idle = harden(idle.resize((TW, TH), Image.Resampling.NEAREST))
    save(idle, ART / "player_idle_0.png")
    save(idle, HEROES / "hero_player_idle.png")

    for i in range(4):
        fr = pad_feet(upscale(paint_walk(i)), TW, TH)
        if fr.width != TW or fr.height != TH:
            fr = harden(fr.resize((TW, TH), Image.Resampling.NEAREST))
        save(fr, ART / f"player_walk_{i}.png")

    rod_kinds = {
        "charge": "player_rod_charge.png",
        "swing": "player_rod_swing.png",
        "release": "player_rod_release.png",
        "wait": "player_rod_wait.png",
        "fight": "player_rod_fight.png",
        "reel_0": "player_rod_reel_0.png",
        "reel_1": "player_rod_reel_1.png",
        "reel_2": "player_rod_reel_2.png",
        "keep": "player_rod_keep.png",
    }
    for kind, name in rod_kinds.items():
        pose = tight_crop(upscale(paint_rod(kind)), pad=4)
        save(pose, ART / name)

    cele = pad_feet(tight_crop(upscale(paint_rod("celebrate")), pad=4), TW, TH)
    if cele.width != TW or cele.height != TH:
        cele = harden(cele.resize((TW, TH), Image.Resampling.NEAREST))
    save(cele, ART / "player_celebrate.png")

    print("Done. Reopen scenes/offshore.scene so textures remount.")


if __name__ == "__main__":
    main()
