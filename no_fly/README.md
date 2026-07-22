# NO FLY

Silly 3D multiplayer social-deception party game for **s&box**.

## Open & play

1. Open `no_fly.sbproj` in s&box.
2. Play scene `scenes/nofly_airport.scene`.
3. Lobby appears — **Ready** for multiplayer, or pick a **Single Player** role.

## Controls

| Action | Default |
|---|---|
| Move | WASD / stick |
| Look | Mouse / right stick |
| Sprint | Shift / run |
| Jump | Space |
| Interact | **E** (or walk into document / bag scan) |
| Report player | Right mouse (while looking at them) |
| Security arrest | Left mouse |
| Undercover mark | R |
| Undercover arrest | 1 |

## Round flow

1. **Before Security (30s)** — Staff walk to their desks. Passengers wait in the lobby (walk around). Smuggler forges a document field and hides contraband. Queues stay closed.
2. **Security Open** — Passengers join document check, then bag scan. Agents get inspection popups when a passenger reaches their desk.
3. **Boarding / Chase** — Catch your flight, or hunt an exposed smuggler.

## Roles

- **Smuggler** — forge one passport field, hide one contraband icon, board Gate flight
- **Document Agent** — compare docs, click the wrong field, Approve / Reject / Call Security
- **Scanner Agent** — spot partial contraband silhouette, Clear / Search / Call Security
- **Security** — answer tablet alerts, detain/arrest
- **Undercover** — use incomplete clues, mark + one arrest attempt
- **Regular Passenger** — objectives, security, catch your flight

## Debug (host)

```
nofly_start_solo Smuggler
nofly_force_role DocumentAgent
nofly_skip_phase
nofly_spawn_npc
nofly_trigger_chase
nofly_end_round tsa
nofly_show_roles
```

## Architecture

Code lives under `Code/NoFly/` — managers, airport kit builder, document/luggage minigames, bots/NPCs, razor UI.
