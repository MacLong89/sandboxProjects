"""Procedurally generates the stylized building textures in Assets/textures.

Run from the project root:
    python tools/generate_building_textures.py

Every texture is tileable, 512x512, and authored in the same hand-painted
Clash-of-Clans-ish style as the original stone/wood/roof/grass set.
"""

from __future__ import annotations

import os

import numpy as np
from PIL import Image

SIZE = 512
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "textures")


# ----------------------------------------------------------------------------
# Noise helpers (all tileable)
# ----------------------------------------------------------------------------

def _lattice_noise(size: int, cells: int, rng: np.random.Generator) -> np.ndarray:
    """Bilinear value noise that wraps at the edges."""
    grid = rng.random((cells, cells))
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float64)
    fy = ys * cells / size
    fx = xs * cells / size
    y0 = fy.astype(int) % cells
    x0 = fx.astype(int) % cells
    y1 = (y0 + 1) % cells
    x1 = (x0 + 1) % cells
    ty = fy - np.floor(fy)
    tx = fx - np.floor(fx)
    ty = ty * ty * (3.0 - 2.0 * ty)
    tx = tx * tx * (3.0 - 2.0 * tx)
    a = grid[y0, x0] * (1 - tx) + grid[y0, x1] * tx
    b = grid[y1, x0] * (1 - tx) + grid[y1, x1] * tx
    return a * (1 - ty) + b * ty


def fbm(size: int, base_cells: int, octaves: int, seed: int) -> np.ndarray:
    """Fractal value noise in [0, 1]."""
    rng = np.random.default_rng(seed)
    total = np.zeros((size, size))
    amp, norm, cells = 1.0, 0.0, base_cells
    for _ in range(octaves):
        total += amp * _lattice_noise(size, cells, rng)
        norm += amp
        amp *= 0.5
        cells = min(cells * 2, size // 2)
    return total / norm


def stretched_noise(size: int, cells_x: int, cells_y: int, seed: int) -> np.ndarray:
    """Anisotropic value noise (different frequency per axis), tileable."""
    rng = np.random.default_rng(seed)
    grid = rng.random((cells_y, cells_x))
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float64)
    fy = ys * cells_y / size
    fx = xs * cells_x / size
    y0 = fy.astype(int) % cells_y
    x0 = fx.astype(int) % cells_x
    y1 = (y0 + 1) % cells_y
    x1 = (x0 + 1) % cells_x
    ty = fy - np.floor(fy)
    tx = fx - np.floor(fx)
    ty = ty * ty * (3.0 - 2.0 * ty)
    tx = tx * tx * (3.0 - 2.0 * tx)
    a = grid[y0, x0] * (1 - tx) + grid[y0, x1] * tx
    b = grid[y1, x0] * (1 - tx) + grid[y1, x1] * tx
    return a * (1 - ty) + b * ty


def painterly(img: np.ndarray, seed: int, amount: float = 0.10) -> np.ndarray:
    """Large soft tonal blotches + fine grain so flat fills read hand-painted."""
    blotch = fbm(SIZE, 4, 4, seed)
    img = img * (1.0 - amount + 2.0 * amount * blotch[..., None])
    grain = np.random.default_rng(seed + 1).normal(0.0, 0.015, (SIZE, SIZE, 1))
    warm = (fbm(SIZE, 3, 2, seed + 2) - 0.5) * 0.05
    img = img + grain
    img[..., 0] += warm
    img[..., 2] -= warm
    return img


def wrap_rect(img: np.ndarray, x0: int, y0: int, w: int, h: int, color) -> None:
    """Fill a rectangle with modulo wrap-around so shapes tile."""
    xs = np.arange(x0, x0 + w) % SIZE
    ys = np.arange(y0, y0 + h) % SIZE
    img[np.ix_(ys, xs)] = color


def wrap_disc(img: np.ndarray, cx: int, cy: int, r: float, color, blend: float = 1.0) -> None:
    """Paint a soft disc with wrap-around."""
    ys, xs = np.mgrid[0:SIZE, 0:SIZE]
    dx = np.minimum(np.abs(xs - cx % SIZE), SIZE - np.abs(xs - cx % SIZE))
    dy = np.minimum(np.abs(ys - cy % SIZE), SIZE - np.abs(ys - cy % SIZE))
    d = np.sqrt(dx * dx + dy * dy)
    mask = np.clip((r - d) / max(r * 0.35, 1.0), 0.0, 1.0) * blend
    img[:] = img * (1 - mask[..., None]) + np.asarray(color) * mask[..., None]


