# THORNS_EVERYTHING_DOCUMENT

Single source of truth for rebuilding **Thorns** in another engine. All gameplay and systems below are inferred from the shipping Thorns codebase (configs, tuning, server authority). No implementation code.

---

## 1. GAME OVERVIEW

- **Genre:** Open-world survival / extraction-adjacent sandbox with base building, hostile humans and wildlife, gear progression, and session-based world persistence options.

- **Core loop**
  - **~30 seconds:** Move, scan for resources or threats, harvest one node or open one container, manage hotbar, react to weather or stamina.
  - **~5 minutes:** Craft or place something meaningful (campfire, chest, wall), complete a short loot trip, push a milestone, or survive a raid warning window.
  - **~1 hour:** Establish or reinforce a base (foundations, walls, core, bed), accumulate rolled weapons/armor and materials, engage raids and dynamic world events, progress XP and upgrade categories, tame and deploy followers.

- **Player fantasy:** Survive a hostile island where nature, bandits, military, and timed raids pressure you; outsmart threats, build a defensible foothold, lose gear on death but keep character progression partially; graduate from naked gatherer to armed builder with tames and a defined “home.”

- **Key pillars:** Server-truth combat and inventory; **full gear loss on death** into a recoverable world container; hunger/thirst/cold/poison layering; **Base Core** as the anchor for raid cycles; procedural-feel pressure via **bandit spawns**, **wildlife ecosystem**, **dynamic supply events**, and **weather**; **rolled** weapons and armor; **taming and mounts**; **radio economy** and **outpost bounties**.

- **Intended emotional experience:** Constant low-grade tension (vitals, weather, noise), spikes during combat and raids, relief at campfires and comfort zones, risk spike on death (body recovery), satisfaction from crafting tier-ups and milestone clears.

---

## 2. DESIGN PHILOSOPHY

- **Why it is designed this way:** Survival timers force movement and planning; cold and Thorn-adjacent zones punish AFK in the wrong place; death drops create stakes without deleting the character; Base Core + raids give a clear “why build”; separate AI stacks (wildlife vs human NPCs) allow different fantasy and tuning.

- **Prioritized:** **Gear and positioning** over twitch aim (hitscan, headshot multiplier); **preparation** (food, water, warmth, ammo) over raw DPS; **server validation** for combat and inventory; **readable tuning** centralized in numeric tables.

- **What should NEVER change:** Authoritative combat and inventory; death **full strip** of carry + equipped armor into a **single recoverable crate** when anything would drop; XP **banked per level** survives but **progress within the current level** resets on death; armor **DR cap**; raid waves anchored to the owner’s **Base Core**; taming gated by **health threshold** upgradeable via meta.

---

## 3. CORE GAMEPLAY RULES

**Combat**

- Damage application is **host-validated**. Clients may send aim intent; the host performs hit tests and applies damage.
- **Hitscan** per trigger pull: one logical ray for standard firearms; **shotguns** fire multiple hitscans in a configurable cone (each pellet can hit; duplicates on one target allowed to aggregate).
- **Headshot:** **×3** damage vs players; NPC headshots use defined hit-zone rules (head mesh / zone tests).
- **Fire rate:** Enforced on host (minimum interval between shots from weapon definition).
- **Range:** Client-claimed range is clamped to the weapon’s maximum range.
- **Origin sanity:** Firing origin must lie within a small tolerance of the character’s validated camera/head reference (anti-cheat intent).
- **Melee weapons:** Use the same hit pipeline where configured as non-consuming “ammo” (clip size used for sync only); optional suppression of bullet-style impact effects.
- **Crouch:** Halves shotgun spread half-angle (tactical reward).
- **Weapon durability:** Each shot can consume durability on the equipped inventory row; weapons can become **broken** with explicit notify state.
- **Armor:** Three slots (helmet, chest, pants). Each piece contributes **percent damage reduction**; piece tier scales reduction; **total reduction capped at 75%**; incoming damage after reduction is rounded up to an integer.
- **TTK:** Driven per weapon by damage, fire mode (semi/auto), range, and armor—no fixed global TTK; sniper-class weapons have high per-shot damage and long reload.

**Death penalties**

- On death: **entire inventory** is serialized into one **death crate** at death position (if any items or equipped armor existed); then inventory slots are **cleared** and equipped armor **stripped**.
- Death crate: **hold-to-loot** interaction, limited activation distance; partial looting supported (crate updates until empty); crate **despawns after a long timer** (multiple deaths = multiple independent crates).
- **XP:** Current **character level is kept**, but **XP progress toward the next level is reset** to the start of the current level (no full level wipe on death).
- **Killer reward:** If another **player** scored the kill, the killer gains **750 XP** and PvP-related quest hooks may fire.

