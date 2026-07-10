namespace Terraingen.AI;

using Terraingen.Multiplayer;

/// <summary>Event-driven hearing and ally combat alerts for bandits.</summary>
public static class ThornsBanditCommunication
{
	public enum SoundType
	{
		Gunshot,
		Explosion,
		Harvest,
		AnimalAttack,
		BuildingBreak,
		AllyCombat,
	}

	public readonly struct SoundEvent
	{
		public Vector3 World { get; init; }
		public double Time { get; init; }
		public SoundType Type { get; init; }
		public float Strength { get; init; }
	}

	const int MaxEntries = 64;
	static readonly SoundEvent[] Events = new SoundEvent[MaxEntries];
	static int _write;

	public static void HostRegister( Vector3 world, SoundType type, float strength = 1f )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		Events[_write % MaxEntries] = new SoundEvent
		{
			World = world,
			Time = Time.Now,
			Type = type,
			Strength = Math.Clamp( strength, 0.1f, 2f ),
		};
		_write++;
	}

	public static void HostRegisterGunshot( Vector3 world, float loudnessMultiplier = 1f ) =>
		HostRegister( world, SoundType.Gunshot, 1.4f * Math.Clamp( loudnessMultiplier, 0f, 2f ) );
	public static void HostRegisterExplosion( Vector3 world ) => HostRegister( world, SoundType.Explosion, 2f );
	public static void HostRegisterHarvest( Vector3 world ) => HostRegister( world, SoundType.Harvest, 0.7f );
	public static void HostRegisterAnimalAttack( Vector3 world ) => HostRegister( world, SoundType.AnimalAttack, 1f );
	public static void HostRegisterBuildingBreak( Vector3 world ) => HostRegister( world, SoundType.BuildingBreak, 1.1f );
	public static void HostRegisterAllyCombat( Vector3 world ) => HostRegister( world, SoundType.AllyCombat, 1.2f );

	public static bool TryGetNearestHeardEvent(
		Vector3 listenerFlat,
		ThornsBanditArchetypeConfig cfg,
		double maxAgeSeconds,
		out SoundEvent best,
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

			var hearR = e.Type switch
			{
				SoundType.Gunshot => cfg.HearGunshotRangeWorld,
				SoundType.Explosion => cfg.HearExplosionRangeWorld,
				SoundType.Harvest => cfg.HearHarvestRangeWorld,
				SoundType.AnimalAttack => cfg.HearAnimalAttackRangeWorld,
				SoundType.BuildingBreak => cfg.HearBuildingBreakRangeWorld,
				SoundType.AllyCombat => cfg.CommunicationRadiusWorld,
				_ => cfg.HearGunshotRangeWorld,
			};

			hearR *= e.Strength;
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

	public static void HostBroadcastCombatAlert( ThornsBanditBrain source, GameObject target, Vector3 lastKnown )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || source is null || !source.IsValid() )
			return;

		HostRegisterAllyCombat( lastKnown );

		var selfFlat = source.GameObject.WorldPosition.WithZ( 0 );
		var cfg = source.Archetype;
		var r2 = cfg.CommunicationRadiusWorld * cfg.CommunicationRadiusWorld;

		foreach ( var ally in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !ally.IsValid() || ally == source || ally.IsDead )
				continue;

			if ( source.GroupId != 0 && ally.GroupId != source.GroupId )
				continue;

			var allyFlat = ally.GameObject.WorldPosition.WithZ( 0 );
			if ( ( allyFlat - selfFlat ).LengthSquared > r2 )
				continue;

			ally.HostReceiveAllyCombatAlert( target, lastKnown, source.GameObject.WorldPosition );
		}
	}

	public static void HostBroadcastWildlifeAttackAlert( ThornsBanditBrain victim, GameObject attacker, Vector3 lastKnown )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || victim is null || !victim.IsValid() || !attacker.IsValid() )
			return;

		HostRegisterAllyCombat( lastKnown );
		HostRegisterAnimalAttack( lastKnown );

		var originFlat = victim.GameObject.WorldPosition.WithZ( 0 );
		var cfg = victim.Archetype;
		var hearRadius = cfg.HearAnimalAttackRangeWorld;
		var r2 = hearRadius * hearRadius;

		foreach ( var bandit in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !bandit.IsValid() || bandit == victim || bandit.IsDead )
				continue;

			var banditFlat = bandit.GameObject.WorldPosition.WithZ( 0 );
			if ( (banditFlat - originFlat).LengthSquared > r2 )
				continue;

			bandit.HostReceiveAllyCombatAlert( attacker, lastKnown, victim.GameObject.WorldPosition );
		}
	}
}
