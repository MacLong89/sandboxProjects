namespace Terraingen.TerrainGen;

using Terraingen;

/// <summary>Detects when the local player can drink from open water under the crosshair.</summary>
public static class ThornsNaturalWaterDrink
{
	public const float DrinkHoldSeconds = 1f;
	public const float InteractRange = 260f;
	const float WaterSurfaceMarginInches = 8f;
	const float MaxWadeBelowSea = 28f;

	/// <summary>Horizontal search cap when locating the nearest shoreline for water ambience.</summary>
	public const float WaterAmbienceAudibleRange = 2400f;

	public const float WaterAmbienceFullRange = 100f;
	public const float WaterAmbienceVerticalFadeRange = 520f;

	static readonly float[] WaterSearchSteps =
	{
		48f, 96f, 160f, 256f, 384f, 512f, 704f, 960f, 1280f, 1600f, 1920f, 2400f
	};

	static ThornsTerrainBootstrap _cachedBootstrap;
	static Scene _cachedBootstrapScene;

	static ThornsTerrainBootstrap ResolveBootstrap( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return null;

		if ( _cachedBootstrap is not null && _cachedBootstrap.IsValid() && _cachedBootstrapScene == scene )
			return _cachedBootstrap;

		_cachedBootstrapScene = scene;
		_cachedBootstrap = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault( b => b.IsWorldApplied );
		return _cachedBootstrap;
	}

	public static void InvalidateBootstrapCache()
	{
		_cachedBootstrap = null;
		_cachedBootstrapScene = null;
	}

	public static bool CanDrinkAt( Scene scene, GameObject playerRoot )
	{
		if ( scene is null || !scene.IsValid() || playerRoot is null || !playerRoot.IsValid() )
			return false;

		var bootstrap = ResolveBootstrap( scene );
		if ( bootstrap is null || !bootstrap.IsValid() )
			return false;

		var terrain = bootstrap.WorldTerrain;
		var field = bootstrap.GetHeightFieldForMap();
		var config = bootstrap.Config;
		if ( !terrain.IsValid() || field is null || config is null || !config.CreateWaterSheet )
			return false;

		var seaZ = terrain.GameObject.WorldPosition.z + config.SeaLevelNormalized * terrain.TerrainHeight;

		return TryAimAtWaterSurface( scene, playerRoot, bootstrap, terrain, field, config, seaZ );
	}

	/// <summary>0–1 volume blend for shoreline/wading ambience (no aim ray required).</summary>
	public static bool TryGetShoreProximityBlend( Scene scene, GameObject playerRoot, out float blend01 ) =>
		TryGetWaterAmbienceState( scene, playerRoot, out _, out blend01 );

	/// <summary>Nearest water emit point and distance-driven ambience blend for the local listener.</summary>
	public static bool TryGetWaterAmbienceState( Scene scene, GameObject playerRoot, out Vector3 waterEmitWorld, out float blend01 )
	{
		waterEmitWorld = default;
		blend01 = 0f;

		if ( scene is null || !scene.IsValid() || playerRoot is null || !playerRoot.IsValid() )
			return false;

		var bootstrap = ResolveBootstrap( scene );
		if ( bootstrap is null || !bootstrap.IsValid() )
			return false;

		var terrain = bootstrap.WorldTerrain;
		var field = bootstrap.GetHeightFieldForMap();
		var config = bootstrap.Config;
		if ( !terrain.IsValid() || field is null || config is null || !config.CreateWaterSheet )
			return false;

		var seaZ = terrain.GameObject.WorldPosition.z + config.SeaLevelNormalized * terrain.TerrainHeight;
		var feet = playerRoot.WorldPosition;

		if ( !TrySampleNearestWater(
			    terrain, field, config, feet, seaZ, WaterAmbienceAudibleRange, out waterEmitWorld, out var horizontalDistance ) )
			return false;

		var distanceFalloff = ThornsWorldSpatialSfx.ComputeDistanceFalloff(
			horizontalDistance, WaterAmbienceFullRange, WaterAmbienceAudibleRange );

		var deltaAboveWater = MathF.Max( 0f, feet.z - seaZ );
		var verticalFalloff = deltaAboveWater <= 0f
			? 1f
			: ThornsWorldSpatialSfx.ComputeDistanceFalloff(
				deltaAboveWater, 0f, WaterAmbienceVerticalFadeRange );

		blend01 = Math.Clamp( distanceFalloff * verticalFalloff, 0f, 1f );
		return blend01 >= 0.02f;
	}