**Progression**

- **Character XP** curves upward quadratically by level (`50 * L * (L-1)` cumulative to enter level L).
- **Upgrade points** are spent in **fixed categories** (mining, woodcutting, hunger max, thirst max, stamina max %, taming threshold, crafting tier, stealth detection multiplier). Each category has **max level** and **cost = baseCost × (1 + currentLevel)** for the next tier.
- **Milestones:** Ordered goals (collect, build, kill animals/bandits, tame species) with XP rewards; prerequisites default to “previous row” unless overridden.

**Loot**

- World containers (including hand-placed and event crates) use **weighted tables**; respawn timers configurable (default baked inherits global loot respawn unless overridden).
- Airdrop / convoy style events: bundles with **multiple loot rolls** (weapons + armor tier profiles), defenders spawned with archetypes, hold-to-open timers on bundles.
- Ground drops from harvest/death use pickup distance rules; item metadata preserves **weapon rolls** and **armor rolls** where applicable.

**Raiding**

- Raids are **per player**, centered on that player’s **placed Base Core** (one core per player enforced at structure level).
- **Primary trigger:** Real-time **online timer** while the player has a valid Base Core (virtual “days” optional legacy path exists but default is realtime cycle).
- **Warning phase:** Long prep (production) or short (debug / short-cycle config)—then sequential **waves** mixing **wildlife species**, **military humanoids**, and special units (e.g. **mutant wolf** with buffs / drop bias).
- Wave pacing: configurable delays between waves; spawn radius band around the core.

**PvP expectations**

- PvP is **opt-in by presence** (players can damage each other with the same weapon rules); kill feed broadcasts killer/victim/method; kill confirm feedback to killer. No separate “safe zone” flag described in core rules—rely on map and social pressure unless a POI adds rules elsewhere.

---

## 4. CORE LOOP BREAKDOWN

### 30 Second Loop

Orient with map/minimap; sprint or crouch under stamina rules; harvest within reach (reach shorter when certain tools equipped); drink/fill water; react to snow without warmth; avoid or exploit Thorn bloom radius; engage or disengage one combat encounter; reload; one loot pickup.

### 5 Minute Loop

Craft consumables or placeables; start or finish a campfire cook; place or upgrade one building piece; clear a nearby POI crate; push milestone progress; hear raid warning and reposition; shop or sell at a **Radio Station**; respond to **dynamic event** beacon (airdrop/convoy/mutation).

### 1 Hour Loop

Grid-base layout with upgrades (wood → stone → metal paths); secure **bed** respawn and **chest** storage; survive or intentionally trigger **raid waves**; hunt bandits for milestones; tame multiple species, assign follow/stay, mount; bank XP and upgrades; explore for rolled gear and gold; complete outpost **bounties** (mutation showcase, PvP bounty, boss spawn where authored).

---

## 5. SYSTEMS ARCHITECTURE

### Core Gameplay

**System:** Character / life state  
**Purpose:** Per-player logical body: inventory reference, equipped weapon state, equipped armor, weapon durability state map.  
**Inputs:** Join/leave, equip requests, damage events, death, persistence import/export.  
**Outputs:** Validated equip, armor DR query, armor export for death crate.  
**Rules:** Armor ticks down slowly while worn until broken and removed; armor slots are strict item allowlists.  
**Dependencies:** Item registry, armor config, persistence.

**System:** Player vitals  
**Purpose:** Health, hunger, thirst, stamina, poison stack, environmental flags, sprint/crouch flags, raid cycle timer.  
**Inputs:** Time delta, weather key, campfire proximity, Thorn bloom proximity, sprint/crouch from client intent, consumable effects.  
**Outputs:** Replicated stat snapshot, death when health 0, cold/bloom/comfort HUD signals.  
**Rules:** Hunger ~30 min to empty, thirst ~15 min at base rates; regen only if both hunger and thirst positive; starvation damages health; snow without **lit** nearby campfire multiplies hunger/thirst drain and stamina stress; Thorn bloom increases drain and walk speed; long continuous stay in bloom applies damage per second; comfort near own core or lit campfire reduces drain multipliers; stamina drains while sprinting and moving, regens when not.  
**Dependencies:** Campfire system, weather, building (core position), optional tame mount dismount on death.

