# Sunday Dynasty — Architecture

Franchise management game for s&box. Server-authoritative, data-oriented, multiplayer-first.

## Layer Overview

```
┌─────────────────────────────────────────────────────────────┐
│  UI (Razor + ViewModels) — read-only projections          │
├─────────────────────────────────────────────────────────────┤
│  Visualization — replays SimEventRecord streams             │
├─────────────────────────────────────────────────────────────┤
│  Networking — snapshots + RPC commands (s&box-specific)     │
├─────────────────────────────────────────────────────────────┤
│  Services — LeagueService, PersistenceService               │
├─────────────────────────────────────────────────────────────┤
│  Systems — Draft, Trade, Sim, Development, etc.             │
├─────────────────────────────────────────────────────────────┤
│  Domain — pure data (LeagueState, PlayerState, …)           │
├─────────────────────────────────────────────────────────────┤
│  Core — IDs, enums, events, commands, interfaces            │
└─────────────────────────────────────────────────────────────┘
```

**Rule:** UI and visualization never mutate league state or run simulation logic.

## Folder Structure

```
Code/Dynasty/
├── Bootstrap/          # DynastyApp, scene bootstrap
├── Core/               # IDs, enums, events, commands, interfaces
├── Data/               # Static definitions (teams, names, attributes)
├── Domain/             # Pure data models + factories
├── Networking/         # LeagueNet host/client replication (s&box)
├── Persistence/        # Versioned JSON saves + migrations
├── Services/           # LeagueService orchestration
├── Systems/            # Modular behavior (one concern per system)
├── UI/                 # Razor HUD + ViewModels
└── Visualization/      # Game replay (top-down / drive panel)
```

## Domain Model

`LeagueState` is the root aggregate:

- `Teams`, `Players`, `Coaches` — entity dictionaries keyed by strong IDs
- `Schedule`, `Draft`, `FreeAgency`, `News`, `History`
- `GmAssignments` — maps Steam IDs to teams (no single-owner assumption)
- `StateRevision` — optimistic concurrency for commands and network sync

All entities are POCOs suitable for JSON serialization and future porting.

## Systems (modular)

| System | Responsibility |
|--------|----------------|
| `SeasonSimulationSystem` | Weekly game sim, standings |
| `GameSimulationEngine` | Statistical football + event stream |
| `PlayerDevelopmentSystem` | Growth/decline |
| `InjurySystem` | Injuries + recovery |
| `DraftSystem` | Prospects, order, picks |
| `TradeSystem` | Evaluation + execution |
| `ContractSystem` | Free agency offers |
| `ScoutingSystem` | Partial information reveal |
| `NewsSystem` | Dynamic headlines |
| `HistorySystem` | Championships, retirements |
| `FacilitySystem` | Upgrades |
| `FanSystem` | Attendance, happiness |
| `ChemistrySystem` | Morale, locker room |
| `RetirementSystem` | End-of-career |

Systems implement `ILeagueSystem` and register via `LeagueRuntime`. Removing a system does not break others.

## Event Bus

`LeagueEventBus` publishes immutable `ILeagueEvent` records:

- Decouples systems (e.g. injury → news)
- Feeds UI refresh triggers
- Provides event log for future replay/audit
- `GameSimulatedEvent` carries scores; `SimEventRecord` list is stored on `GameResult` for visualization

## Commands (multiplayer-safe)

Clients send `ILeagueCommand` to host via RPC. Host validates:

1. `ExpectedStateRevision` matches
2. GM has rights to the team (extend per command)
3. System rules pass

Commands never execute on clients.

## Networking

- **Host:** `Dynasty.LeagueNet.LeagueHostComponent` owns `LeagueService`, runs all commands/simulation
- **Note:** Use `GameNetworking` (`Sandbox.Networking`) for lobby/host APIs — not the `Dynasty.LeagueNet` namespace
- **Replication:** `[Sync(FromHost)]` `LeagueSnapshotJson` + `StateRevision` on revision change
- **Clients:** `LeagueClientComponent` deserializes snapshot into read-only local mirror
- **Dedicated server:** Host runs without UI; clients are GM clients

Scene setup: network-spawn the host object (`NetworkSpawn()`), attach `DynastyBootstrap`, `LeagueHostComponent`, `DynastyHudComponent`.

## Persistence

- `LeagueSaveEnvelope` — versioned wrapper for migrations
- `LeagueSaveSerializer` — JSON with custom ID converters
- Saves to `%LocalAppData%/SundayDynasty/Saves/{slot}.dynasty.json`

## UI Architecture

- `DynastyGameShell.razor` — main menu (new/load/delete saves) and in-game shell
- `DynastyHud.razor` — League Home, Team, Schedule, Draft, News tabs (in-game only)
- `LeagueViewModelProvider` builds screen-specific view models from `LeagueState`
- Extend with additional view models: Contracts, Trade Center, Facilities, Hall of Fame

## Saves & Session Flow

- Saves stored via `FileSystem.Data` at `/saves/{slotId}.dynasty.json` (s&box game data folder)
- `PersistenceService` — list, create, load, delete, auto-save on league mutations
- `GameSession` — toggles `MainMenu` vs `InGame` screens
- Returning to main menu saves first, then unloads league state

## Visualization

Simulation produces `GameSimulationOutput.Events` before any viewer runs.

- `IGameVisualizer` — Option A (`TopDownFieldVisualizer`) or B (`DrivePanelVisualizer`)
- `GameReplayController` — playback timing only

## Data-Driven Extension

- Team definitions in `LeagueDataDefinitions` (→ JSON assets later)
- Position attributes in `PlayerAttributeKeys`
- New traits, facilities, news templates — add enum/definition without changing system contracts

## Future: Web/Mobile Companion

Domain + Persistence layers have no s&box dependency. Expose the same `LeagueState` JSON via HTTP API on dedicated server server-side.

## Future: Unity / Roblox / Browser

Port order: `Core` → `Domain` → `Systems` → `Services`. Replace `Networking/` and `UI/` per platform.
