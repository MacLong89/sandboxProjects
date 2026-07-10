namespace Terraingen.Animals;

using Terraingen.Core;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>Instant tame teleport beside owners — used on summon and after world restore.</summary>
public static class ThornsTameSummonUtil
{
	public static bool TryResolveNearOwnerPosition(
		Scene scene,
		GameObject owner,
		int ringIndex,
		out Vector3 position )
	{
		position = default;
		if ( scene is null || !scene.IsValid() || !owner.IsValid() )
			return false;

		const float ringSpacing = 56f;
		const float angleStep = 72f;
		var ring = Math.Max( 0, ringIndex ) / 5;
		var slot = Math.Max( 0, ringIndex ) % 5;
		var dist = ThornsTameCatalog.SummonOffsetInches + ring * ringSpacing;
		var offset = Rotation.FromYaw( slot * angleStep + ring * 18f ).Forward * dist;
		if ( offset.LengthSquared < 0.01f )
			offset = Vector3.Right * dist;

		var requested = owner.WorldPosition + offset;

		if ( ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( scene, requested, 384f, out position, out _ ) )
			return true;

		var terrain = ThornsTerrainCache.Resolve( scene );
		if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, requested, out position ) )
			return true;

		position = new Vector3( requested.x, requested.y, owner.WorldPosition.z + 4f );
		return true;
	}

	public static void HostSummonNearOwner( ThornsAnimalBrain brain, int ringIndex = 0 )
	{
		if ( brain is null || !brain.IsValid() || !brain.IsTamed || brain.IsDead || brain.IsMounted )
			return;

		ThornsPlayerRootCache.Refresh( brain.Scene );
		brain.InvalidateTamedOwnerCache();
		var owner = brain.ResolveTamedOwnerForSummon();
		if ( !owner.IsValid() )
			return;

		if ( !TryResolveNearOwnerPosition( brain.Scene, owner, ringIndex, out var pos ) )
			return;

		brain.ApplySummonTeleport( owner, pos );
	}

	public static void HostSummonAllOwnedTamesNearPlayers( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		ThornsPlayerRootCache.Refresh( scene );
		var ringByOwner = new Dictionary<string, int>( StringComparer.Ordinal );

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || !brain.IsTamed || brain.IsDead || brain.IsMounted )
				continue;

			var ownerKey = brain.TamedOwnerAccountKey;
			if ( string.IsNullOrEmpty( ownerKey ) )
				continue;

			if ( !ThornsPlayerRootCache.TryGetByAccountKey( scene, ownerKey ).IsValid() )
				continue;

			var ring = ringByOwner.GetValueOrDefault( ownerKey, 0 );
			HostSummonNearOwner( brain, ring );
			ringByOwner[ownerKey] = ring + 1;
		}

		HostRefreshTameSnapshots( scene );
	}

	public static void HostSummonOwnedTamesNearPlayer( Scene scene, string ownerAccountKey )
	{
		if ( scene is null || !scene.IsValid() || string.IsNullOrEmpty( ownerAccountKey ) )
			return;

		ThornsPlayerRootCache.Refresh( scene );
		if ( !ThornsPlayerRootCache.TryGetByAccountKey( scene, ownerAccountKey ).IsValid() )
			return;

		var ring = 0;
		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || !brain.IsTamed || brain.IsDead || brain.IsMounted )
				continue;

			if ( !string.Equals( brain.TamedOwnerAccountKey, ownerAccountKey, StringComparison.Ordinal ) )
				continue;

			HostSummonNearOwner( brain, ring++ );
		}

		HostRefreshTameSnapshots( scene );
	}

	static void HostRefreshTameSnapshots( Scene scene )
	{
		foreach ( var gameplay in scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( gameplay.IsValid() )
				gameplay.HostRebuildTamesFromWorld();
		}
	}
}