	static bool TryAimAtWaterSurface(
		Scene scene,
		GameObject playerRoot,
		ThornsTerrainBootstrap bootstrap,
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig config,
		float seaZ )
	{
		if ( !ThornsSceneObserver.TryResolveLocalAimRay( playerRoot, out var origin, out var forward, useScreenCenter: true ) )
			return false;

		forward = forward.Normal;
		var waterSheet = bootstrap.WaterSheetObject;
		var end = origin + forward * InteractRange;

		var trace = scene.Trace.Ray( origin, end )
			.IgnoreGameObjectHierarchy( playerRoot )
			.Run();

		if ( trace.Hit && trace.GameObject.IsValid() )
		{
			if ( IsWaterSheetHit( trace.GameObject, waterSheet ) )
				return true;

			if ( trace.HitPosition.z > seaZ + WaterSurfaceMarginInches )
				return false;
		}

		if ( !TryIntersectSeaPlane( origin, forward, seaZ, out var surfacePoint ) )
			return false;

		if ( !IsExposedWaterAt( terrain, field, config, surfacePoint.x, surfacePoint.y ) )
			return false;

		var lineOfSight = scene.Trace.Ray( origin, surfacePoint )
			.IgnoreGameObjectHierarchy( playerRoot )
			.Run();

		if ( !lineOfSight.Hit )
			return true;

		if ( lineOfSight.GameObject.IsValid() && IsWaterSheetHit( lineOfSight.GameObject, waterSheet ) )
			return true;

		if ( lineOfSight.HitPosition.z > seaZ + WaterSurfaceMarginInches )
			return false;

		return IsExposedWaterAt( terrain, field, config, lineOfSight.HitPosition.x, lineOfSight.HitPosition.y );
	}

	static bool TryIntersectSeaPlane( Vector3 origin, Vector3 forward, float seaZ, out Vector3 surfacePoint )
	{
		surfacePoint = default;

		if ( MathF.Abs( forward.z ) < 0.02f )
			return false;

		var t = (seaZ - origin.z) / forward.z;
		if ( t < 8f || t > InteractRange )
			return false;

		surfacePoint = origin + forward * t;
		return (surfacePoint - origin).Length <= InteractRange + WaterSurfaceMarginInches;
	}

	static bool IsExposedWaterAt(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig config,
		float worldX,
		float worldY )
	{
		var origin = terrain.GameObject.WorldPosition;
		var size = terrain.TerrainSize;
		var localX = worldX - origin.x;
		var localY = worldY - origin.y;

		if ( localX < 32f || localY < 32f || localX > size - 32f || localY > size - 32f )
			return false;

		var sampler = new TerrainChunkSampler( field, size, terrain.TerrainHeight );
		var seaNorm = config.SeaLevelNormalized + 0.012f;
		return sampler.IsUnderSeaLevel( localX, localY, seaNorm );
	}

	static bool IsWaterSheetHit( GameObject hit, GameObject waterSheet )
	{
		if ( !hit.IsValid() || !waterSheet.IsValid() )
			return false;

		if ( hit == waterSheet || hit.Root == waterSheet )
			return true;

		for ( var go = hit; go.IsValid(); go = go.Parent )
		{
			if ( go == waterSheet )
				return true;
		}

		return false;
	}

	static bool TrySampleNearestWater(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig config,
		Vector3 feet,
		float seaZ,
		float maxSearch,
		out Vector3 nearestWaterWorld,
		out float horizontalDistance )
	{
		nearestWaterWorld = default;
		horizontalDistance = maxSearch;

		var origin = terrain.GameObject.WorldPosition;
		var size = terrain.TerrainSize;
		var localX = feet.x - origin.x;
		var localY = feet.y - origin.y;

		if ( localX < 32f || localY < 32f || localX > size - 32f || localY > size - 32f )
			return false;

		var sampler = new TerrainChunkSampler( field, size, terrain.TerrainHeight );
		var seaNorm = config.SeaLevelNormalized + 0.012f;

		if ( sampler.IsUnderSeaLevel( localX, localY, seaNorm )
		     && feet.z <= seaZ + MaxWadeBelowSea )
		{
			horizontalDistance = 0f;
			nearestWaterWorld = new Vector3( feet.x, feet.y, seaZ );
			return true;
		}

		var bestDistance = maxSearch;
		var bestWorld = Vector3.Zero;
		var found = false;

		for ( var direction = 0; direction < 24; direction++ )
		{
			var angle = direction * (MathF.PI * 2f / 24f);
			var dx = MathF.Cos( angle );
			var dy = MathF.Sin( angle );

			foreach ( var step in WaterSearchSteps )
			{
				if ( step > maxSearch )
					break;

				var px = localX + dx * step;
				var py = localY + dy * step;
				if ( px < 0f || py < 0f || px > size || py > size )
					break;

				if ( !sampler.IsUnderSeaLevel( px, py, seaNorm ) )
					continue;

				if ( step >= bestDistance )
					break;

				bestDistance = step;
				bestWorld = new Vector3( origin.x + px, origin.y + py, seaZ );
				found = true;
				break;
			}
		}

		if ( !found )
			return false;

		horizontalDistance = bestDistance;
		nearestWaterWorld = bestWorld;
		return true;
	}
}
