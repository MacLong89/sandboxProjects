namespace Sandbox;

/// <summary>
/// Wildlife snapshot domain — tamed animals, mounts, progression, quit-save runtime cache.
/// </summary>
public static class ThornsWildlifeSnapshotService
{
	static List<ThornsPersistentWildlifeDto> _runtimeLastTamedWildlife;
	static double _wildlifeRuntimeCacheThrottleUntilRealtime;

	static void EnsureRuntimeCache()
	{
		if ( _runtimeLastTamedWildlife is null )
			_runtimeLastTamedWildlife = new List<ThornsPersistentWildlifeDto>();
	}

	public static void HostClearRuntimeCache()
	{
		EnsureRuntimeCache();
		_runtimeLastTamedWildlife.Clear();
	}

	public static int RuntimeCacheCount => _runtimeLastTamedWildlife?.Count ?? 0;

	public static void HostRefreshTamedWildlifeRuntimeCache()
	{
		if ( !Networking.IsHost || !Game.IsPlaying )
			return;

		EnsureRuntimeCache();
		var now = HostCaptureTamedWildlife();
		if ( now.Count <= 0 )
			return;

		_runtimeLastTamedWildlife.Clear();
		_runtimeLastTamedWildlife.AddRange( now );
	}

	public static void HostRefreshTamedWildlifeRuntimeCacheThrottled( float minIntervalSeconds = 6f )
	{
		if ( !Networking.IsHost || !Game.IsPlaying )
			return;

		if ( Time.Now < _wildlifeRuntimeCacheThrottleUntilRealtime )
			return;

		_wildlifeRuntimeCacheThrottleUntilRealtime = Time.Now + Math.Max( 1.5f, minIntervalSeconds );
		HostRefreshTamedWildlifeRuntimeCache();
	}

	public static List<ThornsPersistentWildlifeDto> HostResolveWildlifeForSnapshot()
	{
		var wildlifeNow = HostCaptureTamedWildlife();
		EnsureRuntimeCache();
		if ( wildlifeNow.Count > 0 )
		{
			_runtimeLastTamedWildlife.Clear();
			_runtimeLastTamedWildlife.AddRange( wildlifeNow );
			return wildlifeNow;
		}

		if ( _runtimeLastTamedWildlife.Count > 0 )
			return new List<ThornsPersistentWildlifeDto>( _runtimeLastTamedWildlife );

		return wildlifeNow;
	}

	public static void HostApplyWildlifeFromSave( Scene scene, IEnumerable<ThornsPersistentWildlifeDto> wildlife, GameObject deferralHost = null )
	{
		if ( wildlife is null )
			return;

		ThornsDeferredHostSpawnQueue queue = null;
		if ( deferralHost.IsValid() )
		{
			queue = deferralHost.Components.Get<ThornsDeferredHostSpawnQueue>();
			if ( !queue.IsValid() )
				queue = deferralHost.Components.Create<ThornsDeferredHostSpawnQueue>();
			queue.WorkBudgetPerFrame = 1;
		}

		foreach ( var w in wildlife )
		{
			if ( w is null || string.IsNullOrWhiteSpace( w.Species ) || w.WildlifeId == Guid.Empty )
				continue;

			if ( !Enum.TryParse<ThornsWildlifeSpeciesKind>( w.Species, ignoreCase: true, out var species ) )
				continue;

			if ( queue.IsValid() )
			{
				var dto = w;
				queue.TryEnqueue( () => HostApplyOneWildlifeFromSave( scene, dto, species ) );
				continue;
			}

			HostApplyOneWildlifeFromSave( scene, w, species );
		}
	}

	static void HostApplyOneWildlifeFromSave( Scene scene, ThornsPersistentWildlifeDto w, ThornsWildlifeSpeciesKind species )
	{
		if ( scene is null || !scene.IsValid() || w is null )
			return;

		var pos = new Vector3( w.Px, w.Py, w.Pz );
		var rot = Rotation.From( w.RPitch, w.RYaw, w.RRoll );

		ThornsWildlifeSpawn.HostCreateFromSave(
			scene,
			species,
			pos,
			rot,
			w.WildlifeId,
			w.CurrentHealth,
			w.TameOwnerAccountKey ?? "",
			w.TameFollowOwner,
			w.TameDisplayName ?? "",
			w.TameTotalXp,
			w.TameUnspentUpgradePoints,
			w.TameHpUpgradeSteps,
			w.TameDmgUpgradeSteps,
			w.TameSpdUpgradeSteps,
			w.TameQualityTier,
			w.TameAffinityHp,
			w.TameAffinityDmg,
			w.TameAffinitySpd,
			w.TameLegendaryAbility );
	}

	public static void HostRemapWildlifeOwnersForAccountKey( string accountKey, Guid newConnectionId )
	{
		if ( string.IsNullOrEmpty( accountKey ) || newConnectionId == Guid.Empty )
			return;

		var idStr = newConnectionId.ToString( "D" );

		foreach ( var wid in ThornsWildlifeIdentity.ActiveByHost.Values )
		{
			if ( wid is null || !wid.IsValid() )
				continue;

			if ( wid.TameOwnerAccountKeySync == accountKey )
			{
				wid.TameOwnerConnectionIdSync = idStr;
				wid.HostRefreshTameRegistryMembership();
			}
		}
	}

	static List<ThornsPersistentWildlifeDto> HostCaptureTamedWildlife()
	{
		var list = new List<ThornsPersistentWildlifeDto>();
		foreach ( var wid in ThornsWildlifeIdentity.ActiveByHost.Values )
		{
			if ( wid is null || !wid.IsValid() || string.IsNullOrEmpty( wid.TameOwnerAccountKeySync ) )
				continue;

			var t = wid.GameObject.WorldTransform;
			var ang = t.Rotation.Angles();
			var hp = wid.Components.Get<ThornsHealth>();

			list.Add( new ThornsPersistentWildlifeDto
			{
				WildlifeId = wid.WildlifeId,
				Species = wid.Species.ToString(),
				Px = t.Position.x,
				Py = t.Position.y,
				Pz = t.Position.z,
				RPitch = ang.pitch,
				RYaw = ang.yaw,
				RRoll = ang.roll,
				CurrentHealth = hp.IsValid() ? hp.CurrentHealth : 0f,
				TameOwnerAccountKey = wid.TameOwnerAccountKeySync,
				TameFollowOwner = wid.TameFollowOwnerSync,
				TameDisplayName = wid.TameDisplayNameSync ?? "",
				TameTotalXp = wid.TameTotalXp,
				TameUnspentUpgradePoints = wid.TameUnspentUpgradePoints,
				TameHpUpgradeSteps = wid.TameHpUpgradeSteps,
				TameDmgUpgradeSteps = wid.TameDmgUpgradeSteps,
				TameSpdUpgradeSteps = wid.TameSpdUpgradeSteps,
				TameQualityTier = wid.TameQualityTierSync,
				TameAffinityHp = wid.TameAffinityHpSync,
				TameAffinityDmg = wid.TameAffinityDmgSync,
				TameAffinitySpd = wid.TameAffinitySpdSync,
				TameLegendaryAbility = wid.TameLegendaryAbilitySync
			} );
		}

		return list;
	}
}
