# Thorns codebase audit

Last updated: 2026-06-01. Scope: `Code/` (~500+ C# files).

## Architecture map

| Area | Path | Role |
|------|------|------|
| Terrain runtime | `Terrain/` | Chunks, replication, proc buildings, settlements, world-gen phases |
| Terraingen | `Terraingen/` | Heightmap pipeline, foliage/clutter populate, water (editor + runtime bridge) |
| Multiplayer | `Multiplayer/` | Lobby, spawn, pawn, replication |
| UI | `UI/` | Menu, HUD, shell partials, minimap |
| Building | `Building/` | Placement, snap, furniture catalog |
| Weapons / Combat | `Weapons/`, `Combat/` | Firing, viewmodels, vitals, weapon defs |
| Wildlife / AI | `Wildlife/`, `AI/` | Animals vs human NPCs |
| Persistence | `Persistence/` | World save DTOs + host authority |

## Completed cleanup (this pass)

- **Player spawn modularized**: `ThornsPlayerSpawnBootstrap.cs`, `ThornsPawnComponentEnsure.cs`; `ThornsGameManager` slimmed (~580 lines removed).
- **Dev components gated**: `ThornsInventoryDevControls` / `ThornsArmorDevControls` only added when `ThornsInventoryDev.EnableDevRpcs` is true.
- **Removed empty** `ServerGrantSpawnStarterLoadout`.
- **Legacy assets removed** (prior session): `LegacyModels/`, `LegacyMaterials/`, `LegacyTextures/`; terrain `.vtex` live under `terrain_materials/`.

## Mega-files (split next — highest ROI)

| File | ~Lines | Suggested split |
|------|--------|-----------------|
| `Terrain/ThornsTerrainSystem.cs` | 7,900 | Chunk host, decor, world-gen bridge |
| `Weapons/ThornsViewModelController.cs` | 6,600 | ADS, recoil, presentation |
| `Terrain/ThornsTerrainGeometry.cs` | 6,300 | Mesh build vs height sampling |
| `Weapons/ThornsWeapon.cs` | 6,500 | Fire, reload, RPCs, melee |
| `UI/ThornsDebugHudHost.GameHud.cs` | 4,900 | Tab panels |
| `Building/ThornsBuildingController.cs` | 4,500 | Snap vs networking |
| `Persistence/ThornsWorldPersistence.cs` | 4,500 | Per-domain serializers |
| `Terrain/ThornsProcBuildingInteriorSample.cs` | 4,400 | Loot vs props vs sampling |

## Patterns to consolidate

1. **World-gen `*DebugViz`** — shared draw host under `Terrain/WorldGen/Debug/`.
2. **Binary replicas** — `ThornsTerrainReplicaBinaryV1` + `ThornsPoiReplicaBinaryV1` shared write helpers.
3. **Host spatial indexes** — wildlife + player grids.
4. **Interactor template** — harvest, tame, radio, water drink (raycast + prompt + RPC).
5. **Interior furniture** — move `Building/ThornsInteriorFurniture*` + `Terrain/ThornsInterior*` into `Terrain/Interiors/`.

## Naming notes

- `ThornsProcBuildingPoc` — misnamed; holds production story/landmark toggles (not throwaway POC).
- `ThornsMainMenuUi.cs` vs class `ThornsMainMenuUI`.
- `Combat/ThornsWeaponDefinitions.cs` — consider moving to `Weapons/` (same `Sandbox` namespace).
- Terraingen types intentionally omit `Thorns` prefix (`Terraingen.TerrainGen` assembly).

## Safe conventions

- **Single sky authority**: `ThornsSun` + atmosphere shader; no scene skybox overrides.
- **Terrain materials**: `TerrainMaterialLibrary` + `terrain_materials/*.tmat` only.
- **Spawn loadout**: empty inventory unless persistence restore or `ThornsInventoryDev.EnableDevRpcs`.

## Follow-up backlog

- [ ] Split `ThornsTerrainSystem` / `ThornsTerrainGeometry`
- [ ] Split `ThornsWeapon` + `ThornsViewModelController`
- [ ] Extract `ThornsGameShell` inventory/craft partials
- [ ] `Terrain/Interiors/` folder consolidation
- [ ] Shared `ThornsWorldGenDebugViz` base
- [ ] Move `ThornsWeaponDefinitions.cs` → `Weapons/`
