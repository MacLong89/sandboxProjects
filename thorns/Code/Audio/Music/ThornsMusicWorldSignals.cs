namespace Sandbox;

/// <summary>Host-side combat/world signals consumed by <see cref="ThornsAtmosphericMusic"/> (lightweight, no per-frame scans).</summary>
public static class ThornsMusicWorldSignals
{
	const float GunshotMemorySeconds = 48f;
	const float DamageMemorySeconds = 55f;
	const int MaxGunshotEntries = 32;

	struct GunshotEntry
	{
		public Vector3 World;
		public double Time;
	}

	static readonly GunshotEntry[] Gunshots = new GunshotEntry[MaxGunshotEntries];
	static int _gunshotWrite;

	public static void HostRegisterGunshot( Vector3 worldEmit )
	{
		if ( !Networking.IsHost )
			return;

		Gunshots[_gunshotWrite % MaxGunshotEntries] = new GunshotEntry { World = worldEmit, Time = Time.Now };
		_gunshotWrite++;
		ThornsBanditHearingHub.HostRegisterGunshot( worldEmit );
	}

	public static void HostNotifyPlayerDamaged( GameObject pawnRoot )
	{
		if ( !Networking.IsHost || pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var music = pawnRoot.Components.Get<ThornsAtmosphericMusic>();
		if ( music.IsValid() )
			music.HostOnPlayerDamaged();
	}

	public static bool HostHasRecentGunfireNear( Vector3 listenerWorld, float planarRadius, double sinceSeconds )
	{
		if ( planarRadius <= 0f )
			return false;

		var cutoff = Time.Now - sinceSeconds;
		var r2 = planarRadius * planarRadius;
		for ( var i = 0; i < MaxGunshotEntries; i++ )
		{
			ref var e = ref Gunshots[i];
			if ( e.Time < cutoff )
				continue;

			var dx = e.World.x - listenerWorld.x;
			var dy = e.World.y - listenerWorld.y;
			if ( dx * dx + dy * dy <= r2 )
				return true;
		}

		return false;
	}

	public static void HostPruneOldGunshots( double maxAgeSeconds )
	{
		var cutoff = Time.Now - maxAgeSeconds;
		for ( var i = 0; i < MaxGunshotEntries; i++ )
		{
			if ( Gunshots[i].Time < cutoff )
				Gunshots[i] = default;
		}
	}
}
