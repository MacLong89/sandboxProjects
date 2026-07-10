# Thorns — World Generation Design Specification

**Document:** `THORNS_WORLD_GENERATION.md`  
**Status:** Single source of truth (design authority)  
**Scope:** World generation, settlements, procedural buildings, scale, density, player experience, architectural rules, future expansion  
**Not in scope:** Implementation code, API reference, or engine-specific tuning values unless listed here as locked design constants.

---

## Document purpose

This specification exists to **stop procedural and world-generation architecture drift**. Any feature that places structures, modifies terrain, connects settlements, or expands the building pipeline **must conform to this hierarchy and these rules** unless this document is explicitly revised.

When implementation and design disagree, **this document wins** until a deliberate design change is recorded here.

---

## Section 1 — Core design goals

### Philosophy

Thorns world generation is not “random building scatter.” It is **curated macro geography** with **sparse, high-value landmarks** embedded in a hostile wilderness. Procedural systems exist to **author believable places**, not to fill space.

The world should feel:

| Quality | Meaning |
|--------|---------|
| **Sparse** | Long stretches without guaranteed safety or loot |
| **Atmospheric** | Mood from emptiness, weather, and scale — not clutter |
| **Memorable** | A small number of strong silhouettes players remember and navigate by |
| **Dangerous** | Exposure during travel; hubs concentrate risk and reward |
| **Readable** | Players understand where they are from terrain, roads, and skyline |
| **Intentionally designed** | POIs feel placed for gameplay, not noise-generated |
| **Survival-focused** | Geography serves tension, scavenging, and vulnerability |

### Travel

Travel should feel:

- **Lonely** — the map is mostly wilderness; companions and structures are exceptions  
- **Vulnerable** — cover is limited; distance matters  
- **Uncertain** — the next landmark is not guaranteed; routes can be contested  

The **wilderness is part of the survival atmosphere**, not dead space between POIs.

### Settlements

Settlements should feel:

- **Tense** — other players and threats concentrate near value  
- **Exposed** — open ground, sightlines, and approach vectors matter  
- **Valuable** — loot and progression justify the risk  
- **Recognizable** — city vs town vs isolated site are distinct reads at distance  

### Anti-goals (do not ship toward these)

- Procedural noise masquerading as content  
- Evenly distributed clutter (“a building every X meters”)  
- Constant landmarks removing map tension  
- Overly dense worlds (Rust-style base soup, urban sprawl)  
- Repetitive spammed POIs with no identity  
- Worlds where travel is only “running between identical loot boxes”  

---

## Section 2 — World hierarchy

All world-generation logic must follow this **strict top-down hierarchy**. Do not skip layers or merge responsibilities across them.

```
WORLD
  → REGIONS
    → SETTLEMENTS
      → BUILDINGS
        → ROOMS
          → TILES
```

### WORLD

**Purpose:** The playable planet — deterministic from seed, shared by all clients on join.

**Owns:**

- Terrain heightfield and collision mesh  
- Global seed and replication of world spec  
- Macro layout: where settlements exist, wilderness scale, coastal bounds  
- Resource scatter policies (trees, nodes, boulders) that respect building footprints  
- Water plane, edge falloff, and world bounds  

**Does not own:** Individual wall placement, interior props, or per-room loot tables.

---

### REGIONS

**Purpose:** Large-scale environmental character across the map — how the wilderness *feels* between hubs.

**Owns:**

- Forest density and clustering  
- Plains, hills, and open sightlines  
- Lakes, wet bands, and shoreline behavior  
- Mountainous or elevated terrain bias (where used)  
- Coastlines and map-edge treatment  

**Does not own:** City manifests, building blueprints, or road graphs (those are settlement/world-flow layers).

Regions inform **where** settlements are *allowed* to score well (flat town sites, elevated military sites, etc.) but do not replace settlement rules.

---

### SETTLEMENTS

**Purpose:** Human-made clusters — the **macro POI system**.

**Owns:**

