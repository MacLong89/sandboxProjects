namespace Terraingen.Animals;

using Terraingen.Buildings;
using Terraingen.Combat;
using Terraingen.Player;
using Terraingen.TerrainGen;

static class ThornsAnimalWorldUtil
{
	const float NavSampleMaxOffset = 96f;
	const float DefaultBuildingFootprintMarginInches = 32f;

	public static bool IsBlockedByBuildingFootprint( Vector3 worldPos, float bodyRadius, float extraMarginInches = DefaultBuildingFootprintMarginInches )
	{
		if ( ThornsProcBuildingFootprintRegistry.Count == 0 )
			return false;

		return ThornsProcBuildingFootprintRegistry.ContainsWorldPoint(
			worldPos.x,
			worldPos.y,
			MathF.Max( bodyRadius, 0f ) + extraMarginInches );
	}

	public static float SeaLevelWorldZ( Terrain terrain, ThornsTerrainConfig config )
		=> ThornsTerrainSurface.GetSeaLevelWorldZ( terrain, config );

	public static bool TryGetHeightField( Scene scene, out HeightmapField field )
	{
		field = null;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var bootstrap in scene.GetAllComponents<ThornsTerrainBootstrap>() )
		{
			if ( bootstrap is null || !bootstrap.IsValid() || !bootstrap.IsWorldApplied )
				continue;

			field = bootstrap.GetHeightFieldForMap();
			return field is not null;
		}

