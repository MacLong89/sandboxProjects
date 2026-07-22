"""Generate all Catch a Critter! art + sound.

- Codex icons: one transparent RGBA chibi sprite per species, drawn from the
  same visual genes (body/ears/tail/palette) the in-game 3D builder uses.
  Parsed straight out of Code/Data/Species.cs so they never drift.
- Sounds: tiny synthesized wavs (swing, catch, shiny, rare, sell, buy, unlock).

All actor PNGs are true RGBA; fully transparent pixels are normalized to
(0,0,0,0).
"""
import math
import random
import re
import struct
import wave
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SPECIES_CS = ROOT / "Code" / "Data" / "Species.cs"
ICONS = ROOT / "Assets" / "ui" / "critters"
SOUNDS = ROOT / "Assets" / "sounds"
ICONS.mkdir(parents=True, exist_ok=True)
SOUNDS.mkdir(parents=True, exist_ok=True)

SHINY_GOLD = (255, 217, 89)

# ---------------------------------------------------------------- species parse

LINE = re.compile(
    r'S\(\s*"(?P<id>\w+)",\s*"(?P<name>[^"]+)",\s*Biome\.(?P<biome>\w+),\s*Rarity\.(?P<rarity>\w+),\s*'
    r'BodyShape\.(?P<body>\w+),\s*EarStyle\.(?P<ears>\w+),\s*TailStyle\.(?P<tail>\w+),\s*'
    r'"(?P<c1>#[0-9a-fA-F]{6})",\s*"(?P<c2>#[0-9a-fA-F]{6})",\s*(?P<size>[\d.]+)f'
)


def parse_species():
    text = SPECIES_CS.read_text(encoding="utf-8")
    out = [m.groupdict() for m in LINE.finditer(text)]
    if not out:
        raise SystemExit("No species parsed — did Species.cs change format?")
    return out


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def dark(c, f=0.8):
    return tuple(int(v * f) for v in c)


def light(c, f=0.35):
    return tuple(int(v + (255 - v) * f) for v in c)

# ---------------------------------------------------------------- icon drawing


def draw_critter(d, sp, pri, sec):
    """Chibi front view on a 32x32 grid."""
    body = sp["body"]
    ears = sp["ears"]
    tail = sp["tail"]
    outline = dark(pri, 0.55)

    # tail behind body
    if tail == "Bushy":
        d.ellipse((1, 12, 11, 24), fill=dark(pri, 0.9))
        d.ellipse((2, 13, 8, 19), fill=sec)
    elif tail == "Long":
        d.line((4, 22, 1, 12), fill=dark(pri, 0.9), width=2)
        d.ellipse((0, 10, 4, 14), fill=sec)
    elif tail == "Curl":
        d.arc((2, 10, 12, 22), 90, 320, fill=dark(pri, 0.9), width=2)
    elif tail == "Fin":
        d.polygon([(4, 22), (1, 14), (8, 18)], fill=sec)
    elif tail == "Nub":
        d.ellipse((4, 20, 9, 25), fill=sec)

    # body
    if body == "Tall":
        bx = (9, 12, 23, 29)
        hy = 4
    elif body == "Long":
        bx = (6, 16, 26, 29)
        hy = 8
    elif body == "Chunky":
        bx = (7, 13, 25, 29)
        hy = 6
    else:  # Round
        bx = (8, 14, 24, 29)
        hy = 7
    d.ellipse(bx, fill=pri, outline=outline)
    # belly
    cx = (bx[0] + bx[2]) // 2
    d.ellipse((cx - 4, bx[1] + 5, cx + 4, bx[3] - 1), fill=sec)

    # feet
    d.ellipse((bx[0] + 2, bx[3] - 3, bx[0] + 7, bx[3] + 1), fill=dark(pri, 0.7))
    d.ellipse((bx[2] - 7, bx[3] - 3, bx[2] - 2, bx[3] + 1), fill=dark(pri, 0.7))

    # head
    hx0, hx1 = cx - 7, cx + 7
    d.ellipse((hx0, hy, hx1, hy + 13), fill=pri, outline=outline)

    # ears (behind-ish, drawn over head edges)
    if ears == "Pointy":
        d.polygon([(hx0 + 1, hy + 3), (hx0 - 1, hy - 4), (hx0 + 5, hy + 1)], fill=pri, outline=outline)
        d.polygon([(hx1 - 1, hy + 3), (hx1 + 1, hy - 4), (hx1 - 5, hy + 1)], fill=pri, outline=outline)
    elif ears == "Round":
        d.ellipse((hx0 - 2, hy - 3, hx0 + 4, hy + 3), fill=pri, outline=outline)
        d.ellipse((hx1 - 4, hy - 3, hx1 + 2, hy + 3), fill=pri, outline=outline)
    elif ears == "Long":
        d.ellipse((hx0, hy - 9, hx0 + 4, hy + 3), fill=pri, outline=outline)
        d.ellipse((hx1 - 4, hy - 9, hx1, hy + 3), fill=pri, outline=outline)
        d.ellipse((hx0 + 1, hy - 7, hx0 + 3, hy + 1), fill=sec)
        d.ellipse((hx1 - 3, hy - 7, hx1 - 1, hy + 1), fill=sec)
    elif ears == "Horn":
        d.polygon([(cx - 1, hy + 1), (cx, hy - 6), (cx + 2, hy + 1)], fill=sec)
        d.polygon([(hx0 + 1, hy + 2), (hx0 - 2, hy - 2), (hx0 + 4, hy)], fill=sec)
        d.polygon([(hx1 - 1, hy + 2), (hx1 + 2, hy - 2), (hx1 - 4, hy)], fill=sec)
    elif ears == "Antenna":
        d.line((cx - 3, hy + 1, cx - 5, hy - 6), fill=dark(pri, 0.7), width=1)
        d.line((cx + 3, hy + 1, cx + 5, hy - 6), fill=dark(pri, 0.7), width=1)
        d.ellipse((cx - 7, hy - 8, cx - 4, hy - 5), fill=sec)
        d.ellipse((cx + 4, hy - 8, cx + 7, hy - 5), fill=sec)

    # face
    d.ellipse((cx - 5, hy + 5, cx - 2, hy + 9), fill=(18, 16, 22))
    d.ellipse((cx + 2, hy + 5, cx + 5, hy + 9), fill=(18, 16, 22))
    d.point((cx - 4, hy + 6), fill=(240, 240, 250))
    d.point((cx + 3, hy + 6), fill=(240, 240, 250))
    d.ellipse((cx - 1, hy + 9, cx + 1, hy + 11), fill=dark(sec, 0.7))

    # blush
    d.point((cx - 6, hy + 10), fill=light(pri, 0.5))
    d.point((cx + 6, hy + 10), fill=light(pri, 0.5))