**System:** Spawn & respawn  
**Purpose:** Place players at world-ready; handle bed vs random vs last saved position policy.  
**Inputs:** Persistence pending teleport, building bed lookup, world ready gate.  
**Outputs:** Spawned character at valid point; optional spawn safety window attribute.  
**Rules:** Manual respawn after death UI; rate-limit respawn requests; bed overrides random when set.  
**Dependencies:** Persistence, building, world state.

**System:** Harvest & resources  
**Purpose:** Validate distance to static resource instances, grant materials with upgrade multipliers, respawn depleted nodes.  
**Inputs:** Node id, player position, tool in hotbar (reach multiplier).  
**Outputs:** Inventory deltas, feedback events, node cooldown state.  
**Rules:** Two reach bands—dense hand-placed vs default; axe/pickaxe in selected slot reduces reach relative to fist/torch.  
**Dependencies:** Inventory, tuning, milestone recorder.

**System:** Crafting  
**Purpose:** Convert materials to items per recipe registry respecting crafting upgrade tier.  
**Inputs:** Recipe id, player inventory snapshot.  
**Outputs:** Success/fail, inventory delta.  
**Dependencies:** Item registry, player upgrades.

**System:** Building  
**Purpose:** Place, upgrade, remove structures; grid snap for foundations/walls; free-place for beds/chests/torches/etc.  
**Inputs:** Structure type, pose, surface hit, owner id.  
**Outputs:** World structure instances, collision rules, raid eligibility (Base Core).  
**Rules:** Costs from structure defs; upgrade paths (wood → stone → metal) for some pieces; removing structure may flush chest contents to owner inventory or ground.  
**Dependencies:** Inventory, storage, persistence, item drops.

**System:** Storage (chest)  
**Purpose:** Grid storage separate from backpack for placed chests.  
**Inputs:** Open, move item between player and chest.  
**Outputs:** Authoritative chest contents.  
**Dependencies:** Building, persistence.

**System:** Doors  
**Purpose:** Interactive doors tied to building frames.  
**Inputs:** Use/interact.  
**Outputs:** Open state.  
**Dependencies:** Building.

**System:** Placeables (campfire, torch, C4, planter, bed)  
**Purpose:** Special placement modes with validation and ongoing simulation (fire, fuel, cook progress, blast).  
**Inputs:** Placement commands, time, interactions.  
**Outputs:** World objects, light/warmth queries for vitals, farming plots.  
**Dependencies:** Building overlap rules, inventory, vitals.

**System:** Farming  
**Purpose:** Seeds in planters, growth timers, harvest.  
**Inputs:** Plant/harvest requests.  
**Outputs:** Crop state, items.  
**Dependencies:** Planters, inventory.

### Combat

**System:** Weapons host  
**Purpose:** Fire, reload, equip, ammo sync, attachment apply, durability, broken state.  
**Inputs:** Fire payload (weapon id, instance id, origin, direction), reload requests.  
**Outputs:** Hit results, damage to targets, feedback payloads, ammo updates.  
**Rules:** As in Section 3; rate-limit fire events; aggregate multi-pellet damage per target per tick for feedback.  
**Dependencies:** Character inventory, player vitals, wildlife, bandits, optional combat feedback bus.

**System:** Loot (containers)  
**Purpose:** Interactable crates with rolls and respawn timers.  
**Inputs:** Open/use, world time.  
**Outputs:** Items to ground or player per rules.  
**Dependencies:** Loot tables, milestones.

**System:** Item drops (ground + death)  
**Purpose:** Physical pickups; death crate creation and partial drain.  
**Inputs:** Death event, spawn drop requests.  
**Outputs:** World drop entities, inventory updates.  
**Dependencies:** Character, persistence.

### AI

**System:** Wildlife simulation  
**Purpose:** Ambient and combat creatures with species traits, time-of-day activity, packs, taming, downed/revive/feed loops.  
**Inputs:** Spawner budget, player proximity LOD, damage events, tame commands.  
**Outputs:** Movement, attacks, drops, tame state.  
**Rules:** State machine includes Idle, Wander, Flee, Hunt, Attack, ReturnToLeash, Downed, Stunned, FollowOwner, AssistCombat, TameStay; hunt commitment timers; flee contagion among nearby passives; LOD throttles tick rate by distance to nearest relevant player (owner for tames); global and per-player population caps; rare cryptid spawn weighting day/night.  
**Dependencies:** Path/movement, player stats, item drops, tuning.

**System:** Bandits / military  
**Purpose:** Humanoid AI factions with loadouts; patrol and combat toward players; raid wave participation.  
**Inputs:** Spawn surface heuristics (wilderness vs POI size), player positions.  
**Outputs:** Damage, death, loot.  
**Dependencies:** Weapon/ballistics for NPC weapons if applicable, POI system.

