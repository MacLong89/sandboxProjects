#nullable disable

using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// World queries against the live <see cref="Terrain"/> mesh (terraingen) — used for spawn, scatter, and snaps.
/// </summary>
public static class ThornsTerraingenTerrainQueries
{
	static Scene _cachedTerrainScene;
	static Terrain _cachedTerrain;
	static double _cachedTerrainValidateUntil;

	public static bool TryFindTerrain( Scene scene, out Terrain terrain )
	{
		terrain = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		if ( _cachedTerrainScene == scene
		     && _cachedTerrain.IsValid()
		     && Time.Now < _cachedTerrainValidateUntil )
		{
			terrain = _cachedTerrain;
			return true;
		}

		if ( TryFindTerrainUncached( scene, out terrain ) )
		{
			_cachedTerrainScene = scene;
			_cachedTerrain = terrain;
			_cachedTerrainValidateUntil = Time.Now + 2.0;
			return true;
		}

		_cachedTerrainScene = default;
		_cachedTerrain = default;
		_cachedTerrainValidateUntil = 0;
		return false;
	}

	static bool TryFindTerrainUncached( Scene scene, out Terrain terrain )
	{
		terrain = default;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var chunk in scene.GetAllComponents<ThornsTerrainChunk>() )
		{
			if ( !chunk.IsValid() || !chunk.GameObject.IsValid() )
				continue;

			if ( TryGetTerrainFromChunkRoot( chunk.GameObject, out terrain ) )
				return true;
		}

		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;
			if ( TryGetTerrainFromChunkRoot( ts.GameObject, out terrain ) )
				return true;
		}

		return false;
	}

	static bool TryGetTerrainFromChunkRoot( GameObject chunkRoot, out Terrain terrain )
	{
		terrain = default;
		if ( chunkRoot is null || !chunkRoot.IsValid() )
			return false;

		var terrainGo = chunkRoot.Children.FirstOrDefault( c =>
			c.IsValid() && c.Name == ThornsTerraingenTerrainRuntime.TerrainChildName );
		if ( !terrainGo.IsValid() )
			return false;

		terrain = terrainGo.Components.Get<Terrain>( FindMode.EverythingInSelf );
		return terrain.IsValid();
	}

	public static bool TryRaycastTerrain(
		Terrain terrain,
		float worldX,
		float worldY,
		out Vector3 worldHit )
	{
		worldHit = default;
		if ( !terrain.IsValid() )
			return false;

		var maxHeight = terrain.TerrainHeight;
		var baseZ = terrain.GameObject.WorldPosition.z;
		var rayStart = new Vector3( worldX, worldY, baseZ + maxHeight * 2.5f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( !terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
			return false;

		worldHit = terrain.GameObject.WorldTransform.PointToWorld( localHit );
		return true;
	}

	public static bool TrySampleGroundWorld(
		Scene scene,
		float worldX,
		float worldY,
		float verticalOffsetInches,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( !TryFindTerrain( scene, out var terrain ) )
			return false;

		if ( !TryRaycastTerrain( terrain, worldX, worldY, out worldPosition ) )
			return false;

		worldPosition += Vector3.Up * verticalOffsetInches;
		return true;
	}

	/// <summary>Chunk-local XY (centered heightfield plane) → world position on the terraingen terrain surface.</summary>
	public static bool TryResolveScatterWorldOnTerrain(
		Scene scene,
		in ThornsTerrainNetSpec spec,
		GameObject chunkRoot,
		float localX,
		float localY,
		Model alignModel,
		Vector3 alignScale,
		bool clutterGrassLift,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( chunkRoot is null || !chunkRoot.IsValid() )
			return false;

		var planarWorld = chunkRoot.WorldPosition + chunkRoot.WorldRotation * new Vector3( localX, localY, 0f );
		if ( !TryFindTerrain( scene, out var terrain ) )
			return false;

		if ( alignModel.IsValid() )
		{
			if ( clutterGrassLift )
			{
				var cfg = GetClutterConfigFromScene( scene ) ?? new ThornsClutterConfig();
				var uniform = Math.Max( alignScale.x, 0.05f );
				if ( ThornsClutterSurface.TrySampleWorld(
					     terrain,
					     null,
					     planarWorld.x,
					     planarWorld.y,
					     alignModel,
					     uniform,
					     isGrass: true,
					     cfg,
					     out worldPosition ) )
					return worldPosition.z >= spec.WaterLevelWorldZ;
			}
			else
			{
				var foliageCfg = GetFoliageConfigFromScene( scene ) ?? new ThornsFoliageConfig();
				if ( ThornsFoliageSurface.TrySampleWorld(
					     terrain,
					     planarWorld.x,
					     planarWorld.y,
					     alignModel,
					     alignScale,
					     FoliageSpecies.Oak,
					     foliageCfg,
					     out worldPosition ) )
					return worldPosition.z >= spec.WaterLevelWorldZ;
			}
		}

		if ( !TryRaycastTerrain( terrain, planarWorld.x, planarWorld.y, out worldPosition ) )
			return false;

		return worldPosition.z >= spec.WaterLevelWorldZ;
	}

	static ThornsClutterConfig GetClutterConfigFromScene( Scene scene )
	{
		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( ts.IsValid() && ts.Enabled )
				return ts.TerraingenClutterConfig;
		}

		return null;
	}

	static ThornsFoliageConfig GetFoliageConfigFromScene( Scene scene )
	{
		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( ts.IsValid() && ts.Enabled )
				return ts.TerraingenFoliageConfig;
		}

		return null;
	}

	public static void InvalidateTerrainCache( Scene scene = null )
	{
		if ( scene is not null && scene.IsValid() && _cachedTerrainScene != scene )
			return;

		_cachedTerrainScene = default;
		_cachedTerrain = default;
		_cachedTerrainValidateUntil = 0;
	}

	public static float ResolvePeakWorldZRough( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return 1600f;

		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;

			var cfg = ts.TerraingenConfig ?? new ThornsTerrainConfig();
			return cfg.MaxTerrainHeightInches + cfg.SeaLevelNormalized * cfg.MaxTerrainHeightInches + 380f;
		}

		return 1600f;
	}
}
