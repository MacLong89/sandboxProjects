namespace Terraingen.Animals;

using Terraingen;
using Terraingen.Core;
using Terraingen.TerrainGen;

/// <summary>Shared host spawn placement for herds, packs, and solitaries.</summary>
public static class ThornsAnimalSpawnUtil
{
	public static bool TryPickDrySpawnPosition(
		Scene scene,
		Vector3 requested,
		float retryRadius,
		out Vector3 position,
		out ThornsAnimalFactory.SpawnPositionResult result )
		=> ThornsAnimalFactory.TryPickDrySpawnPosition( scene, requested, retryRadius, out position, out result );

	public static int HostSpawnGroup(
		Scene scene,
		ThornsAnimalSpeciesData species,
		Vector3 anchor,
		int? countOverride = null,
		List<ThornsAnimalBrain> spawnedBrains = null,
		float minPlayerClearanceInches = 0f )
	{
		if ( scene is null || species is null || !ThornsMultiplayer.IsHostOrOffline )
			return 0;

		var count = countOverride ?? Game.Random.Int( species.GroupSpawnCountMin, species.GroupSpawnCountMax );
		count = Math.Min( count, ThornsAnimalManager.RemainingSpawnSlots() );
		if ( count <= 0 )
			return 0;

		var spawned = 0;
		var herdGroupId = species.SocialMode == ThornsAnimalSocialMode.Herd
			? ThornsAnimalManager.AllocateHerdGroupId()
			: 0u;
		for ( var i = 0; i < count; i++ )
		{
			if ( !ThornsAnimalManager.CanSpawnMore() )
				break;

			var offset = Vector3.Random.WithZ( 0f ).Normal * Game.Random.Float( 24f, species.GroupSpawnRadius );
			var requested = anchor + offset;
			if ( !TryPickDrySpawnPosition( scene, requested, species.GroupSpawnRadius, out var pos, out var resolve ) )
				continue;

			ThornsAnimalSeparation.TryResolveClearSpawn( scene, species, pos, out pos );
			if ( minPlayerClearanceInches > 0f
			     && !IsPlanarDistanceFromAllPlayersAtLeast( scene, pos, minPlayerClearanceInches ) )
				continue;

			var brain = ThornsAnimalFactory.HostSpawn( scene, species, pos, Rotation.FromYaw( Game.Random.Float( 0f, 360f ) ) );
			if ( !brain.IsValid() )
				continue;

			if ( herdGroupId != 0 )
				brain.HostAssignHerdGroup( herdGroupId );

			spawnedBrains?.Add( brain );
			spawned++;
			if ( spawned == 1 && ThornsAnimalDebug.Verbose )
			{
				Log.Info(
					$"[Thorns Animals] First {species.DisplayName}: req={requested:F0} final={pos:F0} " +
					$"navSnap={resolve.NavSnapped} navFail={resolve.NavSnapFailed} underwater={resolve.WasUnderwater}" );
			}
		}

		if ( ThornsAnimalDebug.Verbose )
		{
			var groupLabel = species.SocialMode == ThornsAnimalSocialMode.Pack ? "Pack" : "Herd";
			Log.Info( $"[Thorns Animals] {groupLabel} {species.DisplayName}: spawned {spawned}/{count} at {anchor:F0}." );
		}

		return spawned;
	}

	public static int HostSpawnSolitary(
		Scene scene,
		ThornsAnimalSpeciesData species,
		Vector3 requested,
		float minPlayerClearanceInches = 0f )
	{
		if ( scene is null || species is null || !ThornsMultiplayer.IsHostOrOffline || !ThornsAnimalManager.CanSpawnMore() )
			return 0;

		if ( !TryPickDrySpawnPosition( scene, requested, species.WanderRadius, out var pos, out var resolve ) )
			return 0;

		ThornsAnimalSeparation.TryResolveClearSpawn( scene, species, pos, out pos );
		if ( minPlayerClearanceInches > 0f
		     && !IsPlanarDistanceFromAllPlayersAtLeast( scene, pos, minPlayerClearanceInches ) )
			return 0;

		var brain = ThornsAnimalFactory.HostSpawn( scene, species, pos, Rotation.FromYaw( Game.Random.Float( 0f, 360f ) ) );
		if ( !brain.IsValid() )
			return 0;

		if ( ThornsAnimalDebug.Verbose )
		{
			Log.Info(
				$"[Thorns Animals] Solitary {species.DisplayName}: req={requested:F0} final={pos:F0} " +
				$"navSnap={resolve.NavSnapped} navFail={resolve.NavSnapFailed} underwater={resolve.WasUnderwater}" );
		}

		if ( ThornsAnimalDebug.Verbose )
			Log.Info( $"[Thorns Animals] Solitary {species.DisplayName}: spawned at {pos:F0}." );

		return 1;
	}

	public static Vector3 PickAnchorAround( Vector3 center, float distance, float angleTurns )
	{
		var angle = angleTurns * MathF.PI * 2f + Game.Random.Float( -0.25f, 0.25f );
		var dist = distance * Game.Random.Float( 1f, 1.12f );
		return center + new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * dist;
	}

	public static bool IsPlanarDistanceFromAllPlayersAtLeast( Scene scene, Vector3 position, float minDistanceInches )
	{
		if ( scene is null || !scene.IsValid() || minDistanceInches <= 0f )
			return true;

		ThornsPlayerRootCache.Refresh( scene );
		var players = ThornsPlayerRootCache.RootsReadOnly;
		if ( players.Count == 0 )
			return true;

		var minSq = minDistanceInches * minDistanceInches;
		var flat = position.WithZ( 0f );
		for ( var i = 0; i < players.Count; i++ )
		{
			var player = players[i];
			if ( !player.IsValid() )
				continue;

			if ( (flat - player.WorldPosition.WithZ( 0f )).LengthSquared < minSq )
				return false;
		}

		return true;
	}

	public static bool TryPickAmbientAnchorNearPlayer(
		Scene scene,
		Vector3 playerPosition,
		float distanceMin,
		float distanceMax,
		float minPlayerClearanceInches,
		out Vector3 anchor,
		int maxAttempts = 10 )
	{
		anchor = default;
		if ( scene is null || !scene.IsValid() || distanceMin <= 0f || distanceMax < distanceMin )
			return false;

		minPlayerClearanceInches = MathF.Max( minPlayerClearanceInches, distanceMin );
		for ( var attempt = 0; attempt < maxAttempts; attempt++ )
		{
			var dist = Game.Random.Float( distanceMin, distanceMax );
			var candidate = PickAnchorAround( playerPosition, dist, Game.Random.Float( 0f, 1f ) );
			if ( !IsPlanarDistanceFromAllPlayersAtLeast( scene, candidate, minPlayerClearanceInches ) )
				continue;

			anchor = candidate;
			return true;
		}

		return false;
	}
}