**System:** Block / sandbox NPCs  
**Purpose:** Separate lightweight or scripted NPC archetype (non-wildlife stack) for specific encounters.  
**Inputs:** Spawn bootstrap, encounter rules.  
**Outputs:** Interaction and combat as configured.  
**Dependencies:** Encounter configs.

### World

**System:** World readiness  
**Purpose:** Gate players until static/baked world and critical subsystems are safe to enter.  
**Inputs:** Boot completion, optional generation progress.  
**Outputs:** “Ready” event to systems and clients.  
**Dependencies:** Map/POI registration.

**System:** POI (static)  
**Purpose:** Register map points of interest for map UI, spawners, and bounds.  
**Inputs:** Authored world markers.  
**Outputs:** Map replication payload.  
**Dependencies:** None critical.

**System:** Dynamic POI / events  
**Purpose:** Timed spawns of airdrops, convoys, mutation bosses in rolled world positions with defenders and timed crate locks.  
**Inputs:** Interval timers, debug fast cadence flag.  
**Outputs:** Event instances, broadcast to clients.  
**Dependencies:** Bandit archetypes, loot profiles, wildlife for bosses.

**System:** Time of day  
**Purpose:** Drive lighting and wildlife diurnal weights.  
**Inputs:** Clock speed / curve.  
**Outputs:** Phase to systems.  
**Dependencies:** Lighting (client presentation).

**System:** Weather  
**Purpose:** Select weather state; feed vitals cold rule and presentation.  
**Inputs:** Timers / transitions.  
**Outputs:** Weather key (e.g. clear vs snow).  
**Dependencies:** Player vitals, effects.

**System:** World cycle events  
**Purpose:** Cross-cutting hooks on day/cycle boundaries (e.g. bloom-related world hooks).  
**Inputs:** Time systems.  
**Outputs:** Event bus signals.  
**Dependencies:** Time/weather.

### UI (logical)

**System:** HUD vitals & status  
**Purpose:** Show health, hunger, thirst, stamina, cold, comfort, poison, raid timer.  
**Inputs:** Replicated stats.  
**Outputs:** Presentation only.

**System:** Inventory & hotbar  
**Purpose:** 8 hotbar + 30 backpack cells; drag/move; drop quantity.  
**Inputs:** User commands.  
**Outputs:** Requests to host.  
**Dependencies:** Host inventory authority.

**System:** Map / minimap  
**Purpose:** Full map and corner minimap from static image + POI-derived bounds when unset.  
**Inputs:** Map fetch RPC, player position.  
**Outputs:** UI.  
**Dependencies:** POI data.

**System:** Main menu & settings  
**Purpose:** Pause shell, controls remapping persistence, server browser entry from lobby or in-game panel.  
**Inputs:** Keybind save/load.  
**Outputs:** Settings state.  
**Dependencies:** Persistence for keybinds (cross-session).

**System:** Milestones UI  
**Purpose:** Pin goals, show progress, celebrate completion.  
**Inputs:** Milestone RPCs/events.  
**Dependencies:** Milestone host.

**System:** Tame UI  
**Purpose:** Manage tames, follow/stay, naming, mount where valid.  
**Inputs:** Tame RPCs.  
**Dependencies:** Tame host, wildlife.

**System:** Radio shop UI  
**Purpose:** Buy/sell rotating catalog at stations; hold-to-open.  
**Inputs:** Transactions validated by distance and gold.  
**Dependencies:** Item economy, inventory.

**System:** Combat feedback  
**Purpose:** Tracers, hit markers, damage numbers, kill confirm, remote gunshot audio attenuation by distance.  
**Inputs:** Host events.  
**Outputs:** Client-only presentation within trust boundaries.

### Networking (abstract)

**System:** Rate limiting  
**Purpose:** Throttle abuse-prone actions (fire, respawn, drops).  
**Inputs:** Player id, action key.  
**Outputs:** Allow/deny.  
**Dependencies:** All gated remotes.

**System:** Replication streams  
**Purpose:** Inventory snapshot sequencing; stat snapshots ~low Hz; storage open sessions.  
**Inputs:** Dirty flags on change.  
**Outputs:** Delta or full snapshots to owning client.  
**Dependencies:** Persistence dirty markers.

### Persistence