- **One main city** (12 buildings, fixed role manifest)  
- **Three towns** (5 buildings each, fixed town identities A/B/C)  
- **Three isolated wilderness structures** (drawn from a small pool)  
- Hub flattening zones, spacing, trails/roads between hubs  
- Settlement-level district flavor (commercial, mixed, rural)  

**Does not own:** Tile-level ramp headroom or interior wall graphs.

---

### BUILDINGS

**Purpose:** A single structure instance — one gameplay POI with a **type identity** (House, Factory, Skyscraper, etc.).

**Owns:**

- Footprint (width/depth in tile cells)  
- Story count and vertical silhouette  
- Material tier presentation (wood / stone / metal)  
- Door placement, exterior facade, ruin/destroyed visual variant at spawn  
- Loot tier expectations and interior scatter hooks  

**Generated from:** Modular tile blueprints compiled into validated layouts.

---

### ROOMS

**Purpose:** Playable interior space — **implied**, not a separate mesh pipeline today.

**Owns:**

- Connectivity from primary door  
- Interior wall dividers (auto-generated from layout)  
- Open vs occupied cells, ramp shafts, openings  
- Future: room identity (kitchen, armory, office) for loot and storytelling  

**Does not own:** World position or terrain pads (building layer).

---

### TILES

**Purpose:** The atomic modular cell — the **only** scale at which geometry is authored in blueprints.

**Owns:**

- Floor / opening / ramp / door / window flags per cell per story  
- Alignment to the global 100×100 plan grid  
- Perimeter wall emission on unoccupied edges  

**Does not own:** Settlement placement or region biomes.

---

## Section 3 — World scale (locked constants)

These values are **design law**. Changing them requires updating this document and auditing all dependent systems (terrain, networking, UI minimap, loot, AI).

| Constant | Value | Notes |
|----------|-------|--------|
| **World size** | **32 768 × 32 768** world units | Centered on origin; playable area inset from absolute edges |
| **Plan cell (tile)** | **100 × 100** units (XY) | One foundation bay; `ThornsBuildingModule.Cell` |
| **Floor slab thickness** | **5** units | Foundation thickness |
| **Wall band height** | **100** units | Per-story vertical wall module |
| **Story height** | **105** units | Slab + wall band = one full storey |
| **Wall thickness** | **5** units | Perimeter wall depth |
| **Building rotation** | **90° increments only** | 0°, 90°, 180°, 270° — no free rotation for proc settlements |
| **Major structure budget** | **~30** proc settlement buildings | 12 + 15 + 3 — see Section 4 |
| **World density class** | **Sparse (DayZ-like)** | Not Rust/PUBG urban density |

### Why sparse density is intentional

1. **Pacing** — Long quiet traversal makes landmarks meaningful.  
2. **Multiplayer readability** — Players navigate by memory and silhouette, not minimap spam.  
3. **Performance** — Each building is many networked pieces; density scales cost linearly.  
4. **Survival fantasy** — Scarcity increases tension and loot value.  
5. **Design control** — Fixed manifests (city roles, town identities) beat infinite random archetype soup.  

A “busier” world is a **different game**. Increase density only via a documented design revision and performance budget.

### Heightmap note (implementation constraint)

Terrain is sampled on a **coarse heightfield** relative to world size (~64 m per vertex at 512² on a 32k map). Settlement flattening must treat **entire footprints**, not center points, or hills will clip through walls. Apron blending must be **wide and smooth** or players will see cliff-like hub edges.

---

## Section 4 — World structure

### Macro layout (finalized)

| Hub | Count | Role |
|-----|-------|------|
| **Main city** | **12 buildings** | Regional capital; skyline; highest verticality and loot pressure |
| **Towns** | **3 × 5 buildings** | Scavenging loops; simpler footprints; distinct A/B/C manifests |
| **Isolated sites** | **3 structures** | Wilderness landmarks; exploration and ambush fantasy |
| **Total** | **30 major proc structures** | Fixed budget per world seed |

### Main city

