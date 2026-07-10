namespace Sandbox;

/// <summary>
/// Host-written AI state ordinal for client-side animation on skinned wildlife (<see cref="ThornsWildlifePantherAnimDriver"/>, <see cref="ThornsWildlifeElkAnimDriver"/>).
/// </summary>
[Title( "Thorns — Wildlife anim sync" )]
[Category( "Thorns/Wildlife" )]
[Icon( "sync" )]
[Order( 16 )]
public sealed class ThornsWildlifeAnimSync : Component
{
	[Sync( SyncFlags.FromHost )] public int AiStateOrdinal { get; set; }

	/// <summary>Velocity-driven locomotion anim — independent from <see cref="AiStateOrdinal"/>.</summary>
	[Sync( SyncFlags.FromHost )] public int LocomotionAnimOrdinal { get; set; }

	/// <summary>Host planar locomotion hint (wish vs velocity) for client anim when local delta/wish are stale.</summary>
	[Sync( SyncFlags.FromHost )] public float LocomotionPlanarSpeed { get; set; }

	double _lastPlanarSpeedSyncTime = -1.0;

	/// <summary>Host-confirmed melee event. Sequence drivers use this instead of guessing from the cooldown deadline.</summary>
	[Sync( SyncFlags.FromHost )] public int MeleeStrikeSerial { get; set; }

	/// <summary>Host realtime for <see cref="MeleeStrikeSerial"/> so late animation LOD wakeups do not replay stale bites.</summary>
	[Sync( SyncFlags.FromHost )] public double LastMeleeStrikeTime { get; set; }

	public void HostSetLocomotionAnim( ThornsAnimalLocomotionAnim anim )
	{
		if ( !Networking.IsHost )
			return;

		var ordinal = (int)anim;
		if ( LocomotionAnimOrdinal == ordinal )
			return;

		LocomotionAnimOrdinal = ordinal;
	}

	public void HostSetAiState( ThornsWildlifeAiState state )
	{
		if ( !Networking.IsHost )
			return;

		AiStateOrdinal = (int)state;
	}

	public void HostSetLocomotionPlanarSpeed( float planarSpeed )
	{
		if ( !Networking.IsHost )
			return;

		planarSpeed = MathF.Max( 0f, planarSpeed );
		const float quantStep = 8f;
		var quantized = MathF.Round( planarSpeed / quantStep ) * quantStep;

		var now = Time.Now;
		var minInterval = 1f / ThornsPerformanceBudgets.WildlifeLocomotionPlanarSpeedSyncHz;
		if ( _lastPlanarSpeedSyncTime >= 0.0
		     && now - _lastPlanarSpeedSyncTime < minInterval
		     && MathF.Abs( quantized - LocomotionPlanarSpeed ) < quantStep * 0.45f )
			return;

		LocomotionPlanarSpeed = quantized;
		_lastPlanarSpeedSyncTime = now;
	}

	public void HostNotifyMeleeStrike()
	{
		if ( !Networking.IsHost )
			return;

		MeleeStrikeSerial++;
		LastMeleeStrikeTime = Time.Now;
	}
}
