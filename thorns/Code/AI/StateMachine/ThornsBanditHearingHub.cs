namespace Sandbox;

/// <summary>Recent world sound events for bandit investigation (host-only).</summary>
public static class ThornsBanditHearingHub
{
	public enum EventKind
	{
		Gunshot,
		Explosion,
		SprintFootstep,
		AnimalAttack,
		AllyAlert,
	}

	public readonly struct HeardEvent
	{
		public Vector3 World { get; init; }
		public double Time { get; init; }
		public EventKind Kind { get; init; }
	}

	const int MaxEntries = 48;
	static readonly HeardEvent[] Events = new HeardEvent[MaxEntries];
	static int _write;

	public static void HostRegister( Vector3 world, EventKind kind )
	{
		if ( !Networking.IsHost )
			return;

		Events[_write % MaxEntries] = new HeardEvent { World = world, Time = Time.Now, Kind = kind };
		_write++;
	}

	public static void HostRegisterGunshot( Vector3 world ) => HostRegister( world, EventKind.Gunshot );
	public static void HostRegisterExplosion( Vector3 world ) => HostRegister( world, EventKind.Explosion );
	public static void HostRegisterAnimalAttack( Vector3 world ) => HostRegister( world, EventKind.AnimalAttack );
	public static void HostRegisterAllyAlert( Vector3 world ) => HostRegister( world, EventKind.AllyAlert );

	public static bool TryGetNearestHeardEvent(
		Vector3 listenerFlat,
		ThornsBanditArchetypeConfig cfg,
		double maxAgeSeconds,
		out HeardEvent best,
		out float bestDistSq )
	{
		best = default;
		bestDistSq = float.MaxValue;
		var cutoff = Time.Now - maxAgeSeconds;

		for ( var i = 0; i < MaxEntries; i++ )
		{
			ref var e = ref Events[i];
			if ( e.Time < cutoff )
				continue;

			var hearR = e.Kind switch
			{
				EventKind.Gunshot => cfg.HearGunshotRangeWorld,
				EventKind.Explosion => cfg.HearExplosionRangeWorld,
				EventKind.SprintFootstep => cfg.HearSprintFootstepRangeWorld,
				EventKind.AnimalAttack => cfg.HearAnimalAttackRangeWorld,
				EventKind.AllyAlert => cfg.AlertRadiusWorld,
				_ => cfg.HearGunshotRangeWorld,
			};

			var delta = e.World.WithZ( 0 ) - listenerFlat;
			var dsq = delta.LengthSquared;
			var r2 = hearR * hearR;
			if ( dsq > r2 || dsq >= bestDistSq )
				continue;

			bestDistSq = dsq;
			best = e;
		}

		return best.Time > 0;
	}
}