- **12 buildings** — fixed composition (outer industrial/residential, mid commercial, core high-rise)  
- Placed **near map center** (jitter allowed; not arbitrary wilderness)  
- **Tallest skyline** on the map (Skyscraper, Apartment Tower, Office)  
- **Highest loot density** and **strongest vertical gameplay** (multi-story, ramps, metal tier bias in core)  
- **Largest terrain flattening hub** — shared plateau with per-building footprint shaving  

### Towns (A, B, C)

- **5 buildings each** — manifests are **fixed per town label**, not fully random  
- **Lower verticality** than city on average  
- **Wood-forward material bias** — rural read  
- **Smaller hub radius** than city  
- Positioned on **orbital scatter** around the map with minimum separation from city and each other  

### Isolated structures

- **Exactly three** per world  
- Drawn from pool: Cabin, Ruin, Barn, Military Complex, Radio Outpost (seed shuffles assignment)  
- **Minimum clearance** from city and towns — wilderness-only  
- **Exploration rewards** — memorable one-off discoveries, ambush potential  

### Player movement flow

```
Wilderness (long) → glimpse road/trail/silhouette → approach hub edge →
scavenge/combat loop → leave via road or open terrain → wilderness (long)
```

- **City** is the predictable “gravity well” near center.  
- **Towns** are mid-map detours — worth routing to, not mandatory.  
- **Isolated** sites reward off-trail exploration.  

### Settlement readability

Players should identify:

| From distance | Cue |
|---------------|-----|
| **City** | Cluster of tall vertical pieces, widest flat zone |
| **Town** | Small cluster, low/mid silhouette |
| **Isolated** | Single structure, no cluster |

### Wilderness spacing philosophy

Space between hubs is **intentionally empty** of proc buildings. Wilderness gameplay (resources, wildlife, weather, players) fills that space. **Do not** fill gaps with extra proc POIs without revising the 30-structure budget.

---

## Section 5 — Building identities

Footprint ranges are in **tile cells** (×100 units). Stories are **design targets**; exact layout comes from blueprint compile.

### House

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | General residential scavenging; common town/city outer housing |
| **Visual identity** | Small multi-story home, modest footprint |
| **Materials** | Wood or stone (see Section 6) |
| **Footprint** | ~4–5 cells per side |
| **Verticality** | 2 stories |
| **Loot** | Low–medium; general survival loot |
| **PvP** | Limited vertical advantage; doorways are choke points |

### Ruin

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Fast loot, low threat, atmospheric decay |
| **Visual identity** | Damaged residential read; missing wall pieces |
| **Materials** | Wood or stone |
| **Footprint** | ~4–5 cells |
| **Verticality** | 2 stories (often compromised) |
| **Loot** | Low; quick pass |
| **PvP** | Poor cover integrity; predictable paths |

### Warehouse

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Industrial scavenging; open floor plates |
| **Visual identity** | Wide, low, boxy |
| **Materials** | Stone or metal |
| **Footprint** | Large (~8–10 × 4–5 cells) |
| **Verticality** | 2–3 stories |
| **Loot** | Medium; crafting/industrial bias |
| **PvP** | Long sightlines inside; strong exterior corners |

### Military Complex

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | High-risk high-value; metal loot; radio spawn potential |
| **Visual identity** | Fortified compound, largest rural footprint |
| **Materials** | **Metal** (pure) |
| **Footprint** | Very large (~9–12 cells) |
| **Verticality** | 1–2 stories |
| **Loot** | High; military-tier table |
| **PvP** | Open yards, strong defensive fantasy; isolated sites are ambush magnets |

### Cabin

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Wilderness starter loot; lonely atmosphere |
| **Visual identity** | Small wooden shack |
| **Materials** | **Wood** (pure) |
| **Footprint** | ~3–4 cells |
| **Verticality** | 1 story |
| **Loot** | Low |
| **PvP** | Single-door choke; minimal vertical play |

