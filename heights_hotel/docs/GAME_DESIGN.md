# Heights Hotel — Game Design Document

## Pitch
Heights Hotel is a cozy pixel incremental management game. You build a hotel in a 2D side-on cross-section, hire staff, keep guests happy, and grow forever. Structural inspiration comes from Tiny Tower–style vertical growth; content, systems, and art are original.

## Core loop
1. Earn cash from lodging and amenities.
2. Spend cash to build adjacent rooms (sideways or up).
3. Hire and assign staff so rooms stay clean, staffed, and repaired.
4. Upgrade rooms and raise reputation to unlock better types.
5. Repeat: larger hotel → more guests → more income → more growth.

## Player goals
- Short: build a profitable lobby + rooms + café.
- Mid: unlock all room types and staff roles.
- Endless: maximize hotel value and lifetime profit.

## Construction
- Uniform cells: **96×64** logical pixels.
- Lobby at `(0,0)` is permanent and never demolished.
- Other rooms can be demolished for a partial refund if the hotel stays connected.
- New rooms must share an edge with an occupied cell (4-way adjacency).
- No diagonal-only placement; no floating rooms.
- Build cost = base cost × (1 + `CostEscalation` × existing room count).

## Room types

| Id | Category | Capacity | Base rate / visit | Notes |
|---|---|---|---|---|
| Lobby | Structure | — | — | Check-in, always present |
| StandardRoom | Lodging | 2 | Nightly rate | Starter lodging |
| DeluxeRoom | Lodging | 2 | Higher rate | Mid unlock |
| Suite | Lodging | 3 | Highest lodging | Late unlock |
| Cafe | Amenity | 4 seats | Per visit | Food amenity |
| Restaurant | Amenity | 6 seats | Per visit | Mid unlock |
| Spa | Amenity | 3 seats | Per visit | Late unlock |
| GiftShop | Amenity | 4 seats | Per visit | Mid unlock |
| Laundry | Support | — | — | Speeds cleaning |
| MaintenanceWorkshop | Support | — | — | Speeds repairs |
| StaffRoom | Support | — | — | Slight wage reduction |

Rooms upgrade **Level 1–5**. Each level raises capacity or rate (~18%) and costs escalating cash.

## Guest flow
1. Demand spawns guests based on reputation, lodging supply, and average satisfaction.
2. Guest enters lobby → receptionist check-in (or queue).
3. Assigned to a vacant lodging room of sufficient tier.
4. During stay: may visit amenities if seats available.
5. Dirt accumulates; low cleanliness hurts satisfaction.
6. Checkout pays nightly rate × nights × satisfaction multiplier + amenity spend + tips.
7. Leaves; room becomes dirty and needs housekeeper.

## Staff

| Role | Tasks | Hire cost | Wage / min |
|---|---|---|---|
| Receptionist | Check-in / check-out | Low | Low |
| Housekeeper | Clean dirty rooms | Low | Low |
| Cook | Staff Café / Restaurant | Mid | Mid |
| MaintenanceWorker | Repair broken rooms | Mid | Mid |

- Staff walk between rooms on the grid (elevator shaft for vertical hops).
- Unassigned staff idle in Staff Room or Lobby.
- Missing staff → queues, dirty rooms, closed amenities, unrepaired breaks.

## Economy
- Currency: integer **cents**.
- Income: lodging, amenities, tips.
- Expenses: wages (continuous), upkeep per room per minute, build/upgrade/hire.
- Net income rate shown in HUD.
- Soft fail: cash can go negative only via wages; build/hire/upgrade blocked when unaffordable.

## Progression (reputation)
Reputation levels unlock room types and raise guest expectations.

| Level | Unlocks |
|---|---|
| 1 | Lobby, StandardRoom, Cafe, Receptionist, Housekeeper, Cook |
| 2 | DeluxeRoom, GiftShop, Laundry |
| 3 | Restaurant, StaffRoom |
| 4 | Spa, MaintenanceWorkshop, MaintenanceWorker |
| 5 | Suite |
| 6 | Soft prestige tier begins |
| 7+ | Soft prestige: higher demand each step, no new room types |

Gain reputation from lifetime profit milestones and average satisfaction. Peak unlocks through level 6 are sticky; soft prestige continues endlessly.

## Weather
Weather rotates every ~40–90 sim seconds: Clear, Cloudy, Rain, Heatwave, Festival.
- Rain / Festival raise demand.
- Heatwave lowers demand and satisfaction.
- Rain / Festival give a small satisfaction comfort bonus for indoor stays.

## Daily goals
Each hotel day rolls three goals (build, serve guests, earn, occupancy, hire, or repairs) with cash rewards that can be claimed from the Goals dock.

## Demolition
Non-lobby rooms can be demolished for a partial refund. Demolition is blocked if guests/staff are using the room or if removing it would disconnect the hotel from the lobby.

## Simulation
- Fixed tick: **0.25 s** sim time at 1×.
- Speeds: pause, 1×, 2×, 4×.
- Seeded RNG for tests.
- Offline: up to **8 hours** at **60%** efficiency, applied once on load (can be positive or negative net).
- Receptionists claim and process check-ins; housekeepers/maintenance claim dirty/broken rooms so work is not doubled.
- Idle staff return to their assigned room, Staff Room, or Lobby.
- Guests prefer a lodging tier; underserved stays reduce satisfaction. Amenity visits only charge after arrival, with type-specific duration and satisfaction.

## UI / view
- Orthographic-feel 2D ScreenPanel hotel.
- Pan (drag) + pixel-perfect zoom steps.
- Reference presentation: dense framed hotel cross-section over a deep-blue city at night, warm amber interiors, dark navy/gold beveled HUD panels, and compact pixel typography.
- Persistent left rail: branded hotel sign, rating and reputation progress, occupancy/income summary, goals, and actionable notifications.
- Compact top-right cluster: cash, day/time, weather flavor, pause, simulation speed, and zoom controls.
- Center stage remains dedicated to the hotel facade; room details, people, state bubbles, floor labels, and the exterior shell must remain readable without opening menus.
- Bottom navigation opens one dock at a time for Build, Rooms, Staff, Guests, Goals, Stats, and Menu. It must not permanently cover the hotel with several management panels.
- Build palette, room inspector, hire/assignment panel, guest roster, ledger, tutorial prompts, and explicit action feedback are wired to live simulation state.
- Nearest-neighbor sprites from in-repo generator.

## Save
- `FileSystem.Data` JSON + backup.
- Autosave after commands and on interval.
- Versioned schema; corrupt → new game + message.

## MVP boundaries
In: all rooms/staff above, upgrades, reputation, offline, save/load, tutorial prompts, generated art/animations.
Post-MVP (implemented): weather effects, rotating daily goals with rewards, room demolition, receptionist queues, staff task claims, soft prestige, guest lodging preferences, actionable notifications, new-game reset.
Out: multiplayer, real money IAP, Steam workshop rooms, combat, narrative campaigns.
