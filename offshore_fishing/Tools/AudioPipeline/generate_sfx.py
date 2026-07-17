#!/usr/bin/env python3
"""Generate simple WAV SFX beds (no external deps)."""
from __future__ import annotations

import math
import struct
import wave
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "Assets" / "sounds"


def write_tone(name: str, freq: float, seconds: float, volume: float = 0.3, decay: bool = True):
    OUT.mkdir(parents=True, exist_ok=True)
    rate = 22050
    n = int(rate * seconds)
    with wave.open(str(OUT / f"{name}.wav"), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            t = i / rate
            env = (1.0 - t / seconds) if decay else 1.0
            sample = int(32767 * volume * env * math.sin(2 * math.pi * freq * t))
            frames += struct.pack("<h", sample)
        w.writeframes(frames)


def write_noise(name: str, seconds: float, volume: float = 0.15):
    OUT.mkdir(parents=True, exist_ok=True)
    rate = 22050
    n = int(rate * seconds)
    x = 1
    with wave.open(str(OUT / f"{name}.wav"), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            x = (1103515245 * x + 12345) & 0x7FFFFFFF
            noise = ((x / 0x7FFFFFFF) * 2 - 1)
            env = 1.0 - (i / n)
            sample = int(32767 * volume * env * noise)
            frames += struct.pack("<h", sample)
        w.writeframes(frames)


def main():
    write_tone("ui_click", 880, 0.06, 0.25)
    write_tone("coin", 1320, 0.12, 0.28)
    write_tone("catch", 523, 0.35, 0.3)
    write_tone("bite", 220, 0.1, 0.35)
    write_noise("splash", 0.25, 0.2)
    write_noise("ocean_loop", 2.0, 0.08)
    write_tone("purchase", 660, 0.18, 0.28)
    # cozy music stub: arpeggio loop fragment
    OUT.mkdir(parents=True, exist_ok=True)
    rate = 22050
    seconds = 4.0
    n = int(rate * seconds)
    notes = [262, 330, 392, 523, 392, 330]
    with wave.open(str(OUT / "music_dock.wav"), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            t = i / rate
            note = notes[int(t * 2) % len(notes)]
            sample = int(32767 * 0.12 * math.sin(2 * math.pi * note * t) * (0.5 + 0.5 * math.sin(t * 1.5)))
            frames += struct.pack("<h", sample)
        w.writeframes(frames)
    print(f"Wrote sounds to {OUT}")


if __name__ == "__main__":
    main()