### Store

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Commercial scavenging; town staple |
| **Visual identity** | Retail frontage, windows |
| **Materials** | Wood or stone |
| **Footprint** | ~5–6 × 3–4 cells |
| **Verticality** | 1 story |
| **Loot** | Medium; general + consumable bias |
| **PvP** | Street-facing doors; glass/window sightlines |

### Apartment Block

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Mid-density urban loot; vertical sweep |
| **Visual identity** | Mid-rise residential box |
| **Materials** | **Stone** (pure) |
| **Footprint** | ~4–5 × 5–7 cells |
| **Verticality** | 3 stories |
| **Loot** | Medium–high |
| **PvP** | Multiple floors; stair/ramp fights |

### Factory

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Industrial hub; metal/stone; city outer ring |
| **Visual identity** | Heavy hall + upper floors, ramps |
| **Materials** | Stone or metal |
| **Footprint** | Large (~7–9 × 5–7 cells) |
| **Verticality** | 2–3 stories |
| **Loot** | Medium–high industrial |
| **PvP** | Complex interior; ramp zones |

### Barn

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Rural flavor; town B identity |
| **Visual identity** | Long wooden agricultural box |
| **Materials** | **Wood** (pure) |
| **Footprint** | ~5–7 × 4–5 cells |
| **Verticality** | 1 story |
| **Loot** | Low–medium |
| **PvP** | Open interior; few doors |

### Radio Outpost

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Signal fantasy; metal loot; isolated pool |
| **Visual identity** | Compact tech site |
| **Materials** | **Metal** (pure) |
| **Footprint** | ~4–5 cells |
| **Verticality** | 2 stories |
| **Loot** | Medium–high; radio-related rewards |
| **PvP** | High value target; exposed placement |

### Apartment Tower

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | City core vertical landmark |
| **Visual identity** | Narrow tall tower |
| **Materials** | **Metal** (pure) |
| **Footprint** | ~5 × 5 cells |
| **Verticality** | 5 stories |
| **Loot** | High |
| **PvP** | Strong height advantage; choke at ground floor |

### Skyscraper

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | Premier city POI; maximum verticality |
| **Visual identity** | Tallest silhouette on map |
| **Materials** | **Metal** (pure) |
| **Footprint** | ~6 × 6 cells |
| **Verticality** | **8 stories** |
| **Loot** | Highest city concentration |
| **PvP** | Endgame urban combat; floor control matters |

### Office Building

| Attribute | Specification |
|-----------|----------------|
| **Gameplay role** | City core commercial; mid-high vertical |
| **Visual identity** | Broad office block |
| **Materials** | Stone or metal (metal bias in core) |
| **Footprint** | ~6 × 5 cells |
| **Verticality** | 4 stories |
| **Loot** | High |
| **PvP** | Office plateaus; ramp connectivity critical |

---

## Section 6 — Material rules

Materials are **primary player-readable loot and threat signals**, not cosmetic noise.

### Tier definitions

| Tier | Read | Loot association |
|------|------|------------------|
| **Wood** | Rural, decaying, starter | Basic survival |
| **Stone** | Civic, industrial, sturdy | Mid-tier crafting |
| **Metal** | Military, urban, high-tech | High-tier, radios, dense crates |

### Finalized type → material eligibility

**WOOD (primary or common):**

- House  
- Cabin  
- Barn  
- Store  
- Ruin  

**STONE (primary or common):**

- House  
- Apartment Block  
- Warehouse  
- Factory  
- Office Building  
- Ruin  
- Store  

**METAL (primary or common):**

- Skyscraper  
- Military Complex  
- Apartment Tower  
- Office Building  
- Warehouse  
- Factory  
- Radio Outpost  

### Pure-tier types (never roll other materials)

| Material | Types |
|----------|-------|
| **Wood only** | Cabin, Barn |
| **Stone only** | Apartment Block |
| **Metal only** | Skyscraper, Military Complex, Apartment Tower, Radio Outpost |

### Overlap types (weighted by context)

House, Ruin, Store, Warehouse, Factory, and Office Building may roll between allowed tiers. **Settlement context biases weights:**