		return false;
	}

	public static ThornsTerrainConfig ResolveTerrainConfig( Scene scene )
	{
		var cached = ThornsTerrainCache.Config;
		if ( cached is not null )
			return cached;

		if ( scene is null || !scene.IsValid() )
			return null;

		foreach ( var bootstrap in scene.GetAllComponents<ThornsTerrainBootstrap>() )
		{
			if ( bootstrap is null || !bootstrap.IsValid() )
				continue;

			return bootstrap.Config;
		}

		return null;
	}

	public static bool IsUnderSeaLevel( Scene scene, Terrain terrain, ThornsTerrainConfig config, Vector3 worldPos )
	{
		if ( !terrain.IsValid() || config is null )
			return false;

		if ( TryGetHeightField( scene, out var field ) )
		{
			var origin = terrain.GameObject.WorldPosition;
			var size = terrain.TerrainSize;
			var localX = worldPos.x - origin.x;
			var localY = worldPos.y - origin.y;
			if ( localX >= 0f && localY >= 0f && localX <= size && localY <= size )
			{
				var sampler = new TerrainChunkSampler( field, size, terrain.TerrainHeight );
				return sampler.IsUnderSeaLevel( localX, localY, config.SeaLevelNormalized );
			}
		}

		return worldPos.z <= SeaLevelWorldZ( terrain, config ) + 4f;
	}

	public static bool TrySnapToTerrain( Terrain terrain, Vector3 near, out Vector3 snapped )
	{
		snapped = near;
		if ( !terrain.IsValid() )
			return false;

		var maxHeight = terrain.TerrainHeight;
		var clamped = ThornsTerrainSurface.ClampToTerrainBounds( terrain, near );
		var x = clamped.x;
		var y = clamped.y;
		var min = terrain.GameObject.WorldPosition;
		var rayStart = new Vector3( x, y, min.z + maxHeight * 2f );
		if ( !terrain.RayIntersects( new Ray( rayStart, Vector3.Down ), maxHeight * 4f, out var hit ) )
			return false;

		snapped = terrain.GameObject.WorldTransform.PointToWorld( hit ) + Vector3.Up * 4f;
		return true;
	}

	public static bool IsUnderwater( Scene scene, Terrain terrain, ThornsTerrainConfig config, Vector3 worldPos )
		=> IsUnderSeaLevel( scene, terrain, config, worldPos );

	public static bool IsUnderwater( Terrain terrain, ThornsTerrainConfig config, Vector3 worldPos )
		=> IsUnderSeaLevel( null, terrain, config, worldPos );

	public static bool IsDryLand( Scene scene, Terrain terrain, ThornsTerrainConfig config, Vector3 worldPos )
		=> !IsUnderSeaLevel( scene, terrain, config, worldPos );

	public static bool TryPickDryLandPoint(
		Scene scene,
		Vector3 center,
		float radius,
		out Vector3 dryPoint,
		int maxAttempts = 10 )
	{
		dryPoint = center;
		if ( scene is null || !scene.IsValid() )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ResolveTerrainConfig( scene );
		if ( !terrain.IsValid() || config is null )
			return false;

		for ( var attempt = 0; attempt < maxAttempts; attempt++ )
		{
			var yaw = Game.Random.Float( 0f, 360f );
			var dist = radius * MathF.Sqrt( Game.Random.Float( 0f, 1f ) );
			var candidate = center + Rotation.FromYaw( yaw ).Forward * dist;
			if ( !TrySnapToTerrain( terrain, candidate, out var snapped ) )
				continue;

			if ( IsUnderSeaLevel( scene, terrain, config, snapped ) )
				continue;

			if ( IsBlockedByBuildingFootprint( snapped, 0f ) )
				continue;

			dryPoint = snapped;
			return true;
		}

		return false;
	}

	public static bool TryEstimateSlopeDegrees( Terrain terrain, Vector3 worldPos, out float slopeDegrees )
	{
		slopeDegrees = 0f;
		if ( !terrain.IsValid() )
			return false;

		const float sample = 48f;
		if ( !TrySnapToTerrain( terrain, worldPos, out var center ) )
			return false;
		if ( !TrySnapToTerrain( terrain, worldPos + Vector3.Forward * sample, out var forward ) )
			return false;
		if ( !TrySnapToTerrain( terrain, worldPos + Vector3.Right * sample, out var right ) )
			return false;

		var normal = Vector3.Cross( forward - center, right - center ).Normal;
		if ( normal.z < 0f )
			normal = -normal;

		slopeDegrees = MathF.Acos( Math.Clamp( normal.z, -1f, 1f ) ) * 180f / MathF.PI;
		return true;
	}

	public static bool IsNavMeshAvailableNear( Scene scene, Vector3 worldPos, float maxOffset = 512f )
	{
		if ( scene?.NavMesh is null || !scene.NavMesh.IsEnabled )
			return false;

		var closest = scene.NavMesh.GetClosestPoint( worldPos, maxOffset );
		if ( !closest.HasValue )
			return false;

		return (closest.Value - worldPos).Length <= NavSampleMaxOffset;
	}

	public static bool TryGetNavPoint( Scene scene, Vector3 desired, out Vector3 navPoint )
	{
		navPoint = desired;
		if ( scene?.NavMesh is null || !scene.NavMesh.IsEnabled )
			return false;

		var closest = scene.NavMesh.GetClosestPoint( desired, 512f );
		if ( !closest.HasValue )
			return false;

		navPoint = closest.Value;
		return (navPoint - desired).Length <= NavSampleMaxOffset;
	}

	public static bool TryGetDryNavPoint( Scene scene, Vector3 desired, out Vector3 navPoint )
	{
		if ( TryGetDryNavPointCore( scene, desired, out navPoint ) )
			return true;

		for ( var ring = 0; ring < 6; ring++ )
		{
			var yaw = ring * 60f;
			var offset = Rotation.FromYaw( yaw ).Forward * 128f;
			if ( TryGetDryNavPointCore( scene, desired + offset, out navPoint ) )
				return true;
		}

		return false;
	}

	static bool TryGetDryNavPointCore( Scene scene, Vector3 desired, out Vector3 navPoint )
	{
		if ( !TryGetNavPoint( scene, desired, out navPoint ) )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ResolveTerrainConfig( scene );
		if ( terrain.IsValid() && config is not null && IsUnderSeaLevel( scene, terrain, config, navPoint ) )
			return false;

		if ( IsBlockedByBuildingFootprint( navPoint, 0f ) )
			return false;

		return true;
	}

	public static bool HasLineOfSight( Scene scene, GameObject from, GameObject to )
	{
		if ( scene is null || !scene.IsValid() || !from.IsValid() || !to.IsValid() )
			return false;

		var fromHeight = EyeHeightFor( from );
		var toHeight = EyeHeightFor( to );
		var start = from.WorldPosition + Vector3.Up * fromHeight;
		var end = to.WorldPosition + Vector3.Up * toHeight;
		var tr = scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( from )
			.IgnoreGameObjectHierarchy( to )
			.Run();

		return !tr.Hit || tr.GameObject == to || tr.GameObject.Root == to;
	}

	static float EyeHeightFor( GameObject root )
	{
		var brain = root.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( brain.IsValid() )
			return MathF.Max( 16f, brain.GetBodyRadius() * 0.85f );

		return 32f;
	}

	public static bool IsPlayerThreat( ThornsAnimalSpeciesData species, GameObject playerRoot )
	{
		if ( ThornsAnimalManager.ShouldIgnorePlayers( species ) || !playerRoot.IsValid() )
			return false;

		var health = playerRoot.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndDescendants );
		if ( health is null || !health.IsValid() )
			return true;

		return health.IsAlive;
	}

	public static bool CanPredatorAttackPlayer( ThornsAnimalSpeciesData species )
	{
		return species.AttackPlayers && !ThornsAnimalManager.ShouldIgnorePlayers( species );
	}

	public static bool IsPlayerObject( GameObject root )
	{
		if ( !root.IsValid() )
			return false;

		var gameplay = root.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );
		return gameplay is not null && gameplay.IsValid();
	}

	public static float RollVariation( float baseValue, float fraction = 0.1f )
	{
		var scale = 1f + Game.Random.Float( -fraction, fraction );
		return baseValue * scale;
	}
}