def save(img: np.ndarray, name: str) -> None:
    img = np.clip(img, 0.0, 1.0)
    out = (img * 255).astype(np.uint8)
    path = os.path.abspath(os.path.join(OUT_DIR, name))
    Image.fromarray(out).save(path)
    print(f"wrote {path}")


# ----------------------------------------------------------------------------
# Textures
# ----------------------------------------------------------------------------

def make_brick() -> np.ndarray:
    rng = np.random.default_rng(101)
    img = np.zeros((SIZE, SIZE, 3))
    img[:] = np.array([0.70, 0.66, 0.60])  # mortar
    rows, brick_w, mortar = 8, 128, 7
    row_h = SIZE // rows
    base = np.array([0.60, 0.29, 0.21])
    for r in range(rows):
        y0 = r * row_h
        off = brick_w // 2 if r % 2 else 0
        for b in range(SIZE // brick_w + 1):
            x0 = b * brick_w + off
            shade = 0.80 + 0.42 * rng.random()
            hue = (rng.random() - 0.5) * 0.10
            col = np.clip(base * shade + np.array([hue, hue * 0.25, -hue * 0.4]), 0, 1)
            wrap_rect(img, x0 + mortar // 2, y0 + mortar // 2,
                      brick_w - mortar, row_h - mortar, col)
            # top-edge highlight for a bevelled, chunky read
            wrap_rect(img, x0 + mortar // 2, y0 + mortar // 2,
                      brick_w - mortar, 3, np.clip(col * 1.22, 0, 1))
            wrap_rect(img, x0 + mortar // 2, y0 + row_h - mortar // 2 - 3,
                      brick_w - mortar, 3, col * 0.78)
    return painterly(img, 102, 0.10)


def make_metal() -> np.ndarray:
    rng = np.random.default_rng(201)
    base = np.array([0.47, 0.50, 0.55])
    img = np.zeros((SIZE, SIZE, 3))
    plate = 256
    for r in range(SIZE // plate):
        off = plate // 2 if r % 2 else 0
        for c in range(SIZE // plate + 1):
            shade = 0.88 + 0.22 * rng.random()
            wrap_rect(img, c * plate + off, r * plate, plate, plate, base * shade)
    # seams
    seam = base * 0.55
    for r in range(SIZE // plate):
        off = plate // 2 if r % 2 else 0
        wrap_rect(img, 0, r * plate, SIZE, 4, seam)
        for c in range(SIZE // plate + 1):
            wrap_rect(img, c * plate + off, r * plate, 4, plate, seam)
    # rivets along seams
    for r in range(SIZE // plate):
        off = plate // 2 if r % 2 else 0
        for c in range(SIZE // plate + 1):
            x0, y0 = c * plate + off, r * plate
            for i in range(1, 8):
                wrap_disc(img, x0 + 14, y0 + i * 32, 5.0, base * 1.35)
                wrap_disc(img, x0 + 14 + 2, y0 + i * 32 + 2, 3.0, base * 0.7, 0.6)
                wrap_disc(img, x0 + i * 32, y0 + 14, 5.0, base * 1.35)
                wrap_disc(img, x0 + i * 32 + 2, y0 + 14 + 2, 3.0, base * 0.7, 0.6)
    # brushed streaks
    streak = stretched_noise(SIZE, 6, 64, 202)
    img *= (0.92 + 0.16 * streak[..., None])
    return painterly(img, 203, 0.06)


def make_plaster() -> np.ndarray:
    img = np.zeros((SIZE, SIZE, 3))
    img[:] = np.array([0.89, 0.84, 0.75])
    mottle = fbm(SIZE, 6, 5, 301)
    img *= (0.90 + 0.18 * mottle[..., None])
    # sparse darker patches (weathering)
    patches = fbm(SIZE, 3, 3, 302)
    mask = np.clip((patches - 0.62) * 3.0, 0, 1)
    img = img * (1 - 0.18 * mask[..., None])
    return painterly(img, 303, 0.05)


def make_thatch() -> np.ndarray:
    rng = np.random.default_rng(401)
    base = np.array([0.76, 0.60, 0.30])
    img = np.tile(base, (SIZE, SIZE, 1)).astype(np.float64)
    # vertical straw strands
    x = 0
    while x < SIZE:
        w = int(rng.integers(3, 7))
        shade = 0.72 + 0.55 * rng.random()
        img[:, x:min(x + w, SIZE)] *= shade
        x += w
    strands = stretched_noise(SIZE, 64, 6, 402)
    img *= (0.85 + 0.3 * strands[..., None])
    # layered courses with shadowed underside
    layer_h = 128
    for r in range(SIZE // layer_h):
        y1 = (r + 1) * layer_h
        for i in range(14):
            fade = 1.0 - 0.45 * (1.0 - i / 14.0)
            row = (y1 - 1 - i) % SIZE
            img[row, :] *= fade
        img[(y1 - layer_h) % SIZE, :] *= 1.2  # lit top edge of the course
    return painterly(img, 403, 0.08)


def make_crops() -> np.ndarray:
    rng = np.random.default_rng(501)
    soil_dark = np.array([0.27, 0.18, 0.12])
    soil_lit = np.array([0.45, 0.32, 0.20])
    ys = np.arange(SIZE)
    ridge = 0.5 + 0.5 * np.sin(ys * 2.0 * np.pi / 64.0)
    img = soil_dark[None, None, :] + (soil_lit - soil_dark)[None, None, :] * ridge[:, None, None]
    img = img * (0.85 + 0.3 * fbm(SIZE, 24, 3, 502)[..., None])
    # crop tufts sit on ridge crests (every 64px, crest at +16)
    greens = [np.array([0.30, 0.55, 0.20]), np.array([0.40, 0.68, 0.24]),
              np.array([0.24, 0.46, 0.18]), np.array([0.47, 0.74, 0.30])]
    for row in range(SIZE // 64):
        cy = row * 64 + 16
        x = int(rng.integers(0, 18))
        while x < SIZE:
            g = greens[int(rng.integers(0, len(greens)))]
            r = float(rng.uniform(7.0, 12.0))
            jitter = int(rng.integers(-5, 6))
            wrap_disc(img, x, cy + jitter, r, g * 0.75)
            wrap_disc(img, x - 2, cy + jitter - 2, r * 0.65, g * 1.1)
            x += int(rng.integers(20, 30))
    return painterly(img, 503, 0.07)


def make_awning() -> np.ndarray:
    red = np.array([0.72, 0.20, 0.17])
    cream = np.array([0.93, 0.89, 0.80])
    img = np.zeros((SIZE, SIZE, 3))
    stripe = 64
    for s in range(SIZE // stripe):
        col = red if s % 2 == 0 else cream
        wrap_rect(img, s * stripe, 0, stripe, SIZE, col)
        wrap_rect(img, s * stripe, 0, 3, SIZE, col * 0.75)  # seam shadow
        wrap_rect(img, (s + 1) * stripe - 3, 0, 3, SIZE, col * 0.75)
    weave = stretched_noise(SIZE, 48, 96, 601)
    img *= (0.93 + 0.14 * weave[..., None])
    return painterly(img, 602, 0.05)


def make_slate() -> np.ndarray:
    rng = np.random.default_rng(701)
    img = np.zeros((SIZE, SIZE, 3))
    img[:] = np.array([0.20, 0.22, 0.26])  # gaps
    rows, tile_w, gap = 8, 128, 5
    row_h = SIZE // rows
    base = np.array([0.44, 0.48, 0.55])
    for r in range(rows):
        y0 = r * row_h
        off = tile_w // 2 if r % 2 else 0
        for t in range(SIZE // tile_w + 1):
            x0 = t * tile_w + off
            shade = 0.80 + 0.4 * rng.random()
            cool = (rng.random() - 0.5) * 0.06
            col = np.clip(base * shade + np.array([-cool, 0.0, cool]), 0, 1)
            wrap_rect(img, x0 + gap // 2, y0 + gap // 2, tile_w - gap, row_h - gap, col)
            wrap_rect(img, x0 + gap // 2, y0 + gap // 2, tile_w - gap, 3,
                      np.clip(col * 1.18, 0, 1))
        # drop shadow under each course to fake overlap
        for i in range(8):
            row = (y0 + row_h - 1 - i) % SIZE
            img[row, :] *= (0.62 + 0.38 * (i / 8.0))
    return painterly(img, 702, 0.08)


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    save(make_brick(), "brick_color.png")
    save(make_metal(), "metal_color.png")
    save(make_plaster(), "plaster_color.png")
    save(make_thatch(), "thatch_color.png")
    save(make_crops(), "crops_color.png")
    save(make_awning(), "awning_color.png")
    save(make_slate(), "slate_color.png")


if __name__ == "__main__":
    main()