**System:** World + player save  
**Purpose:** Autosave on interval; per-world vs per-session keying policy; wipe id for full reset.  
**Inputs:** Dirty world, dirty players.  
**Outputs:** Serialized buildings, chests, tames, player position, inventory, stats, upgrades, milestone progress.  
**Rules:** New players: empty inventory unless dev grant; optional resume at last saved position join policy.  
**Dependencies:** All dirty producers.

**System:** Server selection / lobby  
**Purpose:** Optional separate lobby experience: pick official or private server, join by code, teleport into main world.  
**Inputs:** Browser RPCs.  
**Outputs:** Teleport intent with payload.  
**Dependencies:** Platform matchmaking / reserved slots (original used platform-specific teleport data).

---

## 6. DATA MODELS

**Player (logical)**

- Identity, display name.
- **Vitals:** health, hunger, thirst, stamina, max variants (upgrades inflate hunger/thirst/stamina caps).
- **XP** total, derived **level**, **upgrade points**, per-category **upgrade levels**.
- **Poison:** remaining environmental or consumable damage to resolve over time.
- **Flags:** sprinting, crouching, cold stress, Thorn bloom, comfort, bloom overstay timer, raid cycle seconds remaining.
- **Last damage source:** Kind (player / wildlife / bandit / environmental), reference ids for kill attribution and feed.

**Weapon (definition + instance)**

- **Definition:** id, display name, damage, fire rate, fire mode (semi/auto), clip size, reload time, range, ammo type id, optional shotgun pellet count and cone half-angle, optional bow draw time, optional melee flags, attachment slot list, optional ADS/zoom parameters (presentation).
- **Instance (per inventory row):** `weaponInstanceId` (stable id for state), **loaded ammo**, optional **weaponRoll** (affixes affecting merged stats), durability; **weaponStates** map keyed by instance or slot for firing validation.

**Armor piece**

- Slot: helmet | chest | pants (plus special headlamp slot rules).
- **itemId**, **durability**, optional **armorRoll** (tier scales DR multiplier).

**Item (stack)**

- **itemId**, **quantity**, optional **durability** (tools/weapons/armor), optional **weaponRoll** / **armorRoll**, optional **weaponInstanceId** for equipped weapon binding.

**Inventory**

- Fixed **38 slots**: **8** hotbar + **30** backpack in a **6×5** grid presentation order.
- Each slot: item id, quantity, durability, weapon meta, instance id as applicable.
- Operations: add with overflow leftover, remove from slot, move/swap, apply durability damage to equipped weapon row, snapshot for UI and death export.

**AI entity (wildlife)**

- Species key, traits/perks roll, health, state machine state, leash/home, tame owner user id, mount rider binding, LOD tier, attack targets, downed/stun timers, optional fish/deer/wolf/bear/panther/eagle-specific tuning from registry.

**AI entity (bandit / military)**

- Archetype key, faction, bandit id for hit routing, weapon/loadout references, spawn context (POI vs flat ground).

**World object (building)**

- Structure type, owner user id, grid cell key for snapped pieces, transform, health, upgrade tier linkage, door/chest linkage ids as needed.

**Death crate**

- Position, JSON or equivalent payload of all lost stacks + armor export, despawn deadline, display label.

**Dynamic event instance**

- Event type (airdrop, convoy, mutation), center, defender count rolled, crate entities, expiry time.

---

## 7. COMBAT SYSTEM

- **Shooting:** Each shot is a **instant hitscan** from validated origin along aim direction out to **min(client range claim, weapon max range)**. Automatic weapons repeat on hold subject to server fire interval.
- **Damage model:** Base per-pellet or per-shot from merged definition + affixes; **headshot multiplier** for players; NPC zones for head; apply **armor reduction** with **75% cap**; round; subtract from target health pool (players use vitals health; NPCs use their health).
- **Weapon differences:** Damage, fire rate, semi vs auto, clip, reload time, range, ammo type, shotgun vs single-ray, melee vs ranged, attachment slots affecting merged stats (sights/barrels/mags/grips per weapon table).
- **Spread / recoil:** **Shotgun** cone half-angle from data; **halved while crouched**. **Hipfire cone** for non-ADS shooting is defined for presentation on client; **server trust** is primarily ray + origin validation—not full recoil simulation on host for every rifle kick (recoil numeric fields exist for UX / future tightening).
- **Ammo:** Decrement per shot except melee-infinite class; reload fills to clip capped by carried ammo type in inventory; sync ammo count to owner; **broken** state when durability exhausted.

---

## 8. AI / WILDLIFE SYSTEM

