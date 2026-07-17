# Kit craft

See also [FIRST_PASS.md](FIRST_PASS.md) — chat QA after every generation is considered process failure.

## Scaleable kit path (non-mesh)

| Do | Don't |
|----|--------|
| Fix `KitParts` / `PropSpecs` once | Re-tune each sedan in chat |
| Call frozen assemblies | Invent wheel rotations in pieces |
| Screenshot for review | List every defect every time |
| Mesh when kits plateau | Endless box-polish loops |

## Axis contract

- Origin: ground under center  
- `+X` = forward (road direction)  
- `+Z` = up  
- Wheels: size `(diameter, thickness, diameter)` — thin on Y, rolls along X  

## Pipeline

1. New prop class → Spec + Piece using existing KitParts  
2. New visual *part type* → isolated bake into KitParts → freeze  
3. Place in WorkbenchScene  
4. User glance / one screenshot → fix Specs/Parts only if contract broken  

## Part budgets

Street 5–12 · Utility 6–14 · Vehicle 18–35 · else mesh lane.