def save_icon(img, path, scale=4):
    out = img.resize((img.width * scale, img.height * scale), Image.Resampling.NEAREST)
    data = [(0, 0, 0, 0) if p[3] == 0 else p for p in out.getdata()]
    out.putdata(data)
    out.save(path)


def make_icons():
    species = parse_species()
    for sp in species:
        img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        draw_critter(d, sp, hex_rgb(sp["c1"]), hex_rgb(sp["c2"]))
        save_icon(img, ICONS / f"{sp['id']}.png")
    print(f"icons: {len(species)} written to {ICONS}")

# ---------------------------------------------------------------- sounds

SR = 22050


def env(i, n, a=0.01, r=0.5):
    t = i / n
    attack = min(1.0, t / max(a, 1e-6))
    release = min(1.0, (1 - t) / max(r, 1e-6))
    return min(attack, release)


def tone(freq, dur, vol=0.5, shape="sine", slide=0.0, a=0.01, r=0.5):
    n = int(SR * dur)
    out = []
    phase = 0.0
    for i in range(n):
        f = freq * (1 + slide * (i / n))
        phase += 2 * math.pi * f / SR
        if shape == "sine":
            s = math.sin(phase)
        elif shape == "square":
            s = 1.0 if math.sin(phase) > 0 else -1.0
        elif shape == "tri":
            s = 2 / math.pi * math.asin(math.sin(phase))
        else:  # noise
            s = random.uniform(-1, 1)
        out.append(s * vol * env(i, n, a, r))
    return out


def mix(*tracks):
    n = max(len(t) for t in tracks)
    out = [0.0] * n
    for t in tracks:
        for i, s in enumerate(t):
            out[i] += s
    peak = max(1.0, max(abs(s) for s in out))
    return [s / peak * 0.85 for s in out]


def seq(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def pad(track, seconds):
    return [0.0] * int(SR * seconds) + track


def write_wav(name, samples):
    path = SOUNDS / f"{name}.wav"
    with wave.open(str(path), "w") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32767)) for s in samples))
    print(f"sound: {path.name}")


def make_sounds():
    random.seed(7)
    # swing — airy whoosh
    write_wav("swing", mix(tone(0, 0.16, 0.5, "noise", a=0.15, r=0.25), tone(320, 0.16, 0.12, "sine", slide=-0.5)))
    # catch — bright pop + blip
    write_wav("catch", seq(tone(520, 0.05, 0.6, "square", a=0.01, r=0.2), tone(780, 0.09, 0.5, "sine", slide=0.4, r=0.3)))
    # shiny — sparkle arpeggio
    write_wav("shiny", seq(
        tone(660, 0.09, 0.45, "tri", r=0.3), tone(880, 0.09, 0.45, "tri", r=0.3),
        tone(1100, 0.09, 0.45, "tri", r=0.3), mix(tone(1320, 0.22, 0.4, "sine", r=0.7), tone(1980, 0.22, 0.15, "sine", r=0.7))))
    # rare — two-note fanfare
    write_wav("rare", seq(mix(tone(392, 0.12, 0.4, "tri"), tone(494, 0.12, 0.3, "tri")),
                          mix(tone(523, 0.3, 0.45, "tri", r=0.6), tone(659, 0.3, 0.3, "tri", r=0.6))))
    # sell — coin cascade
    coins = [pad(tone(900 + 130 * k + random.uniform(-30, 30), 0.07, 0.35, "square", r=0.25), 0.045 * k) for k in range(6)]
    write_wav("sell", mix(*coins))
    # buy — click-up
    write_wav("buy", seq(tone(440, 0.06, 0.4, "square", r=0.2), tone(660, 0.1, 0.45, "square", r=0.4)))
    # unlock — warm chime
    write_wav("unlock", mix(tone(523, 0.5, 0.35, "sine", r=0.8), pad(tone(784, 0.4, 0.3, "sine", r=0.8), 0.08),
                            pad(tone(1046, 0.35, 0.25, "sine", r=0.8), 0.16)))


if __name__ == "__main__":
    make_icons()
    make_sounds()
    print("done")
