# Thorns — High-Risk Optimizations (Implemented)

## Phase 5 — Host-authoritative world + height cache

- Default `ClientsGenerateDeterministic = false` — clients do **not** re-sculpt unless cache miss + legacy flag.
- `ThornsWorldSession` publishes `world_seed`, `world_version`, `world_ready` via lobby data.
- `ThornsTerrainHeightCache` saves/loads heightfield to `FileSystem.Data/thorns_world_cache/`.
- `ThornsWorldHeightCacheRpc` streams cache from host to clients in ~32k-float chunks.
- Clients: load disk cache → else request RPC → else wait/retry.
- Host: async sculpt → save cache → publish session → apply terrain + cosmetics.
- Clients get foliage/grass from shared heightfield.

**Tuning:** `ThornsTerrainConfig.WorldBuildVersion` — bump to invalidate all caches.

## Phase 6 — Instanced trees

- `ThornsFoliageConfig.UseInstancedTrees` (default **true**).
- `ThornsFoliageInstancedRenderer` draws per-chunk GPU instances (pine/aspen/oak).
- Chunk culling + shadow band by distance on instanced path.
- Legacy per-tree GameObjects when `UseInstancedTrees = false`.

## Phase 12 — Async terrain

- `ThornsTerrainAsyncGenerator` spreads load → crop/resize → sculpt across frames.
- Reduces single-frame startup hitch on host.

## QA checklist

- [ ] Host MP: client sees terrain + trees without local sculpt (check log for cache RPC).
- [ ] Second join same machine: instant cache load from disk.
- [ ] `UseInstancedTrees`: confirm forest renders, FPS improved vs false.
- [ ] Solo offline: full gen still works.
