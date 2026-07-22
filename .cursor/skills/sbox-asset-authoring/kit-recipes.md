# Kit recipes (s&box)

Compose with tinted boxes/spheres. Units: Source-style (copy neighbors in the active scene). Full craft rules: `scene_lab/docs/KIT_CRAFT.md`.

## Craft order (every prop)

1. Silhouette name  
2. Lock origin (ground) + forward axis (+X default)  
3. Primary volumes (2–4)  
4. Secondary volumes  
5. Trim cues (glass, lights, wheels) with ±1 face offsets  
6. ≤ 5 palette tints  

## Sedan (vehicle kit)

Study `scene_lab/Code/Pieces/CarSedanPiece.cs`.

| Layer | Parts |
|-------|--------|
| Primary | rocker, hood, deck/trunk, cabin, roof |
| Glass | windshield, rear, side (±1 face) |
| Nose/tail | grille, bumpers, headlights, taillights |
| Contact | wheel spheres on ground (center Z = radius), arches, hubs |

Proportions: length ≫ width; cabin set back from hood; wheelbase ~0.3 of length from center.

## Dumpster

Study `DumpsterPiece.cs`: bin → larger lid (+Z, slight overhang) → side pockets → caster spheres.

## Chair

Study `ChairPiece.cs`: seat → cushion → 4 legs → back posts + panel behind seat (−Y face nudge).

## Tree / bush / house / crate / fence / hydrant / creature

| Prop | Primary |
|------|---------|
| Tree | trunk box + 2–3 canopy boxes/spheres |
| Bush | 2–4 overlapping spheres near ground |
| House | foundation, walls, roof; façade door/windows ±1 |
| Crate | body + lid (+1 Z) |
| Fence | posts + rails along run length |
| Hydrant | base, body, cap, nozzle |
| Creature placeholder | torso + head + limb boxes |

## Anti-patterns

- One big box “car” or “chair”
- Wheels floating or sunk through the road
- Glass coplanar with cabin (z-fight)
- Inventing new unit scales mid-scene
- >40 primitive parts (switch to mesh)
