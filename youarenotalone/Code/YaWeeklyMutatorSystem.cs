using System;

namespace Sandbox;

/// <summary>Host: rotating lightweight mutators (UTC weekday). Replicated label + tuning multipliers.</summary>
[Title( "YouAreNotAlone — Weekly mutator" )]
[Category( "YouAreNotAlone" )]
[Icon( "calendar_month" )]
[Order( 9 )]
public sealed class YaWeeklyMutatorSystem : Component
{
	public static YaWeeklyMutatorSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public string ActiveMutatorLabel { get; private set; } = "";

	[Sync( SyncFlags.FromHost )] public float HunterMoveSpeedMul { get; private set; } = 1f;

	[Sync( SyncFlags.FromHost )] public float AloneMoveSpeedMul { get; private set; } = 1f;

	[Sync( SyncFlags.FromHost )] public float AloneParanoiaCooldownMul { get; private set; } = 1f;

	[Sync( SyncFlags.FromHost )] public float RoundDurationMul { get; private set; } = 1f;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		HostApplyWeekdayMutator();
	}

	void HostApplyWeekdayMutator()
	{
		HunterMoveSpeedMul = 1f;
		AloneMoveSpeedMul = 1f;
		AloneParanoiaCooldownMul = 1f;
		RoundDurationMul = 1f;
		ActiveMutatorLabel = "";

		switch ( DateTime.UtcNow.DayOfWeek )
		{
			case DayOfWeek.Monday:
				ActiveMutatorLabel = "Mutator: Hunter Sprint+";
				HunterMoveSpeedMul = 1.12f;
				break;
			case DayOfWeek.Tuesday:
				ActiveMutatorLabel = "Mutator: Paranoia Surge";
				AloneParanoiaCooldownMul = 0.72f;
				break;
			case DayOfWeek.Wednesday:
				ActiveMutatorLabel = "Mutator: Alone Predator";
				AloneMoveSpeedMul = 1.1f;
				break;
			case DayOfWeek.Thursday:
				ActiveMutatorLabel = "Mutator: Extended Hunt";
				RoundDurationMul = 1.25f;
				break;
			case DayOfWeek.Friday:
				ActiveMutatorLabel = "Mutator: Blitz Round";
				RoundDurationMul = 0.82f;
				break;
			case DayOfWeek.Saturday:
				ActiveMutatorLabel = "Mutator: Chaos Night";
				HunterMoveSpeedMul = 1.08f;
				AloneMoveSpeedMul = 1.08f;
				AloneParanoiaCooldownMul = 0.85f;
				break;
			case DayOfWeek.Sunday:
				ActiveMutatorLabel = "Mutator: Training Day";
				RoundDurationMul = 1.1f;
				break;
		}

		Log.Info( $"[YA] Weekly mutator active: '{ActiveMutatorLabel}'" );
	}
}
