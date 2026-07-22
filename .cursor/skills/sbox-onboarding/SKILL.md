---
name: sbox-onboarding
description: >-
  Final Outpost-style first-run tip onboarding for s&box games in this repo.
  Use when adding, fixing, or aligning tutorial tips, coach marks, Hide tips,
  Got it dismiss, H-key tip toggle, or first-session onboarding in any game.
---

# s&box onboarding (Final Outpost pattern)

Reference implementation: `the_final_outpost` (`TutorialTips.cs`, `GameCore` dismiss/toggle, `Hud.razor` tip modal).

## UX rules

1. **Basics only** — 4–8 short tips max. Teach the loop, not every system.
2. **One tip at a time** — large center modal; title + 1–3 sentences + icon.
3. **Goal-gated succession (required)** — next tip appears only when the previous tip's *goal* is completed in gameplay. Reference: `fauna2` (`OnboardingTips` + `ObjectiveSystem` + `AdvanceOnboardingTipAfterGoal`).
4. **Got it** (primary) — soft-dismiss only: hide the current tip until that goal completes (or the goal index changes). Must **not** immediately show the next tip.
5. **Hide tips** (ghost) — experienced-player escape: stop all tips, persist, toast *"Tips hidden — press H to show again"*.
6. **H toggle** — re-enable or hide tips anytime during the tip window.
7. **Easy to dismiss** — modal is clear; no multi-page walls of text.

### Goal gating checklist

- Each tip maps to a real goal (objective index, milestone, trigger, phase, or `MeetsCondition`)
- Completing the goal force-shows the next tip
- Soft-dismiss state is per-goal (dismissed goal index / tip id)
- Never: Got it → cooldown → PickNext for the next tip in a flat list

## Persistence (required fields)

```csharp
public bool HideTutorialTips { get; set; }
public List<string> TutorialTipsShown { get; set; } = new();
```

Prefer client/settings or save data that already persists. Do not clear `HideTutorialTips` on prestige/wipe unless the design explicitly resets teaching.

## Tip definition shape

```csharp
public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int Priority { get; init; }
}
```

Pick highest-priority unshown tip that meets conditions. Stop after early-game window.

## UI markup (match labels exactly)

```razor
<div class="overlay tip-overlay">
	<div class="modal confirm tutorial-tip">
		<i class="icon confirm-icon tip-icon">@tip.Icon</i>
		<div class="confirm-title">@tip.Title</div>
		<div class="confirm-body">@tip.Body</div>
		<div class="btn-row">
			<div class="btn ghost" onclick=@HideAllTips>
				<i class="icon">visibility_off</i><span>Hide tips</span>
			</div>
			<div class="btn primary" onclick=@DismissTip><span>Got it</span></div>
		</div>
	</div>
</div>
```

Button copy must stay **Hide tips** / **Got it** (not Skip / Don't show / OK) so every game feels the same.

## Runtime API

```csharp
void DismissTutorialTip( bool hideAll = false );
void ToggleTutorialTipsHidden(); // H key
```

- Block gameplay input while a tip is open when that matches the game (Final Outpost does).
- Do not show tips over welcome/mission/shop takeovers — wait until those close.
- Cooldown after Got it so the next tip does not pop instantly.

## H key

Prefer an `Input` action named `TutorialTips` bound to `h`. If the project has no Input.config entry yet, `Input.Keyboard.Pressed( "h" )` is acceptable until the action is added.

## Games status

All playable games use **center tip modals** + **goal-gated succession** (fauna2 pattern). Side goal boards / objective cards are OK as progression HUD, not tip teaching.

| Game | Goal gate |
|------|-----------|
| `fauna2` | `ObjectiveSystem` goal index (canonical) |
| `the_final_outpost` | Night/season + `MeetsCondition` |
| `catch_a_critter` / `sky_empire` | Catch/sell / milestones |
| `offshore` | `ObjectiveIndex` |
| `under_pressure` / `plunge` / `run_gun` | Gameplay flags |
| `pawn` / `heights_hotel` | Action triggers / tutorial sequence |
| `think_drink` / `aimbox` / `dynastyfootball` | Ready/buzz / menu visits / FTUE UI |
| `no_fly` | Role + round phase |
| `youarenotalone` / `terraingen` | Move / interact / menu goals |
| Skip | `shared_assets`, `scene_lab`, legacy `thorns/` shell |

## Do not

- Dump 3+ steps in one modal for first-run teaching (use sequential tips).
- Use only "Skip tutorial" without a persistent Hide that can be toggled back with H.
- Write long encyclopedic tip bodies.
- Stack multiple first-run teachers (welcome intro + controls card + tip modals). Tips alone.
- Keep offline "welcome back / collect earnings" popups — those are retention, not onboarding.