| Context | Bias |
|---------|------|
| **City core** | Metal ↑, wood ↓ |
| **City mid/outer** | Stone/metal mixed |
| **Towns** | Wood ↑, metal ↓ |
| **Isolated** | Wood ↑, metal ↓ |

### Readability goals

- **Silhouette + material** identifies building role at distance (barn = wood box; tower = metal spike).  
- **Progression readability** — players learn: metal sites are dangerous and rewarding.  
- **Do not** randomize materials without type constraints — breaks learning.

---

## Section 7 — Tile blueprint system

### Authoring model

Buildings are defined as **stacked 2D layers** (one layer per story). Rows are authored **north → south** in text; parser maps to grid **Y=0 as south** (door edge convention).

### Tile legend (canonical)

| Token | Meaning |
|-------|---------|
| `.` | Empty / void |
| `F` | Floor cell (occupied slab) |
| `F0`–`F9` | Floor with optional room id for future room systems |
| `O` | Opening (no floor — shaft / headroom / void) |
| `R_N` `R_S` `R_E` `R_W` | Ramp rising toward north/south/east/west |
| `Door_N` `Door_S` `Door_E` `Door_W` | Primary door on perimeter edge |
| `Window_N` … `Window_W` | Window on perimeter edge |

Combined tokens use `:` (e.g. `[F:Door_S]`, `[F:R_N]`).

### Perimeter behavior

- Any occupied floor edge without a neighbor cell spawns a **perimeter wall** (or door/window piece from facade rules).  
- **No separate corner piece** — corners are two perpendicular walls.  
- **No half-wall module** — full wall height per story.

### Roof behavior

- Top occupied story receives a **roof cap** (foundation slab at roof level), not a pitched roof kit.  
- Future pitched roofs are an expansion (Section 13).

### Auto wall generation

After blueprint compile:

1. **Interior walls** are auto-generated from layout rules (`ThornsProcTileAutoWalls`) — dividers between cells, not hand-placed in every blueprint.  
2. **Facade** maps doors/windows to spawn defs.  
3. **Validation** enforces connectivity, support, and ramp rules.

### Ramps and openings

- Each ramp occupies **one floor cell** on its story.  
- Ramp direction defines ascent vector on the next story.  
- **Openings (`O`)** mark cells that must not receive walkable floor above shafts.

### TWO-TILE RAMP HEADROOM RULE (mandatory)

Every ramp **must** have **exactly two opening cells** on the story **directly above** the ramp:

1. **Shaft cell** — same grid coordinates as the ramp below.  
2. **Headroom cell** — one cell in the **rise direction** (north/south/east/west from ramp arrow).

```
Story N:     [R_N]  → rises north
Story N+1:   [O] at ramp cell AND [O] at cell north of ramp
```

**Why:** Walkable volume must clear the sloped ramp mesh; a single opening causes collision and connectivity failures. Blueprint authors and validators **must reject** layouts that violate this rule.

### Procedural interior walls

- Generated after floor occupancy is known.  
- Must **preserve door-to-all-walkable connectivity** (flood-fill from primary door).  
- Subject to per-story wall budget caps in validation rules.  
- Future room identity may tag walls with loot/prop tables (Section 13).

### Compile / validation pipeline (conceptual)

```
Blueprint layers → occupancy + openings + ramps
  → ramp headroom injection (two-tile rule)
  → layout + auto interior walls + facade
  → primary door assignment
  → connectivity + structural validation
  → (settlement) accept or fallback archetype rect
```

---

## Section 8 — Roads / world connectivity

### Design intent

Roads and trails are **navigation fiction** — they teach the map without urbanizing it.

### Required topology

| Link | Type | Priority |
|------|------|----------|
| City → each of 3 towns | **Dirt road** | Mandatory |
| Town ↔ town (optional) | **Trail** | Probabilistic (~65% chance one link) |
| City/town → isolated sites | **None** | Wilderness only |

### What roads should do

- **Guide movement** — players who follow roads hit towns predictably  
- **Improve readability** — “I am on the city–town artery”  
- **Create natural PvP funnels** — predictable approaches, not safe highways  