- **Behavior states:** Idle, Wander, Flee, Hunt, Attack, ReturnToLeash, Downed, Stunned, FollowOwner, AssistCombat, TameStay—plus water navigation multipliers.
- **Aggro / combat intent:** Predators hunt/chase in commitment windows; pack members may escalate when peers enter combat; prey flees; tames assist owner against threats within a time window; minimum assist damage for zero-damage species when defending.
- **Movement intent:** Throttled ticks (sub-second), LOD reduces work when far; separation from neighbors within a small radius; facing smoothing tighter in combat; leash return when leaving allowed territory.
- **Taming:** Stun window, hold-to-tame duration, health must be below **base 10%** improved by **taming upgrade** (+5% per level up to high cap); feed heals percent HP on cooldown; revive from downed; max following count **3** (large tame cap exists but follow limited).
- **Scaling difficulty:** Population caps global and per-player radius; raid waves scale counts and radii; dynamic events add military defenders; bandit spawn weights favor POIs and pavement military patrol targets.

---

## 9. WORLD DESIGN

- **Map structure:** Large single open map (~7700×7700 world units in config comments) with **baked** terrain and authored POIs; shoreline bias for random spawn when no bed.
- **POI system:** Static markers drive map bounds inference, spawn weights for bandits, and UI.
- **Loot distribution:** Hand-placed containers with category tables; optional debug mode forcing weapon-only rolls in crates; timed respawn.
- **Event system:** Periodic dynamic spawns (airdrop, convoy, mutation boss pool wolf/bear/panther) with beacon height, world min/max placement square, bundle loot slot count, defender counts, hold-to-open duration on bundles, event lifetime duration.

---

## 10. PROGRESSION SYSTEM

- **XP sources:** Milestones, kills (PvP chunk), quests, activities recorded by subsystems (collect/build/kill).
- **Leveling rules:** Total XP maps to discrete level via monotonic curve; death resets only **intra-level** progress.
- **Risk/reward:** Death drops all carried gear and worn armor to world crate; poison and starvation punish poor prep; raids risk base vicinity; dynamic events attract PvP and PvE.
- **Persists:** Inventory (when alive), buildings, chest contents, tame roster, stats, upgrades, milestones, spawn/bed bindings, last position (policy-controlled), keybind overrides.
- **Does not persist across wipe id change:** Everything under saved namespaces resets when wipe key increments.

---

## 11. INVENTORY & ITEMS

- **Structure:** 38 slots in two logical regions; hotbar mirrors first 8 for quick select.
- **Item types:** Resources, consumables (food, water clean/dirty, medical), ammo per caliber, weapons, armor, tools, building pieces, seeds, attachments, explosives, torches, placeable kits.
- **Stack rules:** Per **itemId** max stack from registry; weapons/armor typically low stack; resources stack high.
- **Equipment logic:** One active weapon with synced instance state; armor only in valid slots; attachments only if weapon lists slot; equip swaps push durability and roll metadata.

---

## 12. BUILDING & BASE SYSTEM

- **How building works:** Ray-based placement with **grid snap** for 8-unit foundations/walls/ramps/planters; rotation steps for walls; some pieces force vertical orientation; chests/beds/torches/core use free placement with validation.
- **Ownership rules:** Structures record **owner user id**; bed gives **respawn point** for owner; **Base Core** unique per player.
- **Raid interaction:** Raid manager binds waves to the player’s core position while raid active; cancelling if core invalid.
- **Durability:** Weapons lose durability per shot; armor loses slowly over time while worn until removed at zero; structures have **maxHealth** values for combat against them (exact repair loop optional beyond upgrade paths).

---

## 13. NETWORKING MODEL (ABSTRACT)

- **Server authoritative:** Inventory mutations, crafting, building place/upgrade/remove, all damage and death, loot rolls, AI decisions, weather choice, persistence commits, shop transactions, tame state changes, raid phase transitions.
- **Client side:** Camera, presentation animation, local HUD, **non-authoritative** aim preview where allowed, map/minimap rendering, audio attenuation for distant gunshots, foliage fade optimizations.
- **Synchronization:** Owner receives **snapshots** of inventory and stats on change and at low cadence; critical events (death, raid phase, weather) pushed as messages; strangers receive limited combat feedback (e.g. distant shots audio-only beyond a distance band).

---

## 14. PERFORMANCE CONSIDERATIONS

