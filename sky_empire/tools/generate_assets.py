"""Generate all Sky Empire art + sound.

- Logo: transparent RGBA wordmark rendered with the bundled Poppins fonts.
- Sounds: tiny synthesized wavs (buy, deny, cash, golden, rebirth, chest, quest, unlock).

All PNGs are true RGBA; fully transparent pixels are normalized to (0,0,0,0).
"""
import math
import random
import struct
import wave
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SOUNDS = ROOT / "Assets" / "sounds"
UI = ROOT / "Assets" / "ui"
FONTS = ROOT / "Assets" / "fonts"
SOUNDS.mkdir(parents=True, exist_ok=True)
UI.mkdir(parents=True, exist_ok=True)

# ---------------------------------------------------------------- logo


def make_logo():
    w, h = 1024, 512
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    big = ImageFont.truetype(str(FONTS / "Poppins-Black.ttf"), 150)
    small = ImageFont.truetype(str(FONTS / "Poppins-Bold.ttf"), 56)

    # Cloud puff behind the wordmark
    cloud = (255, 255, 255, 230)
    for cx, cy, r in [(300, 330, 120), (450, 300, 150), (620, 320, 135), (760, 350, 100), (520, 370, 160)]:
        d.ellipse((cx - r, cy - r // 2, cx + r, cy + r // 2), fill=cloud)

    def word(text, y, font, fill, outline, ow=6):
        bbox = d.textbbox((0, 0), text, font=font)
        x = (w - (bbox[2] - bbox[0])) // 2 - bbox[0]
        for dx in range(-ow, ow + 1, 2):
            for dy in range(-ow, ow + 1, 2):
                d.text((x + dx, y + dy), text, font=font, fill=outline)
        d.text((x, y), text, font=font, fill=fill)

    word("SKY", 60, big, (255, 210, 74, 255), (38, 46, 74, 255))
    word("EMPIRE", 210, big, (138, 224, 255, 255), (38, 46, 74, 255))
    word("build your island in the clouds", 400, small, (255, 255, 255, 255), (38, 46, 74, 255), ow=4)

    data = [(0, 0, 0, 0) if p[3] == 0 else p for p in img.getdata()]
    img.putdata(data)
    img.save(UI / "logo.png")
    print(f"logo: {UI / 'logo.png'}")

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
    random.seed(11)
    # buy — satisfying click-up (fires constantly, keep it short)
    write_wav("buy", seq(tone(440, 0.05, 0.4, "square", r=0.2), tone(660, 0.09, 0.45, "square", r=0.4)))
    # deny — soft low blip
    write_wav("deny", tone(180, 0.12, 0.4, "square", slide=-0.3, r=0.4))
    # cash — tiny soft pop, plays many times per second so keep gentle
    write_wav("cash", mix(tone(760, 0.05, 0.28, "sine", r=0.3), tone(1140, 0.04, 0.14, "sine", r=0.3)))
    # golden — sparkle arpeggio
    write_wav("golden", seq(
        tone(660, 0.08, 0.45, "tri", r=0.3), tone(880, 0.08, 0.45, "tri", r=0.3),
        mix(tone(1320, 0.2, 0.4, "sine", r=0.7), tone(1980, 0.2, 0.15, "sine", r=0.7))))
    # rebirth — rising fanfare
    write_wav("rebirth", seq(
        mix(tone(392, 0.14, 0.4, "tri"), tone(494, 0.14, 0.3, "tri")),
        mix(tone(523, 0.14, 0.42, "tri"), tone(659, 0.14, 0.32, "tri")),
        mix(tone(659, 0.4, 0.45, "tri", r=0.65), tone(784, 0.4, 0.35, "tri", r=0.65), tone(1046, 0.4, 0.25, "sine", r=0.65))))
    # chest — coin cascade
    coins = [pad(tone(900 + 140 * k + random.uniform(-30, 30), 0.07, 0.35, "square", r=0.25), 0.05 * k) for k in range(6)]
    write_wav("chest", mix(*coins))
    # quest — two-note ding
    write_wav("quest", seq(tone(784, 0.1, 0.4, "sine", r=0.3), mix(tone(1046, 0.28, 0.42, "sine", r=0.7), tone(1568, 0.28, 0.16, "sine", r=0.7))))
    # unlock — warm chime (floors, boosts)
    write_wav("unlock", mix(tone(523, 0.5, 0.35, "sine", r=0.8), pad(tone(784, 0.4, 0.3, "sine", r=0.8), 0.08),
                            pad(tone(1046, 0.35, 0.25, "sine", r=0.8), 0.16)))


if __name__ == "__main__":
    make_logo()
    make_sounds()
    print("done")