### What roads must not do

- Cover the map in a grid  
- Feel like a modern highway network  
- Replace wilderness tension  
- Spawn isolated POIs along every path  

### Road implementation tiers

| Tier | Description | Status |
|------|-------------|--------|
| **Plan data** | Segment list in settlement plan | Required |
| **Debug viz** | Overlay lines for validation | Required |
| **Terrain/decal** | Mesh tint, decal, or flattened strip | Target spec — must conform to hub apron rules |
| **Door facing** | Primary doors biased toward road vector | Future enhancement |

### Other connectivity layers (world feel)

These may complement roads but **do not replace** sparse building rules:

- **Rivers** — natural valley funnels (terrain-authored)  
- **Power lines** — visual pointers toward city/industry (future)  
- **Terrain valleys** — macro noise guides travel without explicit roads  

---

## Section 9 — Terrain relationships

### Core rules

| Rule | Specification |
|------|----------------|
| **Footprint flattening** | All settlement buildings **must** flatten terrain under the **full rotated footprint** plus wall apron — never center-point only |
| **Shave, don’t raise** | Pads lower hills into buildings; avoid raising terrain into walls |
| **Slope limits** | Footprint corners must pass max height delta (stricter in wilderness than city/town) |
| **Water** | Buildings reject placements below accessible water threshold (relaxed in city/town hubs) |
| **Hub zones** | City/towns share a plateau envelope; per-building pads refine local height |

### Believable placement bias (design targets)

| Type | Preferred terrain context |
|------|---------------------------|
| **Cabin / Barn** | Forest edges, lakes, rural rolls |
| **Military** | Elevated, open sightlines |
| **Towns** | Flatter scoring sites on hub orbit |
| **City** | Central plains / low-relief basin near map center |
| **Isolated** | Away from hubs; memorable terrain features |

Biome **hard gates** (only spawn X in forest) are future expansion — bias scoring is the v1 approach.

### Apron / cliff prevention

Hub edges must use **wide radial blend** (smoothstep, ≥~400–500 m transition) so coarse heightfields do not create **vertical cliff rings** around cities.

---

## Section 10 — Gameplay flow

### Travel loop

```
Long quiet traversal
  → occasional landmark (isolated or distant silhouette)
  → tension rising near hub
  → scavenging / combat / extraction
  → vulnerable exfil across open ground
  → long quiet traversal
```

### City

- **Highest danger** — player concentration and loot value  
- **Strongest PvP** — verticality, sightlines, multiple entries  
- **Strongest loot** — tier bias + crate count  

### Towns

- **Smaller scavenging loops** — faster clears, less vertical  
- **Temporary gathering** — trade, team meetups, short-term bases nearby  
- **Stepping stones** between wilderness and city  

### Isolated structures

- **Exploration rewards** — off-road discovery  
- **Ambush opportunities** — single structure, predictable approach paths  
- **Memorable stories** — “the lone military compound east of town B”  

### Emotional target (locked)

> **Lonely and vulnerable while traveling; tense and exposed when committing to a settlement; brief relief when leaving with loot — never comfortable for long.**

---

## Section 11 — Performance philosophy

### Prioritize

- **Modularity** — tiles → buildings → settlements → world  
- **Readability** — debug viz and validation over blind generation  
- **Scalable generation** — deterministic seed, replicated spec, minimal join payload  
- **Low architectural complexity** — fixed manifests beat unbounded proc city growth  
- **Sparse density** — ~30 major structures, not hundreds  

### Avoid

- Giant procedural cities with hundreds of networked pieces per hub  
- Excessive networked props per building without LOD/streaming plan  
- Simulation complexity unrelated to player experience (e.g. per-room AI schedules at world gen)  
- Raising structure budget without streaming strategy  

### Current scale guidance

