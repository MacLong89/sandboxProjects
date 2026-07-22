# Catch a Critter! 🥅

A multiplayer creature-collection simulator for s&box, engineered for the Play
Fund: instant onboarding, an infinite progression loop, collection psychology,
and daily comeback hooks.

## Why this game (the data)

- The s&box Play Fund pays on **clamped individual player hours** — breadth of
  returning players matters as much as marathon sessions.
- Every top Play Fund earner (Mow the Lawn ~$2k/mo, S&Business, Wall Destroyer,
  Plinko Clicker, Mine & Factory) is an **idle/progression simulator**: one
  satisfying verb + sell→upgrade loop + prestige + dailies.
- On Roblox, simulators have the **highest revenue per player** of any genre,
  and **pet/creature collection** (Adopt Me, Pet Simulator) is the single
  biggest player-hour magnet — but *nobody on s&box has shipped a
  creature-collection sim*. That's the opening this game takes.

## The loop

1. **Catch** — run around a low-poly island and swing your net at 42 species of
   chibi critters across 7 biomes. Sneak (Ctrl) to get close to rare ones,
   which flee faster and need better nets.
2. **Sell or Keep** — sell your backpack at the hub, or keep critters in your
   **Sanctuary** where they earn passive coins (even offline) and can follow
   you around as buffing pets.
3. **Upgrade** — 12 net tiers, Speed / Backpack / Luck upgrades, and coin-gated
   biome bridges (Whisperwood → Shellshore → Glowdeep → Frostfell → Emberpeak
   → Mythwood).
4. **Breed** — pair same-species critters into eggs on real-time timers with
   2.5x shiny odds; generations stack sanctuary income.
5. **Ascend** — prestige for permanent +35% sell, talent points (3-branch
   talent tree, 18 talents), and an extra sanctuary slot.

## Retention systems

- **Momentum milestones** — a 10-step onboarding chain with burst rewards that
  tours every system (catch → sell → upgrade → keep → follow → unlock → hatch
  → daily → ascend).
- **Daily quests** — 3 per day, deterministic per date, scaled to your unlocked
  biomes; pays coins + gems.
- **Login streak** — escalating gem rewards (2→15), with a talent that
  protects a missed day.
- **Offline earnings** — sanctuary income accrues while away (capped 10h) with
  a welcome-back popup.
- **Shinies** — 1/40 base odds, boosted by Luck, talents, and breeding; 12x
  sell value and golden bodies.
- **Codex** — per-biome completion grants permanent +10% sell each.
- **Multiplayer** — up to 24 players per lobby, individual progression,
  shared world spawns (one player's Luck upgrades benefit the lobby), session
  catch leaderboard, follower pets visible to everyone.

## Play

1. Open `catch_a_critter.sbproj` in the s&box editor.
2. Play `Assets/scenes/critter_isle.scene`.

The world, HUD, camera, and lobby are all created at runtime by
`GameBootstrap` — the scene only carries the sun, skybox, and bootstrap object.

### Controls

- **WASD** move · **Ctrl** sneak · **Space** jump
- **LMB** swing net
- **E** interact (sell / stations / gates)
- **Tab** menu (Shop · Sanctuary · Codex · Daily · Ascend)

## Project layout

```
Code/
  Core/         Balance (all tuning), CritterGame (lobby+UI state), GameBootstrap
  Data/         SpeciesCatalog (42 species), BiomeCatalog, NetCatalog, TalentCatalog
  World/        Kit (primitive props), WorldBuilder (island/gates/stations)
  Critters/     CritterAgent (networked AI), CritterBody (procedural bodies), CritterSpawner
  Player/       CritterPlayer, PlayerCamera, CatchEffects/Sfx
  Progression/  PlayerProgress (save/economy), DailyQuests, Milestones, SaveData
  UI/           Hud.razor, MenuPanel.razor
Assets/
  ui/critters/  42 generated codex icons (true-transparency RGBA)
  ui/logo.png   key art
  sounds/       7 generated wavs
  scenes/       critter_isle.scene
tools/
  generate_assets.py   regenerates all icons + sounds from Species.cs
```

## Regenerating art

```bash
python tools/generate_assets.py
```

Icons are parsed straight from `Code/Data/Species.cs`, so adding a species line
there and re-running the script keeps the codex in sync.

## Publishing checklist

1. Set `Org` in `catch_a_critter.sbproj` to your sbox.game organization ident.
2. Upload via the editor's Publish flow.
3. On sbox.game: `Configure > Edit Features` → opt into the **Play Fund**.
4. Ship a weekly content drop (new species/biome/talents) — update cadence is
   a major discovery lever on sbox.game.
