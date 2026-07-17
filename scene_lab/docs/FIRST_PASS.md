# First-pass rules (no ages of chat QA)

Cursor only saves time if the **first** spawn is mostly right. Verbally correcting every prop is not a process.

## What we do instead

1. **Frozen parts** — wheels, lights, etc. live only in `KitParts`. Pieces call them; they do not invent rotations.
2. **Frozen specs** — sizes/ratios live in `PropSpecs`. Pieces do not sprinkle magic numbers for lights/wheels.
3. **Axis contract** (always) — `+X` forward along the road, `+Z` up, wheel thin axis `+Y`. Written on every vehicle piece header.
4. **One library bake** — when a *new* part type is needed (e.g. streetlamp), build it in isolation in the workbench, human OK once, then freeze into `KitParts`. Never redesign wheels inside `CarSedanPiece`.
5. **Screenshot > essay** — if something’s wrong, paste one workbench screenshot. The agent should fix from vision + Specs/Parts. Do not list every defect in prose as the default loop.
6. **Mesh when kits plateau** — if first-pass kits still fail the “reads as a car in 2 seconds” bar after Specs/Parts are frozen, switch that asset class to Blender/Tripo. That’s faster than polish-chat forever.

## Agent must NOT

- Freehand-rotate cylinders “until it looks right” inside a Piece
- Ask the user to rediscover axis bugs that violate the contract above
- Treat chat taste-feedback as the primary quality mechanism

## Smoke check before saying “done” on a vehicle

- [ ] Uses `KitParts.Wheel` / `HeadlightPair` only
- [ ] Wheels: cylinder with **roll 90°** (`Angles(0,0,90)`), NOT pitch 90 — circular faces ±Y
- [ ] Body parts stacked with gaps; use `materials/kit_opaque.vmat` — no lower-body dither
- [ ] No box “arch slabs” that read as sideways wheels
- [ ] Headlights inset (`HeadlightSpanFrac` ~0.28), in the nose — not bumper corners
- [ ] Car `+X` aligns with road corridor `+X`
