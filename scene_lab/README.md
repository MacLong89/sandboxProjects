# Scene Lab

Standalone s&box look-dev project. Build modular scene pieces here, walk them, then copy pieces into other games.

## Open

1. Steam → s&box → open `scene_lab/scene_lab.sbproj`
2. Play `scenes/workbench.scene` (startup scene)

## Iterate in Cursor

Say things like:

- “Create a road with sidewalks and grass embankments”
- “Add trees and a fire hydrant on the grass/sidewalk”

Agents should edit:

- `Code/Scene/WorkbenchScene.cs` — what is placed (composition)
- `Code/Pieces/*.cs` — reusable modular builders

Press **R** in-game to rebuild the workbench after code hot-reload / restart.

## Kit craft (cars, dumpsters, chairs, …)

See `docs/KIT_CRAFT.md`. New props = new `Code/Pieces/*Piece.cs`, then place in `WorkbenchScene.cs`.

Reference kits: sedan, dumpster, chair, hydrant, tree.

## Export to other games

Copy a piece file from `Code/Pieces/` plus any kit helpers it needs from `Code/Kit/`, then call the static `Build(...)` from that game’s world builder. Keep namespaces adjustable.