| Element | Budget philosophy |
|---------|-------------------|
| Proc buildings | ~30 / world |
| Pieces per building | Many (walls per cell per story) — acceptable at sparse macro density |
| Terrain | Single heightfield chunk — acceptable at 32k with 512² |
| Interiors | No separate unload pass — entire building always loaded when present |

Future streaming (Section 13) must not break hierarchy — **stream chunks of buildings**, not reimplement placement at leaf level without settlement context.

---

## Section 12 — Debugging / validation

World generation is **invalid without visualization**. The following are **required** for authors and engineers.

### Macro settlement

| Tool | Purpose |
|------|---------|
| City bounds / radius | Hub flattening and ring placement |
| Town bounds | Orbit and separation |
| Trail/road overlay | Topology vs terrain |
| Building density heatmap | Confirm ~30 POIs, not drift toward spam |
| Isolated sites + clearance disc | Wilderness spacing |

### Building compile

| Tool | Purpose |
|------|---------|
| Overlap validation | OBB rejection reasons |
| Unreachable tiles | Flood-fill from door |
| Disconnected floors | Multi-story connectivity |
| Blocked ramps | Two-tile headroom violations |
| Invalid openings | Shaft vs floor conflicts |
| Ramp opening mismatch list | Per-story coordinates |

### Runtime / QA

| Check | Pass criteria |
|-------|----------------|
| Terrain vs foundation | No mesh clipping through floors/walls on full footprint |
| Door enterability | Primary door not on ramp shaft |
| Loot scatter | Crates not inside blocked cells |
| Minimap POI | City/town/isolated categories correct |

### Logging (host)

Settlement spawn should report: `city=X/12`, `towns=Y/15`, `isolated=3/3`, seed, hub radii — for regression tracking.

---

## Section 13 — Future expansion

The following are **explicitly allowed** if they respect the hierarchy (Section 2) and do not inflate density without a design revision.

| System | Notes |
|--------|--------|
| Biome-specific settlements | Region layer drives weights, not new placement layer |
| Faction zones | Overlay on regions; manifests still settlement-owned |
| Room identity generation | Room layer; loot tables reference room id |
| Advanced interiors | Props, furniture, lighting — still tile-derived footprints |
| Runtime destruction | Building layer; ruins today are spawn-time only |
| Road decals / terrain mesh strips | World-flow layer; must use apron rules |
| Streamed city chunks | Settlement/building streaming; same manifests |
| Environmental storytelling | Static decor scatter keyed to building type |
| Door → road facing | Settlement placement bias, 90° snaps |
| Power lines / rivers as guides | Region/world-flow, not building logic |

### Compatibility rule

New features **attach to a layer** in the hierarchy. They **must not**:

- Place buildings without settlement context  
- Author tiles without blueprint validation  
- Flatten terrain without footprint-aware pads  
- Increase POI count without updating Section 3–4 budgets  

---

## Appendix A — Main city manifest (locked)

**Outer ring:** House, House, Factory, Warehouse  
**Mid ring:** Store, Store, Warehouse, Apartment, Apartment  
**Core:** Office Building, Apartment Tower, Skyscraper  

Placement order: outer → mid → core (large footprints first within ring).

---

## Appendix B — Town manifests (locked)

| Town | Buildings |
|------|-----------|
| **A** | House, House, Store, Warehouse, Barn |
| **B** | House, House, Cabin, Store, Warehouse |
| **C** | House, House, House, Store, Ruin |

---

## Appendix C — Isolated pool (locked)

Per seed, assign three distinct types from:

- Military Complex  
- Cabin  
- Barn  
- Ruin  
- Radio Outpost  

---

## Appendix D — Document maintenance

| Action | Requirement |
|--------|-------------|
| Change world size, cell size, or 30-POI budget | Update Sections 3–4 + audit code |
| Add building type | Update Sections 5–6 + blueprint library + registry |
| Change material rules | Update Section 6 + affinity tables |
| Add road type | Update Section 8 |
| Relax validation | Update Section 7 + document why |

**Owner:** World generation / technical design  
**Review:** Required before major proc-gen refactors or milestone locks  

---

*End of specification.*