- **Wildlife:** Global cap, per-player nearby cap, LOD tick throttling, separation scan radius cap, staggered spawner ticks, PreSimulation/post-simulation movement reapplication pattern to reduce jitter.
- **Boot:** Staggered start delays (seconds to minutes) between heavy systems (POI, loot, resources, dynamic POI, bandits, wildlife) to spread load spikes.
- **Rendering:** Client-only foliage distance fade with part limits per tree; optional server-side shadow disable on dense trees after interaction.
- **Death / persistence:** Deferred character data teardown on disconnect ordering to avoid saving empty inventory on race.
- **Known improvement areas:** Any single-frame full-world scans are avoided in favor of cached roots where configured; dense forests still stress client GPU without LOD.

---

## 15. DO NOT CARRY OVER (CRITICAL)

- **Platform-specific player APIs, character rigs, humanoid health, workspace raycast filter objects, insert-by-asset-id cloud delivery, default UI widgets, teleport service, data store key APIs, remote event names as code coupling** — replace with engine-native player controllers, collider queries, content mounting, RPC tables, and your storage backend.
- **Roblox-specific unit assumptions** (studs) — re-tune in meters but **preserve ratios** (reach, radii, map size) unless you rescale art.
- **`SpawnLocation` parts, `ProximityPrompt`, `Humanoid`, `LoadCharacter` auto pipeline** — replace with custom spawn controllers and interaction components.
- **Client-trusted ray endpoints without your own anti-cheat** — reimplement origin validation in native physics/query space.
- **InsertService for world props** — use engine content system; keep only the **design intent** (death crate visual, supply crate visual).

---

## 16. REBUILD PRIORITIES

**Phase 1 — Playable shell:** First-person controller, host join, world streaming/static map, spawn points, vitals tick, basic HUD, empty persistence write/read.

**Phase 2 — Survival:** Harvest nodes, inventory + hotbar, crafting recipes, consumables (food/water/clean vs dirty poison), campfires + warmth ring, weather → cold rule.

**Phase 3 — Combat:** Hitscan host pipeline, one rifle + ammo, armor slots + DR cap, death + death crate + respawn choice, melee tool.

**Phase 4 — Building:** Grid snap foundations/walls, doors, chest storage, bed respawn, Base Core placement, remove/upgrade.

**Phase 5 — PvE pressure:** One wildlife species + spawner caps; one bandit archetype + spawner; simple loot crates with respawn.

**Phase 6 — Progression:** XP curve, upgrade points purchase, milestones.

**Phase 7 — Advanced:** Raids, dynamic events, radio shop, outpost quests, tame/mount, attachments, map/minimap, keybind persistence.

---

## 17. MINIMUM VIABLE THORNS

- **Required systems:** Host authority movement + interaction; vitals; harvest + inventory + craft; one weapon + armor reduction; death crate + respawn; campfire + snow cold; persistence of position + inventory + 5–10 structure types; basic AI enemy; static map POIs for map bounds.
- **Required features:** First-person combat feedback; loot crate; bed; one raid wave **or** one dynamic event (not both) to prove loop.
- **Intentionally excluded at MVP:** Full attachment matrix, full species list, cryptid, full radio rotation, full milestone list, mount networking edge cases, cosmetics beyond gear tiers.

---

## 18. RISKS & CHALLENGES

- **Technical:** Replicating mount + tame AI smoothly; hitscan anti-cheat; large open-world physics vs baked mesh; death-crate partial loot concurrency; autosave during combat.
- **Design:** Raid difficulty vs solo players; snow frustration without UI clarity; PvP opt-in clarity.
- **Scope:** Full item + weapon + attachment matrix is huge—data migration must be automated from registries.

---

## 19. FUTURE EXPANSION (OPTIONAL)

- Calendar-day raid trigger vs realtime-only.
- Repair stations and armor repair kits beyond passive decay.
- Faction reputation affecting military aggro.
- Vehicles beyond tames.
- Underground POIs and instanced bunkers.
- Procedural satellite islands (legacy direction was reduced in favor of baked world—could return).

---



BELOW IS A LIST OF BEST PRACTICES. ALWAYS CHECK LOGIC AGAINST THESE BEFORE IMPLEMENTING

🧱 1. NETWORKING FUNDAMENTALS
✅ Golden Rules
Server decides:
health
damage
position (authoritative)
game state
Client handles:
input
camera
prediction (optional early)
🔥 Network Objects
Every gameplay object MUST be:
NetworkSpawn()’d to exist across clients
Ownership matters:
owner = who can control / send updates
⚠️ Huge Gotcha

After spawning:

Adding components later does NOT sync automatically

👉 If you modify hierarchy:

