# Thorns UI — Backend Contracts

Server-authoritative gameplay state flows to clients via **snapshots** and **validated requests**. UI reads `ThornsUiClientState` and invalidates through `UiRevisionBus` only.

## Snapshot bundle (`ThornsPlayerSnapshotBundle`)

Pushed to owning client on connect (`RpcReceiveSnapshot`) and after partial updates:

| DTO | Channel |
|-----|---------|
| `ThornsInventorySnapshotDto` | Inventory, Hotbar |
| `ThornsCraftSnapshotDto` | Craft |
| `ThornsJournalSnapshotDto` | Journal |
| `ThornsSkillsSnapshotDto` | Skills |
| `ThornsTamesSnapshotDto` | Tames |
| `ThornsGuildSnapshotDto` | Guild |
| `ThornsMapSnapshotDto` | Map |
| `ThornsVitalsSnapshotDto` | Vitals |

## Client requests (host validates)

| Request | Handler |
|---------|---------|
| `ThornsMoveItemRequest` | `ThornsPlayerGameplay.HostMoveItem` |
| `ThornsCraftRequest` | `ThornsPlayerGameplay.HostCraft` (timed queue) |
| `ThornsEquipRequest` | via move into armor slots |
| `ThornsSkillUnlockRequest` | `HostSkillUnlock` |
| `ThornsTameCommandRequest` | `ThornsTameCommandHost` |
| `ThornsGuildCreateRequest` | `ThornsGuildWorldService.HostCreateGuild` |
| `ThornsGuildJoinRequest` | `ThornsGuildWorldService.HostJoinGuild` |
| `ThornsGuildKickRequest` | `HostKick` |
| `ThornsGuildPromotionRequest` | `HostPromote` |
| `ThornsGuildInviteRequest` | reserved |

## Definition registries

Load content via `ThornsDefinitionRegistry` from:

- `ThornsItemCatalog` — items and recipes
- `ThornsMilestoneDefinitions` — Survivor Journey goals (chained via `PrerequisiteGoalId`, categories in `ThornsJourneyCategory`)
- `ThornsJourneyProgression` — unlock state, HUD auto-pin, town/weapon discovery hooks
- `ThornsUpgradeDefinitions` — 15 skills (Persistence / Instinct / Industry)

Upgrade points: 1 per player level (`ThornsUpgradeBalance`). Milestone XP grants levels (`ThornsXpBalance.XpPerLevel`, currently 300).

## Local-only (client)

- `ThornsLocalSettings` — graphics, audio, UI scale, crosshair, accessibility
- Map waypoints — `ThornsMapWorldService.ClientSetWaypoint`

## UiRevisionBus

Systems call `Publish(channel)` after mutating authoritative state. UI subscribes to `MenuRevisionChanged` — **no polling**, no per-frame `BuildHash` on gameplay data.