Network.Refresh();
RPC Usage
[Rpc.Broadcast] → everyone
[Rpc.Owner] → specific player
[Rpc.Host] → server only
🧍 2. PLAYER / PAWN ARCHITECTURE
✅ Best Practice Structure
Game
 └── Player (connection)
      └── Pawn (GameObject)
           ├── Movement
           ├── Camera
           ├── Weapon (optional)
🚨 CRITICAL RULE

Each client must ONLY control THEIR pawn

Never:

loop through players and assign input
share camera
guess ownership
✅ Local Pawn Detection

Have ONE source of truth:

public static Pawn Local;

Set it ONCE when player spawns.

🎥 3. CAMERA SYSTEM (MOST COMMON FAILURE POINT)
✅ Rules
Camera exists ONLY for local player
Never use global fallback like:
Scene.Camera for gameplay logic
❌ Don’t Do This
“nearest camera”
“active camera”
“guess camera”
✅ Do This

Attach camera to pawn:

if (IsLocal)
{
    Camera.Enabled = true;
}
else
{
    Camera.Enabled = false;
}
🔫 4. VIEWMODEL vs WORLD MODEL
🧠 Separation is REQUIRED
Type	Who sees it
Viewmodel	Local player ONLY
World model	Everyone ELSE
✅ Viewmodel Rules
Create ONLY on local client
Never network it
Parent to camera
❌ Common Mistakes
spawning viewmodel on server
trying to sync viewmodel
letting other players see it
⚡ 5. RESPONSIVENESS (FEEL)
🎯 Goal

Player feels instant response, even if server decides outcome

Techniques
1. Client-side fire
play sound immediately
animate immediately
2. Server validates hit
3. Optional:
prediction (later)
🧠 6. GAME STATE / ROUND SYSTEM
✅ Use Explicit State Machine
Lobby
→ Starting
→ InGame
→ Ending
→ Reset
❌ Don’t
use booleans like:
isPlaying
isGameActive
✅ Do

Use enum:

enum GameState
{
    Lobby,
    InRound,
    End
}
🧍‍♂️ 7. SPAWNING SYSTEM
✅ Best Practice
Server handles spawn
Use spawn points (don’t hardcode positions)
Always reset:
velocity
rotation
health
⚠️ Important

If teleporting:

Network.ClearInterpolation();

👉 prevents rubberbanding

🌐 8. LOBBIES / SERVERS
Built-in system:
Create:
Networking.CreateLobby(...)
Join:
Networking.Connect(lobbyId);

✅ Best Practice
Separate:
Lobby logic
Game logic
🧠 9. SCENE / MAP ARCHITECTURE
Correct layering:
Hammer (.vmap) → geometry
Scene (.scene) → logic
Best structure:
/MapRoot
/Players
/Systems
/UI
/Spawns
⚙️ 10. COMPONENT DESIGN
✅ Rules
One responsibility per component
No “god scripts”
Example

BAD:

PlayerController.cs (movement + shooting + UI + networking)

GOOD:

PlayerMovement
WeaponSystem
CameraController
HealthComponent
🧠 11. STATE SYNCING
✅ Use [Net] properties for:
health
ammo
state
❌ Don’t spam RPCs for state
⚡ 12. PERFORMANCE
Watch out for:
per-frame loops over all players
heavy raycasts
spawning too many objects
⚠️ Big one

Interpolation is automatic:

don’t fight it unless needed
🎮 13. PLAYER INTERACTION
✅ Rules
Only interact if:
player is alive
correct role
in correct game state
🧠 14. DEBUGGING MULTIPLAYER
MUST DO

Add logs for:

Log.Info($"Client: {Connection.Local}");
Log.Info($"IsOwner: {IsOwner}");
Test Cases
1 player
2 players
join mid-game
disconnect mid-game
💾 15. DATA / PERSISTENCE
Current reality

S&box:

limited external SDKs
often HTTP/WebSocket only
Best practice
keep it simple early
store:
stats
settings
🧠 16. ITERATION WORKFLOW
Use hotload aggressively
change code → instant apply
don’t restart constantly
Best practice
build in slices
test immediately
🚨 17. COMMON FAILURE PATTERNS
❌ Overengineering early

→ (you just experienced this)

❌ Mixing client/server logic

→ causes desync

❌ Guessing ownership

→ breaks multiplayer

❌ Building features before foundation

→ regression spiral

🧠 18. BUILD ORDER (THIS IS GOLD)

Follow THIS always:

1

Movement + multiplayer

2

Camera

3

Combat

4

Game state

5

Polish systems

🚀 19. ADVANCED (LATER)
client prediction
lag compensation
hit validation
interest management
